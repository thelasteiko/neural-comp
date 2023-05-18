
using System.Collections.Concurrent;
using System.IO.Ports;
namespace NeurCLib;
/// <summary>
/// Main class that works as the interface for all functionality.
/// </summary>
public class Controller : IDisposable {
  /// <summary>
  /// Describes state of the controller object.
  /// <list type="bullet">
  ///   <item>
  ///     <term>Created</term>
  ///     <description>newly created or in the same state as new</description>
  ///   </item>
  ///   <item>
  ///     <term>Opened</term>
  ///     <description>port is open but not connected</description>
  ///   </item>
  ///   <item>
  ///     <term>Connected</term>
  ///     <description>port is open and we are connected</description>
  ///   </item>
  ///   <item>
  ///     <term>Running</term>
  ///     <description>subtasks are up and running</description>
  ///   </item>
  ///   <item>
  ///     <term>Restart</term>
  ///     <description>one of the subtasks failed and now we are restarting</description>
  ///   </item>
  ///   <item>
  ///     <term>Stopping</term>
  ///     <description>the stop command was sent; killing tasks and closing port</description>
  ///   </item>
  ///   <item>
  ///     <term>Error</term>
  ///     <description>something bad happened</description>
  ///   </item>
  /// </list>
  /// </summary>
  public enum ControlState {
    Created,
    Opened,
    Connected,
    Running,
    Restart,
    Stopping,
    Error
  }
  /// <summary>
  /// Determines the timeout for reading from the port, and the time
  /// interval for sending keepalive packets.
  /// </summary>
  internal const int MAX_TIMEOUT = 5000;
  /// <summary>
  /// Time to sleep threads when there is no input to process.
  /// </summary>
  internal const int MIN_TIMEOUT = 100;

  #region properties
  /// <summary>
  /// Mutex for writing to the serial port.
  /// </summary>
  /// <returns></returns>
  internal System.Threading.Mutex write_mutex = new();
  /// <summary>
  /// Serial port object.
  /// </summary>
  //internal IPorter porter;
  internal SerialPort porter;
  /// <summary>
  /// Handler for when we get stream data.
  /// </summary>
  public event EventHandler<StreamEventArgs>? Stream;
  /// <summary>
  /// Queue for packages from the arduino to be processed.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<Package> q_all = new();
  /// <summary>
  /// Queue for keepalive response packages.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<Package> q_keepalive = new();
  /// <summary>
  /// Queue for commands from the user.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<OpCode> q_user = new();
  /// <summary>
  /// Queue for the responses to commands.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<Package> q_controls = new();
  /// <summary>
  /// Queue for stream packets, to be sent via the Stream handler
  /// and logged to a file.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<StreamEventArgs> q_stream = new();
  /// <summary>
  /// Holds the currently running tasks.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentDictionary<String, TaskEngine> task_bag = new();
  private ControlState _status;
  /// <summary>
  /// Current state of the control object.
  /// </summary>
  /// <value></value>
  public ControlState status {get => _status;}
  /// <summary>
  /// How long to wait for existing threads to die before
  /// attempting to restart them. This is the maximum wait time.
  /// </summary>
  public int KillTimeout = MAX_TIMEOUT / 10;
  internal System.Threading.Mutex stream_lock = new();
  private bool __IsStreaming = false;
  internal bool _IsStreaming {
    get => __IsStreaming;
    set {
      lock(stream_lock) {__IsStreaming = value;}
    }
  }
  /// <summary>
  /// Set to true if the start stream command was sent and the
  /// response came back. False otherwise.
  /// </summary>
  /// <value></value>
  public bool IsStreaming {get => _IsStreaming;}
  private bool _UserStreaming = false;
  /// <summary>
  /// The stream state the user last requested.
  /// </summary>
  internal bool UserStreaming {
    get => _UserStreaming;
  }
  private bool disposed = false;
  #endregion
  /// <summary>
  /// Creates a new controller for interacting with the arduino.
  /// Creates but does not open the serial port.
  /// </summary>
  public Controller() {
    porter = new SerialPort{
      BaudRate = 115200,
      DataBits = 8,
      StopBits = StopBits.One,
      Parity = Parity.None,
      WriteTimeout = 500,
      ReadTimeout = MAX_TIMEOUT
    };

    _IsStreaming = false;
    disposed = false;
    _status = ControlState.Created;
  }
  #region helpers
  /// <summary>
  /// Write directly to the serial port, if you really want to.
  /// Does nothing if the port isn't open. Reports exceptions but
  /// continues.
  /// </summary>
  /// <param name="buffer"></param>
  /// <param name="length"></param>
  public void Write(byte[] buffer, int length) {
    if (!porter.IsOpen) return;
    lock (write_mutex) {
      try {
        porter.Write(buffer, 0, length);
      } catch (Exception e) {
        Log.critical(String.Format("{0}: {1}", e.GetType().Name, e.Message));
      }
    }
  }
  /// <summary>
  /// Checks if the controller is in an active state, starting,
  /// running, or restarting.
  /// </summary>
  /// <returns></returns>
  public bool IsRunning() {
    return (status.In(ControlState.Running, ControlState.Restart) &&
      PriorityState() <= TaskEngine.TaskState.Running);
  }
  /// <summary>
  /// Clear all the queues.
  /// </summary>
  private void ClearQueues() {
    q_all.Clear();
    q_keepalive.Clear();
    q_user.Clear();
    q_controls.Clear();
    q_stream.Clear();
  }
  /// <summary>
  /// Tries to find the named task. If the task exists, sets tsk equal to it.
  /// </summary>
  /// <param name="name">Name of the task</param>
  /// <param name="tsk"></param>
  /// <returns>Returns true if the task exists and tsk is not null. False otherwise.</returns>
  internal bool TryFindTask(String name, out TaskEngine? tsk) {
    if (task_bag.ContainsKey(name)) {
      tsk = task_bag[name];
      return true;
    }
    tsk = null;
    return false;
  }
  /// <summary>
  /// Iterates through the existing tasks and sends the kill order.
  /// Tasks will complete before removing themselves.
  /// </summary>
  internal void SendKill() {
    foreach (var tsk in task_bag) {
      tsk.Value.Kill();
    }
  }
  /// <summary>
  /// Gets the max state of all running tasks.
  /// </summary>
  /// <returns></returns>
  internal TaskEngine.TaskState PriorityState() {
    TaskEngine.TaskState ts = TaskEngine.TaskState.Created;
    foreach (var tsk in task_bag) {
      if(tsk.Value.state > ts) ts = tsk.Value.state;
    }
    return ts;
  }
  /// <summary>
  /// Indicates whether there are any tasks in the task bag still alive.
  /// </summary>
  /// <returns></returns>
  internal bool IsAlive() {
    return task_bag.Count > 0;
  }
  /// <summary>
  /// Sends the stream data via the registered stream events.
  /// </summary>
  /// <param name="args"></param>
  internal void handleOnStream(StreamEventArgs args) {
    // Log.debug("Triggering invokes");
    if (Stream is not null) {
      Delegate[] degs = Stream.GetInvocationList();
      for (int i = 0; i < degs.Length; i++) {
        degs[i].DynamicInvoke(this, args);
      }
    }
  }
  /// <summary>
  /// Helper that pauses an awaiting thread for some interval
  /// and prints a waiting 'bar' as an indicator.
  /// </summary>
  /// <returns></returns>
  public async Task doAWait(int steps=5, int sleepFor=1000) {
    await Task.Factory.StartNew(() => {
      for (int i = 0; i < steps; i++){
        Thread.Sleep(sleepFor);
        Console.Write(".. ");
      }
      Console.WriteLine();
    });
  }
  #endregion

  /// <summary>
  /// Opens and connects to the arduino, then starts all subtasks.
  /// </summary>
  public async Task start() {
    Log.sys("Starting...");
    await Task.Factory.StartNew(() => {
      if (porter.IsOpen) porter.Close();
      if (connect()) {
        _status = ControlState.Opened;
        Log.sys("Connection successful, starting subtasks.");
        spawnTasks();
        _status = ControlState.Running;
      }
    });
    if (status == ControlState.Running) {
      if (IsStreaming) {
        _IsStreaming = false;
        startStreaming();
      }
    }
  }
  /// <summary>
  /// Creates independent threads for each task.
  /// <list type="bullet">
  ///   <item>listening to the port</item>
  ///   <item>consuming and sorting packages</item>
  ///   <item>keepalive</item>
  ///   <item>handling commands</item>
  ///   <item>handling stream data</item>
  /// </list>
  /// If any task fails and exits, the controller attempts a reconnect.
  /// </summary>
  private void spawnTasks() {
    Task.Factory.ContinueWhenAny(new Task[] {
        Task.Factory.StartNew(() => new Listener(this).Run()),
        Task.Factory.StartNew(() => new Consumer(this).Run()),
        Task.Factory.StartNew(() => new Keepalive(this).Run()),
        Task.Factory.StartNew(() => new Commander(this).Run()),
        Task.Factory.StartNew(() => new Streamer(this).Run())
      },
      (task) => reconnect(task)
    );
    Log.sys("Subtasks started.");
  }
  /// <summary>
  /// Sets up the initial connection by searching for an available
  /// port and then attempting to send the initial connect request.
  /// Continues until all ports are exhausted.
  /// </summary>
  /// <returns></returns>
  private bool connect() {
    // Log.debug("Entered connect");
    // get list of connections
    string[] ports = SerialPort.GetPortNames();
    for (int i = 0; i < ports.Length; i++) {
      // for each, try open
      porter.PortName = ports[i];
      _status = ControlState.Created;
      Log.debug("Trying to connect to " + ports[i]);
      try {
        porter.Open();
        porter.DiscardInBuffer();
        porter.DiscardOutBuffer();
      } catch (System.Exception) {
        Log.warn($"Failed to open '{ports[i]}'");
        if (porter.IsOpen) porter.Close();
        continue;
      }
      Log.debug("Connected to " + ports[i]);
      _status = ControlState.Opened;
      if (sendConnect()) return true;
      else if(porter.IsOpen) porter.Close();
    }
    return false;
  }
  /// <summary>
  /// Helper for sending the initial connect request.
  /// Assumes the port is open.
  /// </summary>
  /// <returns></returns>
  private bool sendConnect() {
    Package p = new(PackType.Transaction, OpCode.Initial);
    Log.debug("Sending intial connect");
    byte[] buffer = new byte[p.length];
    // try three times
    bool failed = false;
    for (int i = 0; i < 3; i++) {
      // Log.debug("Trying read");
      try {
        porter.Write(p.toStream(), 0, p.length);
        buffer = new byte[p.length];
        int n = 0;
        failed = false;
        for (int offset = 0; offset < p.length;) {
          n = porter.Read(buffer, offset, buffer.Length - offset);
          // Log.debug($"Bytes read={n} <=> ray size={buffer.Length}");
          offset += n;
        }
      } catch (TimeoutException) {
        Log.warn("Port connection timeout");
        failed = true;
      } catch (Exception e2) {
        Log.critical(String.Format("{0}: {1}", e2.GetType().Name, e2.Message));
        return false;
      }
      // Log.debug("t := " + t.ToString());
      if (failed) Thread.Sleep(1000);
      else break;
    }
    if (failed) return false;
    if (p.isEqual(buffer)) {
      _status = ControlState.Connected;
      return true;
    } else {
      Log.warn($"Could not connect to '{porter.PortName}'");
    }
    return false;
  }
  internal void sendConnectAsync() {
    q_user.Enqueue(OpCode.Initial);
  }
  /// <summary>
  /// Sends the kill order to all threads and waits for a bit.
  /// If the threads take longer to exit than the KillTimeout,
  /// logs warning and continues anyway.
  /// </summary>
  /// <returns></returns>
  private async Task KillAll() {
    SendKill();
    await Task.Factory.StartNew(() => {
      int counter = 0;
      while (IsAlive() && counter < KillTimeout) {
        counter++;
        Thread.Sleep(10);
      }
      if (IsAlive())
        Log.warn("Old tasks still running. Queue: " + q_all.Count);
    });
  }
  /// <summary>
  /// Kills the current tasks and attempts to reconnect to the arduino.
  /// </summary>
  /// <param name="tsk"></param>
  /// <returns></returns>
  private async void reconnect(Task tsk) {
    if (status == ControlState.Stopping) return;
    _status = ControlState.Restart;
    Log.sys("Reconnecting...");
    
    await KillAll();
    await doAWait(3, 1000);
    // reconnect
    if (porter is null || !porter.IsOpen) {
      // TODO probably want to throw an error here
      // user client should trigger a stop-start at this point
      Log.critical("Port connection not open.");
      _status = ControlState.Error;
      return;
    }
    if (sendConnect()) {
      spawnTasks();
      _status = ControlState.Running;
      if (UserStreaming) {
        q_user.Enqueue(OpCode.StartStream);
      }
    } else {
      //await doAWait();
      // TODO exception
      Log.critical("Failed connection.");
      _status = ControlState.Error;
    }
  }

  /// <summary>
  /// Send kill order to all running tasks and return to the
  /// starting state. Any controller objects should be stopped
  /// before exiting.
  /// </summary>
  /// <returns></returns>
  public async Task stop() {
    if (status == ControlState.Created) return;
    _status = ControlState.Stopping;
    await KillAll();
    if (porter is not null && porter.IsOpen) {
      porter.Close();
    }
    ClearQueues();
    _status = ControlState.Created;
  }
  /// <summary>
  /// Ensures a good state before sending stream control commands.
  /// </summary>
  /// <param name="streaming"></param>
  /// <returns></returns>
  private bool StreamCheck(bool streaming) {
    if (status != ControlState.Running) {
      Log.critical("Cannot execute; Controller not running.");
      return false;
    }
    if (PriorityState() > TaskEngine.TaskState.Running) {
      Log.critical("Cannot execute; Tasks are recovering.");
      return false;
    }
    // ya know the arduino is not reliable
    // if (streaming && IsStreaming) {
    //   Log.critical("Cannot execute; already streaming.");
    //   return false;
    // }
    // if (!(streaming || IsStreaming)) {
    //   Log.critical("Cannot execute; Stream already stopped.");
    //   return false;
    // }
    return true;
  }
  /// <summary>
  /// Attempts to send the start stream command. If the controller
  /// is not in the Running state, the command will not be sent.
  /// </summary>
  /// <returns></returns>
  public void startStreaming() {
    if (!StreamCheck(true)) return;
    _UserStreaming = true;
    q_user.Enqueue(OpCode.StartStream);
    Log.sys("Start stream command sent.");
  }
  /// <summary>
  /// Attempts to send the stop streaming command. If the controller
  /// is not in the Running state the command will not be sent.
  /// </summary>
  /// <returns></returns>
  public void stopStreaming() {
    if (!StreamCheck(false)) return;
    _UserStreaming = false;
    q_user.Enqueue(OpCode.StopStream);
    Log.sys("Stop stream command sent.");
  }
  /// <summary>
  /// Kills any running tasks, closes the port and log if open.
  /// </summary>
  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  protected virtual async void Dispose(bool disposing) {
    if (!disposed) {
      if (disposing) {
        _IsStreaming = false;
        FileLog.close();
        await KillAll();
        if (porter is not null && porter.IsOpen) {
          porter.Close();
        }
      }
      disposed = true;
    }
  }
  ~Controller() {
    Dispose(false);
  }
}