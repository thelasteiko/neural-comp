using System.IO.Ports;

namespace NeurCLib;

enum PackType {
  Failure = 0,
  Transaction,
  Stream
}
enum ErrorType {
  BadChecksum = 0,
  TooLong
}

/// <summary>
/// Definition for the packet data and associated functions.
/// Provides standard factory functions for each type of packet.
/// </summary>
public struct Package {
  public const int MIN_SIZE = 7;
  public const int MAX_PAYLOAD_SIZE = 249;
  public byte[] headerSync;
  public byte packetType;
  public byte packetID;
  public byte payloadSize;
  public byte[] payload = new byte[1];
  public byte checksum = 0;
  public Package(int size=1) {
    headerSync = new byte[] {0xAA, 0x01, 0x02};
    packetType = (byte)PackType.Transaction;
    packetID = (byte)packageIndex++;
    payloadSize = (byte)size;
    payload = new byte[payloadSize];
  }
  /// <summary>
  /// Concatenate the package into a byte array.
  /// </summary>
  /// <returns></returns>
  public byte[] toStream() {
    byte[] ray = new byte[this.length()];
    headerSync.CopyTo(ray, 0);
    ray[3] = packetType;
    ray[4] = packetID;
    ray[5] = payloadSize;
    payload.CopyTo(ray, 6);
    ray[5+payloadSize] = checksum;
    return ray;
  }
  /// <summary>
  /// Read in a byte array and set values based on it.
  /// If the byte array length is less than the MIN_SIZE, no values are set.
  /// </summary>
  /// <param name="ray"></param>
  public void fromStream(byte[] ray) {
    if (ray.Length < MIN_SIZE) return;
    headerSync = new byte[] {ray[0], ray[1], ray[2]};
    packetType = ray[3];
    packetID = ray[4];
    payloadSize = ray[5];
    payload = new byte[payloadSize];
    for (int i = 0; i < payloadSize; i++) {
      payload[i] = ray[i + 6];
    }
    checksum = ray[5+payloadSize];
  }
  /// <summary>
  /// Sets the checksum to the summation of all values except the checksum.
  /// </summary>
  public void checkMe() {
    this.checksum = 0;
    byte[] ray = this.toStream();
    byte sum = 0;
    for (int i = 0; i < ray.Length; i++) {
      sum += ray[i];
    }
    this.checksum = sum;
  }
  public int length() {
    return MIN_SIZE + payloadSize;
  }
  public bool isEqual(byte[] ray) {
    if (this.length() > ray.Length) return false;
    byte[] meray = this.toStream();
    int min = Math.Min(meray.Length, ray.Length);
    for (int i = 0; i < min; i++) {
      if (meray[i] != ray[i]) return false;
    }
    return true;
  }
  /// <summary>
  /// Increments for each packet sent to provide a unique ID for each.
  /// </summary>
  static int packageIndex = 0;
  /// <summary>
  /// Set Package for initial connection.
  /// </summary>
  /// <returns></returns>
  public void initial() {
    payload[0] = 0x01;
    checkMe();
  }
  /// <summary>
  /// Set Package to reset watchdog.
  /// </summary>
  /// <returns></returns>
  public void keepalive() {
    payload[0] = 0x02;
    checkMe();
  }
}
/// <summary>
/// Base class for pinging the arduino. Will probably move things later.
/// </summary>
public class Copper {
  public enum State {
    Closed,
    Connecting,
    Running,
    Stop,
    Error
  }
  private System.Timers.Timer ticker;
  private State _state = State.Closed;
  public State state {
    get => _state;
  }
  private SerialPort porter;
  public Copper() {
    porter = new SerialPort();
    porter.PortName = "COM3";
    porter.BaudRate = 115200;
    porter.DataBits = 8;
    porter.Parity = Parity.None;
    porter.StopBits = StopBits.One;
    porter.ReadTimeout = 1000;
    porter.WriteTimeout = 1000;

    ticker = new System.Timers.Timer(5000);
    ticker.Elapsed += tick;
    ticker.AutoReset = false;
  }
  /// <summary>
  /// Start the ticker.
  /// </summary>
  public void start() {
    ticker.Start();
    Console.WriteLine("Ticker started");
  }
  /// <summary>
  /// Controlled stop, closes the connection.
  /// </summary>
  public void stop() {
    _state = State.Stop;
    ticker.Stop();
    porter.Close();
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
    Console.WriteLine("tick");
    Package p = new Package();
    if (state == State.Stop) {
      porter.Close();
      return;
    }
    if (!porter.IsOpen) {
      // connect to arduino and wait for next tick
      porter.Open();
      _state = State.Connecting;
      ticker.Start();
      Console.WriteLine($"Connection opening: {state.ToString()}");
      return;
    } else if (state == State.Connecting) {
      // port is open so send initial packet
      Console.WriteLine("Initial");
      p.initial();
      Console.WriteLine("Initial");
    } else if (state == State.Running) {
      // send keepalive
      p.keepalive();
      Console.WriteLine("Keepalive");
    } else {
      // I did something weird
      Console.WriteLine($"Unexpected state '{state.ToString()}' exiting.");
      _state = State.Error;
      return;
    }
    // send package and listen for response
    porter.Write(p.toStream(), 0, p.length());
    byte[] ray = new byte[p.length()];
    try {
      porter.Read(ray, 0, ray.Length);
    }
    catch (TimeoutException) {
      // response not received; reset to connect
      _state = State.Connecting;
      ticker.Start();
      Console.WriteLine("Timeout");
      return;
    } catch (ArgumentException e2) {
      // I did something wrong...probably
      _state = State.Error;
      Console.WriteLine("{0}: {1}", e2.GetType().Name, e2.Message);
      return;
    }
    
    if (p.isEqual(ray)) {
      // request success
      _state = State.Running;
    } else {
      // received error
      handleError(ray);
      _state = State.Error;
    }
    if (state != State.Error) {
      ticker.Start();
    }
    Console.WriteLine($"end: {state.ToString()}");
  }
  private void handleError(byte[] ray) {
    Package p = new Package();
    p.fromStream(ray);
    if (p.payload.Length == 0) {
      Console.WriteLine("Error occured: 0 length response");
      return;
    }
    ErrorType errorType = (ErrorType) p.payload[0];
    String report = "Error occured: ";
    report += errorType switch {
      ErrorType.BadChecksum => "Bad checksum: " + ((int)p.checksum).ToString(),
      ErrorType.TooLong => "Payload is too long: " + ((int)p.payloadSize).ToString() + " <> " + p.payload.Length.ToString(),
      _ => throw new ArgumentOutOfRangeException(errorType.ToString(), $"Unexpected error: {errorType.ToString()}")
    };
    Console.WriteLine(report);
  }
}
