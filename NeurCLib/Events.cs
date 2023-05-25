using System.Reflection.Metadata;
using System.Buffers.Binary;

namespace NeurCLib;
/// <summary>
/// Arguments for streamed data.
/// </summary>
public class StreamEventArgs : EventArgs {
  public const double DYNAMIC_RANGE = 3932.0;
  public const double X_MIN = -1885.0032958984373;
  /// <summary>
  /// Represents the timestamp part of the data
  /// </summary>
  public ulong timestamp;
  public ushort raw;
  /// <summary>
  /// Represents the data part
  /// </summary>
  public double microvolts;
  public StreamEventArgs(byte[] payload) {
    // first 4 are timestamp
    timestamp = BinaryPrimitives.ReadUInt32LittleEndian(payload);
    // last 2 is neural data
    raw = BinaryPrimitives.ReadUInt16LittleEndian(payload.Skip(4).ToArray());
    // convert raw data to uV
    microvolts = raw / 65536.0 * DYNAMIC_RANGE + X_MIN;
  }
}

/// <summary>
/// Helper extensions
/// </summary>
internal static class Ext {
  /// <summary>
  /// Quick lookup for enums
  /// </summary>
  public static bool In<T>(this T val, params T[] values) where T : struct {
      return values.Contains(val);
  }
}
