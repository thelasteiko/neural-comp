using System;
using System.Collections.Concurrent;

namespace NeurCLib;
/// <summary>
/// A TaskEngine wraps a function in a loop. When the state
/// changes, the loop breaks out.
/// The TaskEngine does not do threading on it's own, but enables
/// tasks to track state and some parameters.
/// </summary>
internal class TaskEngine {
  /// <summary>
  /// Describes the state, or requested state, of the task object.
  /// <list type="bullet">
  ///   <item>
  ///     <term>Created</term>
  ///     <description>Just constructed</description>
  ///   <item>
  ///   <item>
  ///     <term>Running</term>
  ///     <description>Run loop is running</description>
  ///   <item>
  ///   <item>
  ///     <term>Timeout</term>
  ///     <description>For tasks that use the serial port, reports a timeout error</description>
  ///   <item>
  ///   <item>
  ///     <term>KillOrder</term>
  ///     <description>Received a kill order from the controller, will exit ASAP</description>
  ///   <item>
  ///   <item>
  ///     <term>Error</term>
  ///     <description>Some other error occured, exiting the task</description>
  ///   <item>
  /// </list>
  /// </summary>
  public enum TaskState {
    Created,
    Running,
    Timeout,
    KillOrder,
    Error
  }
  /// <summary>
  /// Name of the keepalive task
  /// </summary>
  public const String TE_KEEPALIVE = "keepalive";
  /// <summary>
  /// Name of the listener task
  /// </summary>
  public const String TE_LISTENER = "listener";
  /// <summary>
  /// Name of the consumer task
  /// </summary>
  public const String TE_CONSUMER = "consumer";
  /// <summary>
  /// Name of the commander task
  /// </summary>
  public const String TE_COMMANDER = "commander";
  /// <summary>
  /// Name of the streamer task
  /// </summary>
  public const String TE_STREAMER = "streamer";
  protected Mutex state_lock = new();
  protected TaskState __state;
  protected TaskState _state {
    get => __state;
    set {
      lock(state_lock) {__state = value;}
    }
  }
  /// <summary>
  /// Current state of the task.
  /// </summary>
  /// <value></value>
  public TaskState state {
    get => _state;
  }
  protected String _name;
  /// <summary>
  /// The name of the task which is used a key for the controller's task bag
  /// </summary>
  /// <value></value>
  public String name {get => _name;}
  /// <summary>
  /// In this context, controls the task bag and allows access to the port.
  /// </summary>
  protected Controller controller;
  /// <summary>
  /// Some tasks can continue in order to clear queues before dying.
  /// </summary>
  protected bool FinishWorkOnKill = false;
  /// <summary>
  /// Create a task with a name
  /// </summary>
  /// <param name="ctrl">Controller object</param>
  /// <param name="n">Name of the task</param>
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
  /// <returns>The final state of the task</returns>
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
/// <summary>
/// Sends the keepalive packet to the port and listens to the keepalive
/// queue for the response.
/// </summary>
internal class Keepalive : TaskEngine {
  private int last_keepalive = 0;
  private bool last_returned = true;
  /// <summary>
  /// Sends the keepalive packet to the port and listens to the keepalive
  /// queue for the response.
  /// </summary>
  /// <param name="ctrl"></param>
  /// <returns></returns>
  public Keepalive(Controller ctrl) : base(ctrl, TE_KEEPALIVE) {
    last_keepalive = 0;
    last_returned = true;
  }
  protected override void runner() {
    Package? p;
    if(last_keepalive > 0 && controller.q_keepalive.TryDequeue(out p)) {
      if (p.packetID != last_keepalive) {
        Log.warn($"Keepalive mismatch: {p.packetID} <> {last_keepalive}");
      } else {  
        Log.sys("Still Alive");
      }
      last_returned = true;
    }
    if (!last_returned) {
      Log.critical("Missed keepalive, retrying.");
    }
    p = new(PackType.Transaction, OpCode.Keepalive);
    Log.debug("Sending keepalive.");
    //check before writing
    if (state == TaskState.KillOrder) {
      Log.sys("Killing keepalive");
      return;
    }
    controller.Write(p.toStream(), p.length);
    
    last_keepalive = p.packetID;
    last_returned = false;
    Thread.Sleep(Controller.MAX_TIMEOUT);
    // Thread.Sleep(1000);
  }
}
/// <summary>
/// Listens to the port, builds packets, and queues them for sorting.
/// </summary>
internal class Listener : TaskEngine {
  
  private int timeout_count = 0;
  private int timeout_timeout;
  /// <summary>
  /// Listens to the port, builds packets, and queues them for sorting.
  /// </summary>
  /// <param name="ctrl">Controller object</param>
  /// <param name="timeout">How many timeout exceptions before deciding to end the task</param>
  /// <returns></returns>
  public Listener(Controller ctrl, int timeout=3) : base(ctrl, TE_LISTENER) {
    timeout_timeout = timeout;
    // Log.debug("Port timeout: " + controller.porter.ReadTimeout.ToString());
  }
  protected override void runner() {
    PackFactory pf = new();
    // Log.debug("Starting new read.");
    try {
      // go until we build or fail
      while (!pf.IsReady) {
        if (controller.porter.BytesToRead > 0) {
          // Log.debug("Bytes: " + controller.porter.BytesToRead.ToString());
          byte b = (byte) controller.porter.ReadByte();
          if (b >= 0) pf.build(b);
          else break;
          if (pf.IsFailed) throw new TimeoutException("Packet factory reset timeout.");
        } else {
          Thread.Sleep(Controller.MIN_TIMEOUT);
        }
      }
    } catch (TimeoutException e) {
      Log.warn("Port connection timeout: " + e.Message);
      timeout_count++;
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
    if (pf.pack.isValid()) {
      // got a valid packet
      // Log.debug("Packet valid, queueing");
      controller.q_all.Enqueue(pf.pack);
    }
  }
}
/// <summary>
/// Sorts the queued packets and hands them off to the correct queues.
/// </summary>
internal class Consumer : TaskEngine {
  private int reconnect_timeout;
  private int current_reconnect_attempts = 0;
  /// <summary>
  /// Sorts the queued packets and hands them off to the correct queues.
  /// </summary>
  /// <param name="ctrl">Controller object</param>
  /// <param name="timeout">How many reconnect attempts before ending task</param>
  /// <returns></returns>
  public Consumer(Controller ctrl, int timeout=3) : base(ctrl, TE_CONSUMER) {
    FinishWorkOnKill = true;
    reconnect_timeout = timeout;
  }
  protected override void runner() {
    Package? p;
    if (controller.q_all.TryDequeue(out p)) {
      // see what we got
      PackType pt = (PackType) p.packetType;
      if (pt is PackType.Failure) handleError(p);
      else if (pt is PackType.Transaction) handleTransaction(p);
      else if (pt is PackType.Stream) handleStream(p);
      else {
        Log.critical("Unknown packet received");
        Log.debug(p.ToString(), p.toStream());
      }
    } else if (state == TaskState.Running) {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    } else if (state == TaskState.KillOrder){
      // once the queue is depleted, die for real
      FinishWorkOnKill = false;
    }
  }
  
  /// <summary>
  /// Handle an error thrown by the arduino. Reads the error
  /// and prints an error message.
  /// </summary>
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
    if (errorType.In(ErrorType.BadChecksum, ErrorType.BadOpCode, ErrorType.BadPackType)) {
      _state = TaskState.Error;
    } else if (errorType == ErrorType.NotConnected) {
      if (current_reconnect_attempts >= reconnect_timeout) {
        _state = TaskState.Error;
      } else {
        controller.sendConnectAsync();
        current_reconnect_attempts++;
      }
    }
    Log.critical(report);
    Log.debug("Packet: " + p.ToString(), p.toStream());
  }
  /// <summary>
  /// Handle keepalives and control responses. Hands off to respective tasks.
  /// </summary>
  /// <param name="p"></param>
  protected void handleTransaction(Package p) {
    current_reconnect_attempts = 0;
    OpCode opc = (OpCode) p.payload[0];
    if (opc == OpCode.Keepalive) {
      controller.q_keepalive.Enqueue(p);
    } else if (opc.In(OpCode.StartStream, OpCode.StopStream, OpCode.Initial)) {
      // Log.debug("Command:", p.toStream());
      controller.q_controls.Enqueue(p);
    }
  }
  /// <summary>
  /// Hands off stream data to the streamer task.
  /// </summary>
  /// <param name="p"></param>
  protected void handleStream(Package p) {
    current_reconnect_attempts = 0;
    // check check
    if (!p.isValid()) {
      Log.warn("Bad checksum on stream packet.");
      Log.debug("Packet: " + p.ToString(), p.toStream());
    }
    StreamEventArgs args = new(p.payload);
    FileLog.write(args);
    controller.q_stream.Enqueue(args);
  }
}
/// <summary>
/// Sends commands on behalf of the user and listens for responses from
/// the arduino. Attempts to keep from sending the same command twice.
/// </summary>
internal class Commander : TaskEngine {
  /// <summary>
  /// Only one command can be sent at a time.
  /// </summary>
  private int last_command_id = 0;
  private bool last_returned = true;
  private OpCode last_command = OpCode.Unknown;
  /// <summary>
  /// Sends commands on behalf of the user and listens for responses from
  /// the arduino. Attempts to keep from sending the same command twice.
  /// </summary>
  /// <param name="ctrl"></param>
  /// <returns></returns>
  public Commander(Controller ctrl) : base(ctrl, TE_COMMANDER) {}
  protected override void runner() {
    OpCode op;
    Package? p;
    // check if user sent a command
    if (controller.q_user.TryDequeue(out op)) {
      // first check if we are triggering twice
      if (op != OpCode.Initial && last_command == op) {
        Log.sys("Command sent, please wait.");
        // reset to force next
        last_command = OpCode.Unknown;
      } else if (!last_returned) {
        // we are waiting for response
        Log.sys("Last command response not yet received, please wait.");
      } else {
        p = new(PackType.Transaction, op);
        Log.debug("Writing command");
        //check before writing
        if (state == TaskState.KillOrder) {
          Log.sys("Killing commander");
          return;
        }
        controller.Write(p.toStream(), p.length);
        last_command_id = p.packetID;
        last_command = op;
      }
    } else {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    }
    // we are waiting for a command
    if (last_command > 0 && controller.q_controls.TryDequeue(out p)) {
      if (p.packetID != last_command_id) {
        Log.warn($"Command mismatch: {p.packetID} <> {last_command}");
      }
      last_returned = true;
      OpCode oc = (OpCode)p.payload[0];
      if (oc == OpCode.StartStream) {
        controller._IsStreaming = true;
        FileLog.create();
      } else if (oc == OpCode.StopStream) {
        controller._IsStreaming = false;
        FileLog.close();
      } else if (oc == OpCode.Initial) {
        Log.sys("Connection initialized.");
        if (controller.UserStreaming) controller.q_user.Enqueue(OpCode.StartStream);
      }
      // reset for next command
      last_command_id = 0;
      last_command = OpCode.Unknown;
    } else {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    }
  }
}
/// <summary>
/// Hands off stream data to the user client via the controller's stream event.
/// user events.
/// </summary>
internal class Streamer : TaskEngine {
  /// <summary>
  /// Hands off stream data to the user client via the controller's stream event.
  /// </summary>
  /// <param name="ctrl"></param>
  /// <returns></returns>
  public Streamer(Controller ctrl) : base(ctrl, TE_STREAMER) {
    FinishWorkOnKill = true;
  }
  protected override void runner() {
    StreamEventArgs? args;
    if (controller.q_stream.TryDequeue(out args)) {
      // got stream data, send to user
      controller.handleOnStream(args);
    } else if (state == TaskState.Running) {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    } else if (state == TaskState.KillOrder) {
      // go until we run out
      FinishWorkOnKill = false;
    }
  }
}