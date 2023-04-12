using System.IO.Ports;

namespace NeurCLib {
  /// <summary>
  /// Interface for the serial port. This allows the Copper to switch
  /// between a pseudo port for testing and an actual SerialPort.
  /// </summary>
  public interface IPorter {
    public string PortName {get;set;}
    public int BaudRate {get;set;}
    public int DataBits {get;set;}
    public Parity Parity {get;set;}
    public StopBits StopBits {get;set;}
    public int ReadTimeout {get;set;}
    public int WriteTimeout {get;set;}
    public Boolean IsOpen {get;}

    public void Open();
    public void Close();
    public void Write(byte[] buffer, int offset, int count);
    public int Read(byte[] buffer, int offset, int count);

  }
  /// <summary>
  /// PseudoPorter is a test class for debugging.
  /// </summary>
  public class PseudoPorter : IPorter {
    private String _PortName = "COM3";
    public String PortName {
      get => _PortName;
      set => _PortName = value;
    }
    private int _BaudRate = 115200;
    public int BaudRate {
      get => _BaudRate;
      set => _BaudRate = value;
    }
    private int _DataBits = 8;
    public int DataBits {
      get => _DataBits;
      set => _DataBits = value;
    }
    private Parity _Parity = Parity.None;
    public Parity Parity {
      get => _Parity;
      set => _Parity = value;
    }
    private StopBits _StopBits = StopBits.One;
    public StopBits StopBits {
      get => _StopBits;
      set => _StopBits = value;
    }
    private int _ReadTimeout = 1000;
    public int ReadTimeout {
      get => _ReadTimeout;
      set => _ReadTimeout = value;
    }
    private int _WriteTimeout = 1000;
    public int WriteTimeout {
      get => _WriteTimeout;
      set => _WriteTimeout = value;
    }
    private Boolean _IsOpen = false;
    public Boolean IsOpen {
      get => _IsOpen;
      set => _IsOpen = value;
    }
    /// <summary>
    /// The last message the pseudo port received.
    /// </summary>
    private byte[] LastMessage = new byte[0];

    public void Open() {
      IsOpen = true;
      Log.debug("Port opened.");
    }
    public void Close() {
      IsOpen = false;
      Log.debug("Port closed.");
    }
    public void Write(byte[] buffer, int offset, int count) {
      // string.join(' ', buffer.Select(b => b.ToString('X2')))
      Log.debug($"Message Written [{offset}, {count}]:", buffer);
      LastMessage = buffer;
    }
    public int Read(byte[] buffer, int offset, int count) {
      int min = Math.Min(buffer.Length, LastMessage.Length);
      for (int i = offset; i < min; i++) {
        buffer[i] = LastMessage[i];
      }
      Log.debug($"Message Read [{offset}, {count}]", buffer);
      return count;
    }
    // TODO simulate timeout and errors
  }
  /// <summary>
  /// The PortWrapper wraps a SerialPort so I can easily switch this out
  /// with the test class.
  /// </summary>
  public class PortWrapper : IPorter {
    private SerialPort sporter = new SerialPort();
    public String PortName {
      get => sporter.PortName;
      set => sporter.PortName = value;
    }
    public int BaudRate {
      get => sporter.BaudRate;
      set => sporter.BaudRate = value;
    }
    public int DataBits {
      get => sporter.DataBits;
      set => sporter.DataBits = value;
    }
    public Parity Parity {
      get => sporter.Parity;
      set => sporter.Parity = value;
    }
    public StopBits StopBits {
      get => sporter.StopBits;
      set => sporter.StopBits = value;
    }
    public int ReadTimeout {
      get => sporter.ReadTimeout;
      set => sporter.ReadTimeout = value;
    }
    public int WriteTimeout {
      get => sporter.WriteTimeout;
      set => sporter.WriteTimeout = value;
    }
    public Boolean IsOpen {
      get => sporter.IsOpen;
    }
    public void Open() {
      sporter.Open();
    }
    public void Close() {
      sporter.Close();
    }
    public void Write(byte[] buffer, int offset, int count) {
      sporter.Write(buffer, offset, count);
    }
    public int Read(byte[] buffer, int offset, int count) {
      return sporter.Read(buffer, offset, count);
    }
  }
}