using System.IO.Ports;

namespace NeurCLib;

/// <summary>
/// Base class for pinging the arduino. Will probably move things later.
/// </summary>
public class Copper {
  /// <summary>
  /// Tracks the state the Copper is in.
  /// </summary>
  public enum State {
    Closed,
    Connecting,
    Running,
    Error
  }
  /// <summary>
  /// Timer that triggers the tick function.
  /// </summary>
  private System.Timers.Timer ticker;
  /// <summary>
  /// State that the Copper is in. Used to determine what to
  /// do during tick.
  /// </summary>
  private State _state = State.Closed;
  public State state {
    get => _state;
  }
  /// <summary>
  /// The serial port object. Using a wrapper for trading out with
  /// a test object.
  /// </summary>
  private IPorter porter;
  public Copper(Boolean debug=false) {
    if (debug) {
      porter = new PseudoPorter();
    } else {
      porter = new PortWrapper();
    }
    porter.PortName = "COM3";
    porter.BaudRate = 115200;
    porter.DataBits = 8;
    porter.Parity = Parity.None;
    porter.StopBits = StopBits.One;
    porter.ReadTimeout = 2000;
    porter.WriteTimeout = 1000;

    ticker = new System.Timers.Timer(4000);
    ticker.Elapsed += tick;
    ticker.AutoReset = false;
  }
  /// <summary>
  /// Start the ticker.
  /// </summary>
  public void start() {
    ticker.Start();
    Log.sys("Ticker started");
  }
  /// <summary>
  /// Controlled stop, closes the connection.
  /// </summary>
  public void stop() {
    ticker.Stop();
    porter.Close();
    Log.sys("Ticker stopped");
  }
  /// <summary>
  /// Executes at each timeout of the ticker.
  /// If the porter is not open, open the porter.
  /// If the porter is open, but the state is not connected, connect to arduino.
  /// If the state is connected, send keepalive.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void tick(object? sender, System.Timers.ElapsedEventArgs e) {
    Log.debug("Tick start");
    Package p = new Package();
    if (!porter.IsOpen) {
      // connect to arduino and wait for next tick
      porter.Open();
      _state = State.Connecting;
      ticker.Start();
      Log.debug($"Connection opening: {state.ToString()}");
      Log.sys("Opening port " + porter.PortName);
      return;
    } else if (state == State.Connecting) {
      // port is open so send initial packet
      Log.sys("Port open, sending connection request.");
      p.initial();
    } else if (state == State.Running) {
      // send keepalive
      Log.sys("Connection open, sending keepalive.");
      p.keepalive();
    } else {
      // I did something weird
      Log.critical($"Unexpected state '{state.ToString()}' exiting.");
      _state = State.Error;
      return;
    }
    //Log.debug("Payload size: " + p.length().ToString());
    // send package and listen for response
    porter.Write(p.toStream(), 0, p.length());
    byte[] ray = new byte[p.length()];
    int n = 0;
    try {
      for (int offset = 0; offset < p.length();) {
        n = porter.Read(ray, offset, ray.Length - offset);
        Log.debug($"Bytes read={n} <=> ray size={ray.Length}");
        offset += n;
      }
    }
    catch (TimeoutException) {
      // response not received; reset to connect
      _state = State.Connecting;
      ticker.Start();
      Log.warn("Port connection Timeout: " + porter.ReadTimeout.ToString());
      return;
    } catch (ArgumentException e2) {
      // I did something wrong...probably
      _state = State.Error;
      Log.critical("{0}: {1}", e2.GetType().Name, e2.Message);
      return;
    }
    if (p.isEqual(ray)) {
      // request success
      Log.sys("Still Alive");
      _state = State.Running;
    } else {
      // received error
      handleError(ray);
      Log.debug("Original:", p.toStream());
      Log.debug("Error ray:", ray);
      _state = State.Error;
    }
    // if we aren't in error and we haven't restarted, restart
    if (state != State.Error) {
      ticker.Start();
    }
    Log.debug($"End tick: {state.ToString()}");
  }
  /// <summary>
  /// Handle an error thrown by the arduino. Reads the error
  /// and prints an error message.
  /// </summary>
  /// <param name="ray">Output from arduino</param>
  private void handleError(byte[] ray) {
    Package p = new Package();
    p.fromStream(ray);
    if (p.payload.Length == 0) {
      Log.critical("Error occured: 0 length response");
      return;
    }
    ErrorType errorType = (ErrorType) p.payload[0];
    String report = "Error occured: ";
    report += errorType switch {
      ErrorType.BadChecksum => "Bad checksum: " + ((int)p.checksum).ToString(),
      ErrorType.TooLong => "Payload is too long: " + ((int)p.payloadSize).ToString() + " <> " + p.payload.Length.ToString(),
      _ => throw new ArgumentOutOfRangeException(errorType.ToString(), $"Unexpected error: {errorType.ToString()}")
    };
    Log.critical(report);
  }
}
