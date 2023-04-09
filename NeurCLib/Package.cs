
using System.Text;
namespace NeurCLib {
  // baudrate / 10 = byte rate
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
  public class Package {
    public const int MIN_SIZE = 7;
    public const int MAX_PAYLOAD_SIZE = 249;
    public byte[] headerSync;
    public byte packetType;
    public byte packetID;
    public byte payloadSize;
    public byte[] payload = new byte[1];
    public byte checksum = 0;
    public Package(int size=1) {
      this.headerSync = new byte[] {0xAA, 0x01, 0x02};
      this.packetType = (byte)PackType.Transaction;
      this.packetID = (byte)packageIndex++;
      this.payloadSize = (byte)size;
      this.payload = new byte[payloadSize];
    }
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
    /// <returns></returns>
    public byte[] toStream() {
      byte[] ray = new byte[this.length()];
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
      //Log.debug("Package to stream:", ray);
      byte sum = 0;
      for (int i = 0; i < ray.Length; i++) {
        sum += ray[i];
      }
      this.checksum = sum;
      // Log.debug("Checksum=" + checksum.ToString("X2"));
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
      this.payload[0] = 0x01;
      this.checkMe();
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
}