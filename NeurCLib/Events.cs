using System.Buffers.Binary;

namespace NeurCLib;
/// <summary>
/// Arguments for streamed data.
/// </summary>
public class StreamEventArgs : EventArgs {
  /// <summary>
  /// Represents the timestamp part of the data
  /// </summary>
  public ulong timestamp;
  /// <summary>
  /// Represents the data part
  /// </summary>
  public ushort microvolts;
  public StreamEventArgs(byte[] payload) {
    // first 4 are timestamp
    timestamp = BinaryPrimitives.ReadUInt32LittleEndian(payload);
    // last 2 is neural data
    microvolts = BinaryPrimitives.ReadUInt16LittleEndian(payload.Skip(4).ToArray());
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
