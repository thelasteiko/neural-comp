using System.Text;
using System.Runtime.CompilerServices;
namespace NeurCLib {
  /// <summary>
  /// Singleton class to standardize writes to console with timestamp
  /// and stack tracing.
  /// </summary>
  public sealed class Log {
    /// <summary>
    /// Determines how much text gets logged to the console.
    /// Quiet: no logs at all; you won't see anything
    /// Critical: only critical errors
    /// SysMsg: Standard log level; minimally tracks execution plus critical
    /// Warn: SysMsg plus warnings
    /// Debug: All the messages
    /// </summary>
    public enum Levels {
      Quiet,
      Critical,
      SysMsg,
      Warn,
      Debug
    }
    /// <summary>
    /// Timestamp when the log is created.
    /// </summary>
    private DateTime start;
    private Levels LogLevel;
    
    private Log() {
      start = DateTime.Now;
    }
    /// <summary>
    /// Returns the difference b/t now and the start in milliseconds,
    /// left padded up to 12 zeroes.
    /// </summary>
    /// <returns></returns>
    public string timestamp() {
      TimeSpan interval = DateTime.Now - start;
      // string padded by zeroes
      return interval.TotalMilliseconds.ToString().PadLeft(12, '0');
    }
    /// <summary>
    /// Builds the log message with a standard header.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="callerName"></param>
    /// <param name="filepath"></param>
    /// <param name="linenum"></param>
    /// <returns></returns>
    private string build(string msg, string callerName, string filepath, int linenum) {
      // timestamp + caller + file + line + msg
      int i = filepath.LastIndexOf('\\');
      string f = filepath.Substring(i+1);
      return $"{timestamp()}:{f}:{callerName}:{linenum}:>{msg}";
    }
    /// <summary>
    /// This is for messages that don't need a stack trace.
    /// </summary>
    /// <param name="msg"></param>
    /// <returns></returns>
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
    /// <summary>
    /// Creates if necessary, and returns the one and only Log instance.
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public static Log instance(Levels level = Levels.Debug) {
      if (onelog == null) {
        onelog = new Log();
        onelog.LogLevel = level;
      }
      return onelog;
    }
    /// <summary>
    /// Print a debug message with stack trace.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="callerName"></param>
    /// <param name="filepath"></param>
    /// <param name="linenum"></param>
    public static void debug(string msg,
        [CallerMemberName] string callerName="",
        [CallerFilePath] string filepath="",
        [CallerLineNumber] int linenum=0) {
      instance()._debug(msg, callerName, filepath, linenum);
    }
    /// <summary>
    /// Print debug message with trace and print a byte array
    /// as a hexidecimal string.
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="buffer"></param>
    /// <param name="callerName"></param>
    /// <param name="filepath"></param>
    /// <param name="linenum"></param>
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
    /// <summary>
    /// Print a system message without any trace data.
    /// </summary>
    /// <param name="msg"></param>
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
      // 00 00 00 00 00  00 00 00 00 00  00 00 00 00 00  00 00 00 00 00
      // int section = 1;
      // int row = 0;
      sb.Append(buffer[0].ToString("X2"));
      for (int i = 1; i < buffer.Length; i++) {
        sb.Append(" ");
        if (i % 5 == 0) {
          if (i % 20 == 0) {
            sb.Append("\n");
          } else {
            sb.Append(" ");
          }
        }
        sb.Append(buffer[i].ToString("X2"));
        // if (section < 5) {
        //   sb.Append(" ");
        //   sb.Append(buffer[i].ToString("X2"));
        //   section++;
        // } else if (row < 5) {
        //   section = 0;
        //   sb.Append("  ");
        //   sb.Append(buffer[i].ToString("X2"));
        //   row++;
        // } else {
        //   // end of section and row
        //   section = 0;
        //   row = 0;
        //   sb.Append("\n");
        //   sb.Append(buffer[i].ToString("X2"));
        // }
      }
      return sb.ToString();
    }
  }
}