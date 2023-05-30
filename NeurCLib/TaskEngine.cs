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
  #region parameters
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
  /// <summary>
  /// Name of the notifier
  /// </summary>
  public const String TE_NOTIFIER = "notifier";
  protected Mutex state_lock = new();
  protected TaskState __state;
  protected TaskState _state {
    get => __state;
    set {
      Log.debug(_name + " set state " + value);
      lock(state_lock) {__state = value;}
    }
  }
  private Task<TaskState>? _worker;
  public Task<TaskState>? worker {
    get => _worker;
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
  #endregion
  /// <summary>
  /// Create a task with a name
  /// </summary>
  /// <param name="ctrl">Controller object</param>
  /// <param name="n">Name of the task</param>
  public TaskEngine(Controller ctrl, string n) {
    controller = ctrl;
    _name = n;
    _state = TaskState.Created;
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
  /// Creates and starts a task that will run until this object is sent the kill order,
  /// or until an exception occurs.
  /// </summary>
  /// <returns>The final state of the task</returns>
  public Task<TaskState> Run() {
    _worker = Task.Factory.StartNew<TaskState>(() => {
      _state = TaskState.Running;
      Log.debug($"Task '{_name}' running.");
      //controller.taskBag.TryAdd(this);
      while ((state == TaskState.Running || FinishWorkOnKill) && state != TaskState.Error) {
        //Log.debug($"Task '{_name}' on thread {Thread.CurrentThread.ManagedThreadId}");
        runner();
      }
      // if we break out, remove from bag
      controller.taskBag.TryRemove(this);
      return state;
    });
    return _worker;
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
    // do a thread check
    controller.taskBag.Check();
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
      Log.critical($"Missed keepalive # {last_keepalive}, retrying.");
    }
    p = new(PackType.Transaction, OpCode.Keepalive);
    Log.debug("Sending keepalive.", p.toStream());
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
          //Log.debug("Bytes: " + controller.porter.BytesToRead.ToString());
          int b = controller.porter.ReadByte();
          if (b >= 0) pf.build((byte) b);
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
      if (p.isFailure()) handleError(p);
      else if (p.isTransaction()) handleTransaction(p);
      else if (p.isStream()) handleStream(p);
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
      ErrorType.AlreadyTherapy => "Already administering therapy",
      ErrorType.AlreadyNotTherapy => "Already stopped therapy",
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
    } else if (errorType.In(ErrorType.AlreadyStreaming, ErrorType.AlreadyStopped, ErrorType.AlreadyTherapy, ErrorType.AlreadyNotTherapy)) {
      controller.resetSendState();
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
    } else if (opc.In(OpCode.StartStream, OpCode.StopStream, OpCode.Initial, OpCode.StartStim, OpCode.StopStim)) {
      // Log.debug("Command:", p.toStream());
      controller.q_command_responses.Enqueue(p);
      controller.q_client_events.Enqueue(p);
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
    //FileLog.write(args);
    controller.q_stream.Enqueue(args);
    controller.q_client_events.Enqueue(p);
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
    if (controller.q_commands.TryDequeue(out op)) {
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
    }
    Thread.Sleep(Controller.MIN_TIMEOUT);
    // we are waiting for a command
    if (last_command > 0 && controller.q_command_responses.TryDequeue(out p)) {
      if (p.packetID != last_command_id) {
        Log.warn($"Command mismatch: {p.packetID}|{p.opCode} <> {last_command_id}|{last_command}");
      }
      last_returned = true;
      OpCode oc = (OpCode)p.payload[0];
      if (oc == OpCode.StartStream) {
        controller.StartStreamSent = false;
        controller._IsStreaming = true;
        FileLog.create();
      } else if (oc == OpCode.StopStream) {
        controller.StopStreamSent = false;
        controller._IsStreaming = false;
        FileLog.close();
      } else if (oc == OpCode.StartStim) {
        controller.StartStimSent = false;
        controller._IsStimming = true;
      } else if (oc == OpCode.StopStim) {
        controller.StopStimSent = false;
        controller._IsStimming = false;
      } else if (oc == OpCode.Initial) {
        Log.sys("Connection initialized.");
        // TODO may have to rethink using user streaming
        if (controller.UserStreaming) controller.q_commands.Enqueue(OpCode.StartStream);
      }
      // reset for next command
      last_command_id = 0;
      last_command = OpCode.Unknown;
    } else {
      // Thread.Sleep(Controller.MIN_TIMEOUT);
    }
  }
}
/// <summary>
/// Processes stream data to save to file and predict if there is a seizure happening.
/// </summary>
internal class Streamer : TaskEngine {
  SignalWindow window = new();
  bool seizure_detected;
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
      window.add(args.microvolts);
      //Log.debug($"Added signal # {window.Count}: {args.microvolts}");
      if (window.PredictReady) {
        // Log.debug("window is prediction ready");
        seizure_detected = window.predict();
        //controller.SendKill();
      }
      double c = window.confidence();
      //Log.debug($"Seizure {seizure_detected}, confidence is {c}, stimming is {controller.IsStimming}");
      if (seizure_detected && c > 0.0 && !controller.IsStimming) {
        //Log.write("start")
        controller.startTherapy();
      } else if (!seizure_detected && c < 0.0 && controller.IsStimming) {
        controller.stopTherapy();
      }
      FileLog.write(args, seizure_detected, controller.IsStimming, c);
    } else if (state == TaskState.Running) {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    } else if (state == TaskState.KillOrder) {
      FinishWorkOnKill = false;
    }
  }
}
internal class Notifier : TaskEngine {
  public Notifier(Controller ctrl) : base(ctrl, TE_NOTIFIER) {
    FinishWorkOnKill = true;
  }

  protected override void runner() {
    Package? p;
    if (controller.q_client_events.TryDequeue(out p)) {
      if (p.isStream()) {
        StreamEventArgs args = new(p.payload);
        controller.handleOnStream(args);
      } else if (p.isCommand()) {
        OpCode c = p.opCode;
        switch (c) {
          case OpCode.StartStream:
            controller.handleOnStreamStart();
            break;
          case OpCode.StopStream:
            controller.handleOnStreamStop();
            break;
          case OpCode.StartStim:
            controller.handleOnTherapyStart();
            break;
          case OpCode.StopStim:
            controller.handleOnTherapyStop();
            break;
        }
      }
      
    } else if (state == TaskState.Running) {
      Thread.Sleep(Controller.MIN_TIMEOUT);
    } else if (state == TaskState.KillOrder) {
      // go until we run out
      FinishWorkOnKill = false;
    }
  }
}