using System;
using System.Collections.Concurrent;

namespace NeurCLib;
/// <summary>
/// A TaskEngine wraps a function in a loop. When the state
/// changes, the loop breaks out.
/// The TaskEngine does not do threading on it's own.
/// </summary>
internal class TaskEngine {
  /// <summary>
  /// Describes the state, or requested state, of the task object.
  /// </summary>
  public enum TaskState {
    Created,
    Running,
    Timeout,
    RequestDenied,
    KillOrder,
    Error
  }
  
  public const String TE_KEEPALIVE = "keepalive";
  public const String TE_LISTENER = "listener";
  public const String TE_CONSUMER = "consumer";
  public const String TE_COMMANDER = "commander";
  protected Mutex state_lock = new();
  protected TaskState __state;
  protected TaskState _state {
    get => __state;
    set {
      lock(state_lock) {__state = value;}
    }
  }
  public TaskState state {
    get => _state;
  }
  protected String _name;
  public String name {get => _name;}
  protected Controller controller;
  protected bool FinishWorkOnKill = false;
  public TaskEngine(Controller ctrl, string n) {
    controller = ctrl;
    _state = TaskState.Created;
    _name = n;
    Log.debug($"Task '{_name}' created.");
  }
  /// <summary>
  /// Set the status to KillOrder, prompting the task to finish ASAP.
  /// </summary>
  public virtual void Kill() {
    // change status out of sync
    _state = TaskState.KillOrder;
  }
  /// <summary>
  /// Runs until told to stop. Should be called within it's own thread.
  /// </summary>
  /// <returns></returns>
  public TaskState Run() {
    _state = TaskState.Running;
    Log.debug($"Task '{_name}' running.");
    controller.task_bag.TryAdd(name, this);
    while (state == TaskState.Running || FinishWorkOnKill) {
      //Log.debug($"Task '{_name}' on thread {Thread.CurrentThread.ManagedThreadId}");
      runner();
    }
    // if we break out, remove from bag
    controller.task_bag.TryRemove(name, out _);
    return state;
  }
  protected virtual void runner() {
    throw new NotImplementedException();
  }
}

internal class Keepalive : TaskEngine {
  private int last_keepalive = 0;
  private Mutex return_lock = new();
  private bool last_returned = true;
  public Keepalive(Controller ctrl) : base(ctrl, TE_KEEPALIVE) {
    last_keepalive = 0;
    last_returned = true;
  }
  public override void Kill() {
    base.Kill();
    lock(return_lock) {
      last_keepalive = 0;
      last_returned = true;
    }
  }
  protected override void runner() {
    if (!last_returned) {
      Log.critical("Missed keepalive, resetting");
      _state = TaskState.Error;
      return;
    }
    Package p = new(PackType.Transaction, OpCode.Keepalive);
    Log.debug("Sending keepalive.");
    controller.Write(p.toStream(), p.length);
    
    lock(return_lock) {
      last_keepalive = p.packetID;
      last_returned = false;
    }
    Thread.Sleep(Controller.MAX_TIMEOUT);
    //Thread.Sleep(1000);
  }
  public void sync(Package p) {
    lock(return_lock) {
      if (p.packetID != last_keepalive) {
        Log.warn($"Keepalive mismatch: {p.packetID} <> {last_keepalive}");
        _state = TaskState.Error;
      } else {  
        Log.critical("Still Alive");
        last_returned = true;
      }
    }
  }
}

internal class Listener : TaskEngine {
  
  private int timeout_count = 0;
  private int timeout_timeout;
  public Listener(Controller ctrl, int timeout=3) : base(ctrl, TE_LISTENER) {
    timeout_timeout = timeout;
  }
  protected override void runner() {
    // while state alive
      PackFactory pf = new();
      // Log.debug("Starting new read.");
      try {
        // probably the package is ready but not breaking out
        while (!pf.IsReady) {
          pf.build((byte) controller.porter.ReadByte());
          if (pf.IsFailed) throw new TimeoutException("Packet factory reset timeout.");
        }
      } catch (TimeoutException e) {
        Log.warn("Port connection timeout: " + e.Message);
        timeout_count++;
        //_state = TaskState.Timeout;
        if (timeout_count >= timeout_timeout) {
          Log.critical("Reached connection timeout limit.");
          _state = TaskState.Timeout;
        }
        return;
      } catch (Exception e2) {
        Log.critical(String.Format("{0}: {1}", e2.GetType().Name, e2.Message));
        _state = TaskState.Error;
        return;
      }
      timeout_count = 0;
      // got a valid packet
      // Log.debug("Packet valid, queueing");
      controller.que.Enqueue(pf.pack);
  }
}

internal class Consumer : TaskEngine {
  public Consumer(Controller ctrl) : base(ctrl, TE_CONSUMER) {
    FinishWorkOnKill = true;
  }
  protected override void runner() {
    Package? p;
    if (controller.que.TryDequeue(out p)) {
      // see what we got
      PackType pt = (PackType) p.packetType;
      if (pt is PackType.Failure) handleError(p);
      else if (pt is PackType.Transaction) handleTransaction(p);
      else if (pt is PackType.Stream) handleStream(p);
      else Log.critical("Unknown packet: " + p.ToString());
    } else if (state == TaskState.KillOrder){
      // once the queue is depleted, die for real
      FinishWorkOnKill = false;
    }
  }
  
  /// <summary>
  /// Handle an error thrown by the arduino. Reads the error
  /// and prints an error message.
  /// </summary>
  /// <param name="ray">Output from arduino</param>
  protected void handleError(Package p) {
    //_state = TaskState.Error;
    if (p.payload.Length == 0) {
      Log.critical("Error occured: 0 length response");
      return;
    }
    ErrorType errorType = (ErrorType) p.payload[0];
    String report = "Error occured: ";
    report += errorType switch {
      ErrorType.BadChecksum => "Bad checksum: " + ((int)p.checksum).ToString(),
      ErrorType.TooLong => "Payload is too long: " + ((int)p.payloadSize).ToString() + " <> " + p.payload.Length.ToString(),
      ErrorType.BadPackType => "Bad packet type: " + ((PackType)p.packetType).ToString(),
      ErrorType.BadOpCode => "Bad Op code: " + ((OpCode)p.payload[0]).ToString(),
      ErrorType.AlreadyConnected => "Already connected",
      ErrorType.AlreadyStreaming => "Already streaming",
      ErrorType.AlreadyStopped => "Already stopped streaming",
      ErrorType.NotConnected => "Not connected",
      _ => throw new ArgumentOutOfRangeException(errorType.ToString(), $"Unexpected error: {errorType.ToString()}")
    };
    Log.critical(report);
    Log.debug("Packet: " + p.ToString(), p.toStream());
  }
  protected void handleTransaction(Package p) {
    OpCode opc = (OpCode) p.payload[0];
    if (opc == OpCode.Keepalive) {
      TaskEngine? tsk;
      if (controller.TryFindTask(TE_KEEPALIVE, out tsk)){
        // tsk has to be non-null here
        #pragma warning disable CS8600, CS8602
        ((Keepalive)tsk).sync(p);
        #pragma warning restore CS8600, CS8602
      }
    } else if (opc == OpCode.StartStream || opc == OpCode.StopStream) {
      // Log.debug("Command:", p.toStream());
      Commander.sync(p);
    }
  }
  protected void handleStream(Package p) {
    // check check
    if (!p.isValid()) {
      Log.warn("Bad checksum on stream packet.");
      Log.debug("Packet: " + p.ToString(), p.toStream());
    }
    // Log.debug("Stream data:", p.toStream());
    // payload is the data
    StreamEventArgs args = new(p.payload);
    // Log.debug("After args");
    controller.handleOnStream(args);
    // also save result to log file
    FileLog.write(args);
  }
}

internal class Commander : TaskEngine {
  private OpCode operation;
  private int wait_timeout;
  private int wait_interval;
  /// <summary>
  /// Only one command can be sent at a time.
  /// </summary>
  private static int last_command = 0;
  private static Mutex return_lock = new();
  private static bool last_returned = true;
  private static Package? return_package = null;
  public Commander(Controller ctrl, OpCode opc) : base(ctrl, TE_COMMANDER) {
    operation = opc;
    wait_interval = Controller.MAX_TIMEOUT;
    wait_timeout = 3;
  }
  public override void Kill() {
    base.Kill();
    lock(return_lock) {
      last_command = 0;
      last_returned = true;
      return_package = null;
    }
  }
  protected override void runner() {
    if (!last_returned) {
      Log.sys("Last command not yet returned.");
      // reset the return lock...b/c
      last_returned = true;
      return;
    }
    // write command
    Package p = new(PackType.Transaction, operation);
    Log.debug("Writing command");
    controller.Write(p.toStream(), p.length);

    lock (return_lock) {
      last_command = (int)p.packetID;
      last_returned = false;
    }
    // Log.debug($"Waiting for response: {wait_timeout}|{wait_interval}");
    // Log.debug("Last response: " + last_returned.ToString());
    int c = 0;
    while (!last_returned && c < wait_timeout) {
      // Log.debug($"{c} :: Waiting {wait_interval}ms");
      Thread.Sleep(wait_interval);
      c++;
    }
    if (!last_returned || return_package is null) {
      Log.sys("Request not honored.");
      _state = TaskState.RequestDenied;
      return;
    }
    lock (return_lock) {
      OpCode oc = (OpCode)return_package.payload[0];
      if (oc == OpCode.StartStream) {
        controller._IsStreaming = true;
        FileLog.create();
      } else if (oc == OpCode.StopStream) {
        controller._IsStreaming = false;
        FileLog.close();
      }
    }
    // don't repeat
    _state = TaskState.KillOrder;
  }
  public static void sync(Package p) {
    lock (return_lock) {
      if (p.packetID != last_command) {
        Log.warn($"Command mismatch: {p.packetID} <> {last_command}");
      }
      last_returned = true;
      return_package = p;
    }
  }
}
