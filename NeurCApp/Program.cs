using NeurCLib;

Console.WriteLine("Starting....");

Copper cop = new Copper();

Console.WriteLine($"State is: {cop.state.ToString()}");
Console.WriteLine("Starting connection...");

cop.start();

String? stop = "";
do {
  stop = Console.ReadLine();
  if (stop == null) {
    stop = "";
  }
} while (!stop.ToLower().StartsWith('y'));

cop.stop();