using System;
using System.Collections.Concurrent;

namespace NeurCLib;

public class TaskEngine {
  public enum TaskState {
    Created,
    Running,
    Timeout,
    KillOrder,
    Error
  }
  internal static ConcurrentDictionary<String, TaskEngine> task_bag = new();
  internal static bool TryFindTask(String name, out TaskEngine? tsk) {
    if (task_bag.ContainsKey(name)) {
      tsk = task_bag[name];
      return true;
    }
    tsk = null;
    return false;
  }
  internal static void KillAll() {
    foreach (var tsk in task_bag) {
      tsk.Value.Kill();
    }
  }
  internal static bool IsAlive() {
    return task_bag.Count > 0;
  }
  public const String TE_KEEPALIVE = "keepalive";
  public const String TE_LISTENER = "listener";
  public const String TE_CONSUMER = "consumer";
  public const String TE_COMMANDER = "commander";
  protected Mutex state_lock = new();
  protected TaskState _state;
  public TaskState state {
    get => _state;
  }
  protected String _name;
  public String name {get => _name;}
  protected Controller controller;
  public TaskEngine(Controller ctrl, string n) {
    controller = ctrl;
    _state = TaskState.Created;
    _name = n;
  }

  public void Kill() {
    // change status out of sync
    lock (state_lock) {
      _state = TaskState.KillOrder;
    }
  }
  
  public void Run() {
    lock (state_lock) {
      _state = TaskState.Running;
    }
    task_bag.TryAdd(name, this);
    while (state == TaskState.Running) {
      runner();
    }
    // if we break out, remove from bag
    task_bag.TryRemove(name, out _);
  }
  protected virtual void runner() {
    throw new NotImplementedException();
  }
}

public class Keepalive : TaskEngine {
  private int last_keepalive = 0;
  private Mutex return_lock = new();
  private bool last_returned = true;
  public Keepalive(Controller ctrl) : base(ctrl, TE_KEEPALIVE) {
  }
  protected override void runner() {
    if (!last_returned) {
      Log.critical("Missed keepalive, resetting");
      _state = TaskState.Error;
      return;
    }
    Package p = new(PackType.Transaction, OpCode.Keepalive);
    controller.Write(p.toStream(), p.length);
    
    lock(return_lock) {
      last_keepalive = p.packetID;
      last_returned = false;
    }
    Thread.Sleep(5000);
  }
  public void sync(Package p) {
    lock(return_lock) {
      if (p.packetID != last_keepalive) {
        Log.warn($"Keepalive mismatch: {p.packetID} <> {last_keepalive}");
        _state = TaskState.Error;
      } else {  
        Log.sys("Still Alive");
        last_returned = true;
      }
    }
  }
}

public class Listener : TaskEngine {
  
  private int timeout_count = 0;
  private int timeout_timeout;
  public Listener(Controller ctrl, int timeout=3) : base(ctrl, TE_LISTENER) {
    timeout_timeout = timeout;
  }
  protected override void runner() {
    // while state alive
      PackFactory pf = new();
      try {
        while (!pf.IsReady) {
          pf.build((byte) controller.porter.ReadByte());
          if (pf.IsFailed) throw new TimeoutException("Packet factory reset timeout.");
        }
      } catch (TimeoutException) {
        Log.warn("Port connection timeout");
        timeout_count++;
        _state = TaskState.Timeout;
        if (timeout_count >= timeout_timeout) {
          Log.critical("Reached connection timeout limit.");
          _state = TaskState.Error;
        }
        return;
      } catch (Exception e2) {
        Log.critical("{0}: {1}", e2.GetType().Name, e2.Message);
        _state = TaskState.Error;
        return;
      }
      timeout_count = 0;
      // got a valid packet
      controller.que.Enqueue(pf.pack);
  }
}
public class StreamEventArgs : EventArgs {
  public StreamPacket data;
  public StreamEventArgs(StreamPacket sp) {
    data = sp;
  }
}
public class Consumer : TaskEngine {
  
  public Consumer(Controller ctrl) : base(ctrl, TE_CONSUMER) {
    
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
    }
  }
  
  /// <summary>
  /// Handle an error thrown by the arduino. Reads the error
  /// and prints an error message.
  /// </summary>
  /// <param name="ray">Output from arduino</param>
  protected void handleError(Package p) {
    _state = TaskState.Error;
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
      if (TryFindTask(TE_KEEPALIVE, out tsk)){
        ((Keepalive) tsk).sync(p);
      }
    } else if (opc == OpCode.StartStream || opc == OpCode.StopStream) {
      Commander.sync(p);
    }
  }
  protected void handleStream(Package p) {
    // check check
    if (!p.isValid()) {
      Log.warn("Bad checksum on stream packet.");
      Log.debug("Packet: " + p.ToString(), p.toStream());
    }
    // payload is the data
    StreamPacket sp = new(p.payload);
    
    // also save result to log file

  }
}

public class Commander : TaskEngine {
  private OpCode operation;
  /// <summary>
  /// Only one command can be sent at a time.
  /// </summary>
  private static int last_command;
  private static Mutex return_lock = new();
  private static bool last_returned = true;
  public Commander(Controller ctrl, OpCode opc) : base(ctrl, TE_COMMANDER) {
    operation = opc;
  }
  protected override void runner() {
    // set to kill right away
    _state = TaskState.KillOrder;
    if (!last_returned) {
      Log.sys("Last command not yet returned.");
      // reset the return lock...b/c
      last_returned = true;
      return;
    }
    // write command
    Package p = new(PackType.Transaction, operation);
    controller.Write(p.toStream(), p.length);

    lock (return_lock) {
      last_command = (int)p.packetID;
      last_returned = false;
    }
  }
  public static void sync(Package p) {
    lock (return_lock) {
      if (p.packetID != last_command) {
        Log.warn($"Command mismatch: {p.packetID} <> {last_command}");
      }
      last_returned = true;
    }
  }
}

