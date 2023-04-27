
using System.Collections.Concurrent;
using System.Collections;
using System.IO.Ports;
namespace NeurCLib;

public class Controller {
  public enum ControlState {
    Created,
    Opened,
    Connected,
    Running,
    Restart,
    Error
  }
  internal System.Threading.Mutex write_mutex = new();
  internal IPorter porter;
  public void Write(byte[] buffer, int length) {
    lock (write_mutex) {
      porter.Write(buffer, 0, length);
    }
  }
  internal ConcurrentQueue<Package> que = new();

  private ControlState _status;
  public ControlState status {get => _status;}
  /// <summary>
  /// How long to wait for existing threads to die before
  /// attempting to restart them. This is the maximum wait time.
  /// </summary>
  public int KillTimeout = 1000;
  private bool IsStreaming = false;
  public Controller(bool debug=false) {
    if (debug) {
      porter = new PseudoPorter();
    } else {
      porter = new PortWrapper();
    }
    _status = ControlState.Created;
  }
  public delegate void OnCommand(Object sender, StreamEventArgs e);
  public event EventHandler<StreamEventArgs>? OnStream;
  
  public void start() {
    if (porter.IsOpen) porter.Close();
    if (connect()) {
      _status = ControlState.Opened;
      spawnTasks();
    }
  }
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
    _status = ControlState.Running;
  }
  private bool connect() {
    // get list of connections
    string[] ports = SerialPort.GetPortNames();
    for (int i = 0; i < ports.Length; i++) {
      // for each, try open
      porter.PortName = ports[i];
      _status = ControlState.Created;
      try {
        porter.Open();
        porter.DiscardInBuffer();
        porter.DiscardOutBuffer();
      } catch (System.Exception) {
        Log.warn($"Failed to open '{ports[i]}'");
        continue;
      }
      _status = ControlState.Opened;
      if (sendConnect()) return true;
    }
    return false;
  }
  private bool sendConnect() {
    // if open, try connect; else, try next
    Package p = new(PackType.Transaction);
    p.initial();
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
  /// <summary>
  /// Kills the current threads and attempts to reconnect to the arduino.
  /// </summary>
  /// <param name="tsk"></param>
  /// <returns></returns>
  private async void reconnect(Task tsk) {
    _status = ControlState.Restart;
    // kill all the things
    TaskEngine.KillAll();
    await Task.Factory.StartNew(() => {
      int counter = 0;
      while (TaskEngine.IsAlive() && counter < KillTimeout) {
        counter++;
        Thread.Sleep(1);
      }
      if (TaskEngine.IsAlive()) Log.critical("Old tasks still running.");
    });
    // reconnect
    if (porter is null || !porter.IsOpen) {
      // TODO probably want to throw an error here
      Log.critical("Port connection not open., attempting restart.");
      _status = ControlState.Error;
      start();
      return;
    }
    if (sendConnect()) {
      spawnTasks();
      if (IsStreaming) {
        await Task.Factory.StartNew(() => new Commander(this, OpCode.StartStream).Run());
      }
    } else {
      // TODO exception
      Log.critical("Failed connection.");
      _status = ControlState.Error;
    }
  }

  public void handleOnStream(StreamPacket sp) {
    StreamEventArgs args = new(sp);
    OnStream?.Invoke(this, args);
  }
}