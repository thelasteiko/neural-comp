
using System;
using System.Text;

namespace NeurCLib;
#region enums
/// <summary>
/// List of packet types.
/// <list type="bullet">
///   <item>
///     <term>Failure</term>
///     <description>Failure/error packet</description>
///   <item>
///   <item>
///     <term>Transaction</term>
///     <description>Control and keepalive</description>
///   <item>
///   <item>
///     <term>Stream</term>
///     <description>Streamed data</description>
///   <item>
///   <item>
///     <term>Unknown</term>
///     <description>Default value</description>
///   <item>
/// </list>
/// </summary>
public enum PackType {
  Failure = 0,
  Transaction,
  Stream,
  Unknown
}
/// <summary>
/// Types of errors from the arduino, sent as the payload
/// if the packet type is Failure.
/// <list type="bullet">
///   <item>
///     <term>BadChcksum</term>
///     <description>Sent a bad checksum value</description>
///   <item>
///   <item>
///     <term>TooLong</term>
///     <description>Payload length exceeds payload size</description>
///   <item>
///   <item>
///     <term>BadPackType</term>
///     <description>Packet type not correct</description>
///   <item>
///   <item>
///     <term>BadOpCode</term>
///     <description>Payload does not match packet type</description>
///   <item>
///   <item>
///     <term>AlreadyConnected</term>
///     <description>Already connected, connection request sent more than once</description>
///   <item>
///   <item>
///     <term>AlreadyStreaming</term>
///     <description>Stream already started, start streaming sent more than once</description>
///   <item>
///   <item>
///     <term>AlreadyStopped</term>
///     <description>Stream already stopped, stop streaming sent more than once</description>
///   <item>
///   <item>
///     <term>NotConnected</term>
///     <description>Cannot respond to control commands because we are not connected</description>
///   <item>
///   <item>
///     <term>Unknown</term>
///     <description>Default value</description>
///   <item>
/// </list>
/// </summary>
public enum ErrorType {
  BadChecksum = 0,
  TooLong,
  BadPackType,
  BadOpCode,
  AlreadyConnected,
  AlreadyStreaming,
  AlreadyStopped,
  NotConnected,
  AlreadyTherapy,
  AlreadyNotTherapy,
  Unknown
}
/// <summary>
/// Control operations for manipulating arduino.
/// <list type="bullet">
///   <item>
///     <term>Initial</term>
///     <description>Initialize connection</description>
///   <item>
///   <item>
///     <term>Keepalive</term>
///     <description>Reset watchdog</description>
///   <item>
///   <item>
///     <term>StartStream</term>
///     <description>Start streaming data</description>
///   <item>
///   <item>
///     <term>StopStream</term>
///     <description>Stop streaming data</description>
///   <item>
///   <item>
///     <term>Unknown</term>
///     <description>Default value</description>
///   <item>
/// </list>
/// </summary>
public enum OpCode : byte {
  Initial = 0x01,
  Keepalive = 0x02,
  StartStream = 0x03,
  StopStream = 0x04,
  StartStim = 0x05,
  StopStim = 0x06,
  Unknown
}
#endregion
/// <summary>
/// Definition for the packet data and associated functions.
/// Provides utility functions to manage and print info.
/// </summary>
public class Package {
  /// <summary>
  /// Minimum possible size, if the payload length is 0.
  /// </summary>
  public const int MIN_SIZE = 7;
  /// <summary>
  /// I got this from the arduino code
  /// </summary>
  public const int MAX_PAYLOAD_SIZE = 249;
  /// <summary>
  /// The header expected by the arduino. I would make this constant,
  /// but an array is more convenient.
  /// </summary>
  /// <value></value>
  internal static byte[] Header = new byte[] {0xAA, 0x01, 0x02};
  /// <summary>
  /// Header, usually 3 bytes, that indicates the beginning of a packet.
  /// </summary>
  public byte[] headerSync;
  /// <summary>
  /// One of <see cref="PackType"/>
  /// </summary>
  public byte packetType;
  /// <summary>
  /// Packet Id that uniquely identifies the packet sent.
  /// </summary>
  public byte packetID;
  /// <summary>
  /// Size of the payload byte array
  /// </summary>
  public byte payloadSize;
  /// <summary>
  /// Payload! Could be stream data or <see cref="OpCode"/>
  /// </summary>
  public byte[] payload;
  /// <summary>
  /// Checksum is the sum of all bytes, header to payload, except the checksum.
  /// </summary>
  public byte checksum = 0;
  public OpCode opCode {
    get => (OpCode) payload[0];
  }
  /// <summary>
  /// Create a blank packet, normally used for building one read from
  /// the serial port.
  /// </summary>
  public Package() {
    headerSync = new byte[3];
    packetType = (byte)PackType.Unknown;
    packetID = 0;
    payloadSize = 0;
    payload = new byte[0];
  }
  /// <summary>
  /// Create a packet to be sent through the serial port. The packet Id
  /// will be incremented to keep each packet sent unique.
  /// </summary>
  /// <param name="packType">One of <see cref="PackType"/></param>
  /// <param name="opc">One of <see cref="OpCode"/></param>
  public Package(PackType packType, OpCode opc=OpCode.Unknown) {
    this.headerSync = new byte[] {0xAA, 0x01, 0x02};
    //this.headerSync = new byte[3];
    this.packetType = (byte)packType;
    this.packetID = (byte)packageIndex++;
    int size = 0;
    size = packType switch {
      PackType.Failure => 1,
      PackType.Transaction => 1,
      PackType.Stream => 6,
      _ => 1
    };
    this.payloadSize = (byte)size;
    this.payload = new byte[payloadSize];
    if (opc != OpCode.Unknown) {
      payload[0] = (byte)opc;
      checkMe();
    }
  }
  /// <summary>
  /// Print some values for debugging.
  /// </summary>
  /// <returns></returns>
  public override string ToString() {
    StringBuilder sb = new StringBuilder("[");
    sb.Append("headerSync=" + headerSync.Length);
    sb.Append(", packetType=" + ((PackType) packetType).ToString());
    sb.Append(", packetID=" + packetID.ToString());
    sb.Append(", payloadSize=" + payloadSize.ToString());
    sb.Append(", payload=" + payload.Length);
    sb.Append(", checksum=" + checksum.ToString());
    sb.Append("]");
    return sb.ToString();
  }
  /// <summary>
  /// Concatenate the package into a byte array.
  /// </summary>
  /// <returns>Byte array ready for the serial port</returns>
  public byte[] toStream() {
    byte[] ray = new byte[this.length];
    headerSync.CopyTo(ray, 0);
    ray[3] = packetType;
    ray[4] = packetID;
    ray[5] = payloadSize;
    payload.CopyTo(ray, 6);
    ray[6+payloadSize] = checksum;
    return ray;
  }
  /// <summary>
  /// Read in a byte array and set values based on it.
  /// If the byte array length is less than the MIN_SIZE, no values are set.
  /// </summary>
  /// <param name="ray">Should be the response packet from the arduino</param>
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
    //Log.debug("Package to stream:", ray);
    byte sum = 0;
    for (int i = 0; i < ray.Length; i++) {
      sum += ray[i];
    }
    this.checksum = sum;
    // Log.debug("Checksum=" + checksum.ToString("X2"));
  }
  /// <summary>
  /// Number of bytes in the packet.
  /// </summary>
  /// <value></value>
  public int length {
    get => MIN_SIZE + payloadSize;
  }
  /// <summary>
  /// Sets the indicated byte. Used in conjuction with the
  /// <see cref="PackFactory"/> to create a valid packet.
  /// 
  /// Meant for building the packet in sequence.
  /// </summary>
  /// <param name="i">Index of the byte to set</param>
  /// <param name="b">Value to set</param>
  /// <returns></returns>
  public bool setByte(int i, byte b) {
    //Log.debug($"Set {i} => {b.ToString("X2")}");
    switch(i) {
      case < 3:
        if (headerSync[i] != 0) return false;
        if (b != Header[i]) return false;
        headerSync[i] = b;
        break;
      case 3:
        packetType = b;
        break;
      case 4:
        packetID = b;
        break;
      case 5:
        payloadSize = b;
        payload = new byte[b];
        // Log.debug("Payload size = " + payload.Length);
        break;
      case > 5:
        if (i <= (5+payloadSize)) {
          // Log.debug($"Set {i-5} to {b.ToString("X2")}");
          payload[i-6] = b;
        } else if (i == (5+payloadSize+1)) {
          checksum = b;
        } else {return false;}
        break;
    }
    return true;
  }
  /// <summary>
  /// Checks if the values in the byte array are the same as the object.
  /// Used for checking against the response packet.
  /// </summary>
  /// <param name="ray"></param>
  /// <returns></returns>
  public bool isEqual(byte[] ray) {
    if (this.length > ray.Length) return false;
    byte[] meray = this.toStream();
    int min = Math.Min(meray.Length, ray.Length);
    for (int i = 0; i < min; i++) {
      if (meray[i] != ray[i]) return false;
    }
    return true;
  }
  /// <summary>
  /// Checks if the checksum is valid.
  /// </summary>
  /// <returns></returns>
  public bool isValid() {
    byte check = checksum;
    if (check == 0) return false;
    checkMe();
    if (check == checksum) return true;
    checksum = check;
    return false;
  }
  public bool isTransaction() {
    return ((PackType) packetType) == PackType.Transaction;
  }
  public bool isCommand() {
    return (isTransaction() && ((OpCode) payload[0]).In(
      OpCode.StartStim, OpCode.StopStim, OpCode.StartStream, OpCode.StopStream));
  }
  public bool isStream() {
    return ((PackType) packetType) == PackType.Stream;
  }
  public bool isFailure() {
    return ((PackType) packetType) == PackType.Failure;
  }
  public bool isKeepalive() {
    return isTransaction() && ((OpCode) payload[0] == OpCode.Keepalive);
  }
  /// <summary>
  /// Increments for each packet sent to provide a unique ID for each.
  /// </summary>
  static int packageIndex = 0;
  
}
/// <summary>
/// Builds a package one byte at a time, verifying that it is
/// being built correctly. The factory will reset if the packet
/// needs to be restarted from the header for a limited number
/// of resets.
/// </summary>
public class PackFactory {
  private Package _pack = new();
  /// <summary>
  /// <see cref="Package"/> being built.
  /// </summary>
  /// <value></value>
  public Package pack {
    get => _pack;
  }
  private int current_byte;
  private bool reset = true;
  private int reset_count = 0;
  private int reset_timeout = 50;
  /// <summary>
  /// The packet is ready when the checksum is set.
  /// </summary>
  /// <value></value>
  public bool IsReady {
    get => (_pack != null && _pack.checksum != 0);
  }
  /// <summary>
  /// The factory has failed if it has had to reset to the header
  /// greater than the reset_timeout times, and the packet is still
  /// not ready.
  /// </summary>
  /// <value></value>
  public bool IsFailed {
    get => (reset_count >= reset_timeout && !IsReady);
  }
  /// <summary>
  /// Creates the factory and sets the timeout for how many times
  /// it can reset to the header without failing.
  /// </summary>
  /// <param name="timeout">Defautl is 50</param>
  public PackFactory(int timeout=50) {
    reset_timeout = timeout;
  }
  /// <summary>
  /// Adds a byte to the packet being built. If the byte is rejected,
  /// the packet will be restarted on the next build.
  /// </summary>
  /// <param name="b"></param>
  /// <returns><see cref="Package"> being built</returns>
  public Package build(byte b) {
    // Log.debug("Read byte: " + b.ToString("X2"));
    if (reset) {
      _pack = new();
      current_byte = 0;
      reset = false;
      reset_count++;
      // Log.debug("Reset packet factory.");
    }
    // Log.debug($"Set {current_byte} => {b.ToString("X2")}");
    // all headers are set, we are synced
    if (!_pack.setByte(current_byte, b)) {
      // Log.debug($"Could not set {current_byte}={b.ToString("X2")}: ", pack.toStream());
      reset = true;
    }
    current_byte += 1;
    // Log.debug("Current byte is " + current_byte.ToString());
    return _pack;
  }
}
