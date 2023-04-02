using System.Reflection.Metadata;
using System.IO.Ports;
using System.Timers;

namespace NeurCLib;

/// <summary>
/// Definition for the packet data.
/// </summary>
public struct Package {
 // public Package
}
/// <summary>
/// Base class for pinging the arduino. Will probably move things later.
/// </summary>
public class Copper {
  private System.Timers.Timer? ticker;
  private String state = "closed";
  public Copper() {
    SerialPort sp = new SerialPort();
    sp.PortName = "";
    sp.BaudRate = 115200;
    sp.DataBits = 8;
    sp.Parity = Parity.None;
    sp.StopBits = StopBits.One;
  }
  public void start() {
    ticker = new System.Timers.Timer(1000);
    ticker.Elapsed += tick;
    ticker.AutoReset = false;
    ticker.Start();
    state = "started";
  }
  public void tick(object? sender, System.Timers.ElapsedEventArgs e) {
    // TODO
    if (state == "running") {
      ticker.Start();
    }
  }
}
