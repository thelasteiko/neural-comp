
using System;
using System.Text;
using System.Buffers.Binary;
namespace NeurCLib;
  
public enum PackType {
  Failure = 0,
  Transaction,
  Stream,
  Unknown
}
public enum ErrorType {
  BadChecksum = 0,
  TooLong,
  BadPackType,
  BadOpCode,
  AlreadyConnected,
  AlreadyStreaming,
  AlreadyStopped,
  NotConnected,
  Unknown
}
public enum OpCode : byte {
  Initial = 0x01,
  Keepalive = 0x02,
  StartStream = 0x03,
  StopStream = 0x04,
  Unknown
}
public static class Ext
{
    public static bool In<T>(this T val, params T[] values) where T : struct
    {
        return values.Contains(val);
    }
}
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
  public static byte[] Header = new byte[] {0xAA, 0x01, 0x02};
  public byte[] headerSync;
  public byte packetType;
  public byte packetID;
  public byte payloadSize;
  public byte[] payload;
  public byte checksum = 0;
  public static bool IsPackType(byte b) {
    return ((PackType) b).In(PackType.Failure, PackType.Stream, PackType.Transaction);
  }
  
  public Package() {
    headerSync = new byte[3];
    packetType = (byte)PackType.Unknown;
    packetID = 0;
    payloadSize = 0;
    payload = new byte[0];
  }
  public Package(PackType packType, OpCode opc=OpCode.Unknown) {
    //this.headerSync = new byte[] {0xAA, 0x01, 0x02};
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
  public int length {
    get => MIN_SIZE + payloadSize;
  }
  public void setByte(int i, byte b) {
    byte[] ray = toStream();
    if (i > ray.Length) {
      byte[] ray2 = new byte[ray.Length+1];
      for (int j = 0; j < ray.Length; j++) {
        ray2[j] = ray[j];
      }
      ray2[i] = b;
      fromStream(ray2);
    } else {
      ray[i] = b;
      fromStream(ray);
    }
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
  public bool isSynced() {
    if (headerSync.Length < 3) return false;
    for (int i = 0; i < 3; i++) {
      if (headerSync[i] != Header[i]) return false;
    }
    return true;
  }
  public bool isValid() {
    byte check = checksum;
    if (check == 0) return false;
    checkMe();
    if (check == checksum) return true;
    checksum = check;
    return false;
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
    this.payload = new byte[]{ (byte)OpCode.Initial};
    payloadSize = 1;
    this.packetType = (byte)PackType.Transaction;
    this.checkMe();
  }
  /// <summary>
  /// Set Package to reset watchdog.
  /// </summary>
  /// <returns></returns>
  public void keepalive() {
    payload = new byte[]{(byte)OpCode.Keepalive};
    payloadSize = 1;
    this.packetType = (byte)PackType.Transaction;
    checkMe();
  }
  public void start() {
    payload = new byte[]{(byte)OpCode.StartStream};
    payloadSize = 1;
    this.packetType = (byte)PackType.Transaction;
    checkMe();
  }
  public void stop() {
    payload = new byte[]{(byte)OpCode.StopStream};
    payloadSize = 1;
    this.packetType = (byte)PackType.Transaction;
    checkMe();
  }
  public static Boolean IsTransaction(byte[] buffer) {
    if (buffer.Length < MIN_SIZE) return false;
    if (buffer[3] != ((byte)PackType.Transaction)) return false;
    return true;
  }
  public static Boolean IsInitial(byte[] buffer) {
    if (IsTransaction(buffer) && buffer[6] == (byte)OpCode.Initial) return true;
    return false;
  }
  public static Boolean IsKeepalive(byte[] buffer) {
    if (IsTransaction(buffer) && buffer[6] == (byte)OpCode.Keepalive) return true;
    return false;
  }
}
/// <summary>
/// Build-a-package
/// </summary>
public class PackFactory {
  private Package _pack = new();
  public Package pack {
    get => _pack;
  }
  private int current_byte;
  private bool reset = true;
  private int reset_count = 0;
  private int reset_timeout = 50;
  public bool IsReady {
    get => (_pack != null && _pack.isValid());
  }
  public bool IsFailed {
    get => (reset_count >= reset_timeout && !IsReady);
  }
  public PackFactory() {}
  public PackFactory(int timeout) {
    reset_timeout = timeout;
  }
  
  public Package build(byte b) {
    if (reset) {
      _pack = new();
      current_byte = 0;
    }
    // TODO lol
    for (int i = current_byte; i < 3; i++) {
      // check each header byte to sync
      if (_pack.headerSync[i] == 0) {
        if (b == Package.Header[i]) {
          // found the correct header value
          _pack.headerSync[i] = b;
          current_byte++;
        } else {
          // if we hit 0 but the byte doesn't match, reset
          reset = true;
          reset_count++;
        }
        return _pack;
      }
    }
    // all headers are set, we are synced
    _pack.setByte(current_byte++, b);
    return _pack;
  }
}

public class StreamPacket {
  public ulong timestamp {get;}
  public ushort microvolts {get;}
  public StreamPacket(byte[] data) {
    // first 4 are timestamp
    timestamp = BinaryPrimitives.ReadUInt64LittleEndian(data);
    // last 2 is neural data
    microvolts = BinaryPrimitives.ReadUInt16LittleEndian(data.Skip(4).ToArray());
  }
}