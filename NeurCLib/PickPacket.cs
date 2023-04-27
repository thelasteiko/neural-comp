using System.IO.Ports;

namespace NeurCLib;

public class PickPacket {
  
}
public class ControlEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
  public ControlEventArgs(Exception? error, bool cancelled, object? userState) : base(error, cancelled, userState) {
  }

  public char KeyPressed {get; set;}
}
public delegate void ControlEventHandler(Object sender, ControlEventArgs e);