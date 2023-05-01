
using System.Collections.Concurrent;
using System.Collections;
using System.IO.Ports;
namespace NeurCLib;
/// <summary>
/// Main class that works as the interface for all functionality.
/// </summary>
public class Controller : IDisposable {
  /// <summary>
  /// Describes state of the controller object.
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
  internal const int MAX_TIMEOUT = 5000;
  internal System.Threading.Mutex write_mutex = new();
  internal IPorter porter;
  /// <summary>
  /// Write directly to the serial port, if you really want to.
  /// Does nothing if the port isn't open.
  /// </summary>
  /// <param name="buffer"></param>
  /// <param name="length"></param>
  public void Write(byte[] buffer, int length) {
    if (!porter.IsOpen) return;
    lock (write_mutex) {
      porter.Write(buffer, 0, length);
    }
  }
  /// <summary>
  /// Queue for packages from the arduino to be processed.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentQueue<Package> que = new();
  /// <summary>
  /// Holds the currently running tasks.
  /// </summary>
  /// <returns></returns>
  internal ConcurrentDictionary<String, TaskEngine> task_bag = new();
  /// <summary>
  /// Tries to find the named task. If the task exists, sets tsk equal to it.
  /// </summary>
  /// <param name="name"></param>
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
  internal bool IsAlive() {
    return task_bag.Count > 0;
  }
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
  public int KillTimeout = 1000;
  internal System.Threading.Mutex stream_lock = new();
  private bool __IsStreaming;
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
  private bool disposed = false;
  public Controller(bool debug=false) {
    if (debug) {
      porter = new PseudoPorter();
    } else {
      porter = new PortWrapper();
    }
    porter.BaudRate = 115200;
    porter.DataBits = 8;
    porter.StopBits = StopBits.One;
    porter.Parity = Parity.None;
    porter.WriteTimeout = 500;
    porter.ReadTimeout = MAX_TIMEOUT;

    _status = ControlState.Created;
  }
  public event EventHandler<StreamEventArgs>? Stream;
  /// <summary>
  /// Opens and connects to the arduino, then starts the keepalive,
  /// listening, and consumming threads.
  /// </summary>
  public void start() {
    if (porter.IsOpen) porter.Close();
    if (connect()) {
      _status = ControlState.Opened;
      Log.debug("Connection successful, starting subtasks.");
      spawnTasks();
      _status = ControlState.Running;
    }
  }
  /// <summary>
  /// Creates independent threads for each task.
  /// </summary>
  private void spawnTasks() {
    // clear que first
    que.Clear();
    Task.Factory.ContinueWhenAny(new Task[] {
        Task.Factory.StartNew(() => new Listener(this).Run()),
        Task.Factory.StartNew(() => new Consumer(this).Run()),
        Task.Factory.StartNew(() => new Keepalive(this).Run())
      },
      (task) => reconnect(task)
    );
    Log.debug("Subtasks started.");
  }
  /// <summary>
  /// Sets up the initial connection by searching for an available
  /// port and then attempting to send the initial connect request.
  /// Continues until all ports are exhausted.
  /// </summary>
  /// <returns></returns>
  private bool connect() {
    Log.debug("Entered connect");
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
        continue;
      }
      Log.debug("Connected to " + ports[i]);
      _status = ControlState.Opened;
      if (sendConnect()) return true;
    }
    return false;
  }
  /// <summary>
  /// Helper for sending the initial connect request.
  /// Assumes the port is open.
  /// </summary>
  /// <returns></returns>
  private bool sendConnect() {
    Package p = new(PackType.Transaction);
    p.initial();
    Log.debug("Sending intial connect");
    porter.Write(p.toStream(), 0, p.length);
    // if connect, continue; else, try next
    byte[] buffer = new byte[p.length];
    int n = 0;
    try {
      for (int offset = 0; offset < p.length;) {
        n = porter.Read(buffer, offset, buffer.Length - offset);
        Log.debug($"Bytes read={n} <=> ray size={buffer.Length}");
        offset += n;
      }
    } catch (TimeoutException) {
      Log.warn("Port connection timeout");
      return false;
    } catch (Exception e2) {
      Log.critical("{0}: {1}", e2.GetType().Name, e2.Message);
      return false;
    }
    
    if (p.isEqual(buffer)) {
      return true;
    } else {
      Log.warn($"Could not connect to '{porter.PortName}'");
    }
    return false;
  }
  private async Task KillAll() {
    SendKill();
    await Task.Factory.StartNew(() => {
      int counter = 0;
      while (IsAlive() && counter < KillTimeout) {
        counter++;
        Thread.Sleep(1);
      }
      if (IsAlive())
        Log.critical("Old tasks still running. Queue: " + que.Count);
    });
  }
  /// <summary>
  /// Kills the current threads and attempts to reconnect to the arduino.
  /// </summary>
  /// <param name="tsk"></param>
  /// <returns></returns>
  private async void reconnect(Task tsk) {
    _status = ControlState.Restart;
    await KillAll();
    // reconnect
    if (porter is null || !porter.IsOpen) {
      // TODO probably want to throw an error here
      Log.critical("Port connection not open., attempting restart.");
      _status = ControlState.Error;
      start();
      return;
    }
    Log.sys("Waiting for component reset.");
    await doAWait();
    if (sendConnect()) {
      spawnTasks();
      if (IsStreaming) {
        await Task.Factory.StartNew(() => new Commander(this, OpCode.StartStream).Run());
      }
      _status = ControlState.Running;
    } else {
      //await doAWait();
      // TODO exception
      Log.critical("Failed connection.");
      _status = ControlState.Error;
    }
  }

  internal async Task doAWait() {
    await Task.Factory.StartNew(() => {
      for (int i = 0; i < 5; i++){
        Thread.Sleep(1000);
        Console.Write(".. ");
      }
      Console.WriteLine();
    });
  }

  internal void handleOnStream(StreamEventArgs args) {
    Stream?.AsyncInvoke(this, args);
  }
  /// <summary>
  /// Send kill order to all running tasks and return to the
  /// starting state. Any controller objects should be stopped
  /// before exiting.
  /// </summary>
  /// <returns></returns>
  public async Task stop() {
    _status = ControlState.Stopping;
    await KillAll();
    if (porter is not null && porter.IsOpen) {
      porter.Close();
    }
    que.Clear();
    _status = ControlState.Created;
  }
  public async Task startStream() {
    if (status != ControlState.Running) {
      Log.critical("Controller not running.");
      return;
    }
    if (IsStreaming) {
      Log.critical("Stream command already sent.");
      return;
    }
    await Task.Factory.StartNew(() => new Commander(this, OpCode.StartStream));
    Log.sys("Stream started.");
  }
  public async Task stopStreaming() {
    if (status != ControlState.Running) {
      Log.critical("Controller not running.");
      return;
    }
    if (!IsStreaming) {
      Log.critical("Stream already stopped.");
      return;
    }
    await Task.Factory.StartNew(() => new Commander(this, OpCode.StopStream));
    Log.sys("Stream stopped.");
  }
  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  protected virtual void Dispose(bool disposing) {
    if (!disposed) {
      if (disposing) {
        SendKill();
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