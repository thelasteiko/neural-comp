using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
namespace NeurCLib {
  /// <summary>
  /// Singleton class to standardize writes to console with timestamp.
  /// </summary>
  public sealed class Log {
    /// <summary>
    /// Determines how much text gets logged to the console.
    /// </summary>
    public enum Levels {
      Quiet,
      Critical,
      SysMsg,
      Warn,
      Debug
    }
    private DateTime start;
    private Levels LogLevel;
    
    private Log() {
      start = DateTime.Now;
    }
    public string timestamp() {
      TimeSpan interval = DateTime.Now - start;
      // string padded by zeroes
      return interval.TotalMilliseconds.ToString().PadLeft(12, '0');
    }
    private string build(string msg, string callerName, string filepath, int linenum) {
      // timestamp + caller + file + line + msg
      int i = filepath.LastIndexOf('\\');
      string f = filepath.Substring(i+1);
      return $"{timestamp()}:{f}:{callerName}:{linenum}:>{msg}";
    }
    private string build(string msg) {
      return $"{timestamp()}:>{msg}";
    }
    public void _debug(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      if (LogLevel < Levels.Debug) return;
      Console.WriteLine(build(msg, callerName, filepath, linenum));    
    }
    public void _warn(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      if (LogLevel < Levels.Warn) return;
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine(build(msg, callerName, filepath, linenum));
      Console.ResetColor();
    }
    public void _sys(string msg) {
      if (LogLevel < Levels.SysMsg) return;
      Console.WriteLine(build(msg));
    }
    public void _critical(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      if (LogLevel < Levels.Critical) return;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(build(msg, callerName, filepath, linenum));
      Console.ResetColor();
    }
    private static Log? onelog = null;

    public static Log instance(Levels level = Levels.Debug) {
      if (onelog == null) {
        onelog = new Log();
        onelog.LogLevel = level;
      }
      return onelog;
    }
    
    public static void debug(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      instance()._debug(msg, callerName, filepath, linenum);
    }
    public static void debug(string msg, byte[] buffer,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      instance()._debug(msg, callerName, filepath, linenum);
      Console.WriteLine(ByteToString(buffer));
    }
    public static void warn(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      instance()._warn(msg, callerName, filepath, linenum);
    }
    public static void sys(string msg) {
      instance()._sys(msg);
    }
    public static void critical(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      instance()._critical(msg, callerName, filepath, linenum);
    }
    /// <summary>
    /// Creates a string of hexadecimal characters from a byte array.
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public static string ByteToString(byte[] buffer) {
      if (buffer.Length == 0) return "";
      StringBuilder sb = new StringBuilder();
      // 00 00 00 00  00 00 00 00  00 00 00 00  00 00 00 00
      int section = 1;
      int row = 0;
      sb.Append(buffer[0].ToString("X2"));
      for (int i = 1; i < buffer.Length; i++) {
        if (section < 4) {
          sb.Append(" ");
          sb.Append(buffer[i].ToString("X2"));
          section++;
        } else if (row < 4) {
          section = 0;
          sb.Append("  ");
          sb.Append(buffer[i].ToString("X2"));
          row++;
        } else {
          // end of section and row
          section = 0;
          row = 0;
          sb.Append("\n");
          sb.Append(buffer[i].ToString("X2"));
        }
      }
      return sb.ToString();
    }
  }
}