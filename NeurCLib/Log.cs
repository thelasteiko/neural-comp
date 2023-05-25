using System.Text;
using System.Runtime.CompilerServices;
namespace NeurCLib;

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
  public Levels LogLevel {set; get;}
  
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
  private static System.Threading.Mutex logmutex = new();
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
    lock(logmutex) {
      instance()._debug(msg, callerName, filepath, linenum);
    }
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
    lock(logmutex) {
      instance()._debug(msg, callerName, filepath, linenum);
      Console.WriteLine(ByteToString(buffer));
    }
  }
  public static void warn(string msg,
      [CallerMemberName] string callerName="",
      [CallerFilePath] string filepath="",
      [CallerLineNumber] int linenum=0) {
    lock (logmutex) {
      instance()._warn(msg, callerName, filepath, linenum);
    }
  }
  /// <summary>
  /// Print a system message without any trace data.
  /// </summary>
  /// <param name="msg"></param>
  public static void sys(string msg) {
    lock(logmutex) {
      instance()._sys(msg);
    }
  }
  public static void critical(string msg,
      [CallerMemberName] string callerName="",
      [CallerFilePath] string filepath="",
      [CallerLineNumber] int linenum=0) {
    lock (logmutex) {
      instance()._critical(msg, callerName, filepath, linenum);
    }
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
    }
    return sb.ToString();
  }
}
/// <summary>
/// The file log prints stream data and start/stop events to a log file.
/// Ensures log files do not exceed 2MB.
/// Filename format: <date>-<time>-<iteration>.csv
/// If log level is Debug...
/// Record format: <log timestamp>,<packet timestamp>,<microvolts>,<classification>,<therapy>
/// If log level is SysMsg...
/// Record format: <packet timestamp>,<microvolts>,<classification>,<therapy>
/// </summary>
internal sealed class FileLog : IDisposable{
  /// <summary>
  /// Max allowable log file size in bytes.
  /// </summary>
  /// <returns></returns>
  public const int MAX_FILE_SIZE = (1024*1024*2);
  private bool _IsOpen = false;
  /// <summary>
  /// Whether the file stream is open or not.
  /// </summary>
  /// <value></value>
  public bool IsOpen {get => _IsOpen;}
  private int log_index = 0;
  private DateTime current_stamp;
  private string log_path = "";
  private StreamWriter? log_stream;
  private bool disposed = false;
  private int bytes_written = 0;
  private void _incrementLog() {
    log_index++;
    log_path = $"{current_stamp.ToString("yyyyMMdd")}-{current_stamp.ToString("HHmmss")}-{log_index}.csv";
    log_stream?.Dispose();
    log_stream = new(log_path);
    _IsOpen = true;
    bytes_written = 0;
  }
  /// <summary>
  /// Create and start the log file. Opens the stream writer.
  /// </summary>
  public void _create() {
    if (IsOpen) return;
    current_stamp = DateTime.Now;
    _incrementLog();
    //_write("0,start stream");
    Log.debug("Starting log");
  }
  /// <summary>
  /// Write text to the log file. Format is CSV, with the log timestamp
  /// in the first column.
  /// </summary>
  /// <param name="msg"></param>
  public void _write(string msg) {
    if (!IsOpen) return;
    // check size of file
    if (bytes_written >= MAX_FILE_SIZE) {
      _incrementLog();
    }
    string s;
    if (Log.instance().LogLevel >= Log.Levels.Debug) {
      s = Log.instance().timestamp() + "," + msg;
    } else {
      s = msg;
    }
    bytes_written += s.Length;
    log_stream?.WriteLine(s);
  }
  /// <summary>
  /// Write the stream data to the log file.
  /// </summary>
  /// <param name="args"></param>
  public void _write(StreamEventArgs args, bool seizure_detected, bool therapy_on) {
    // format packet data
    _write($"{args.timestamp},{args.microvolts},{seizure_detected},{therapy_on}");
  }
  /// <summary>
  /// Close the log and dispose the stream writer.
  /// </summary>
  public void _close() {
    if (!IsOpen) return;
    log_index = 0;
    //_write("0,stop stream");
    _IsOpen = false;
    log_stream?.Dispose();
    Log.debug("Closing log");
  }
  private static FileLog? onelog;
  /// <summary>
  /// Get the file log instance.
  /// </summary>
  /// <returns></returns>
  public static FileLog instance() {
    if (onelog == null) {
      onelog = new FileLog();
    }
    return onelog;
  }
  /// <summary>
  /// Create and start the log file. Opens the stream writer.
  /// </summary>
  public static void create() {
    instance()._create();
  }
  /// <summary>
  /// Write the stream data to the log file.
  /// </summary>
  /// <param name="args"></param>
  public static void write(StreamEventArgs args, bool seizure_detected, bool therapy_on) {
    instance()._write(args, seizure_detected, therapy_on);
  }
  /// <summary>
  /// Close the log and dispose the stream writer.
  /// </summary>
  public static void close() {
    instance()._close();
  }
  /// <summary>
  /// Close the log
  /// </summary>
  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  private void Dispose(bool disposing) {
    if (!disposed) {
      if (disposing) {
        _close();
      }
      disposed = true;
    }
  }
  ~FileLog() {
    Dispose(false);
  }
}