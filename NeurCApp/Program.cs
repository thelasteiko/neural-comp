using NeurCLib;

Log.instance(Log.Levels.Debug);
Log.sys("Log initialized. Starting...");

Copper cop = new Copper(debug:true);

Console.CancelKeyPress += delegate {
  cop.stop();
  Log.sys("Exiting...");
  Environment.Exit(0);
};

Log.debug($"State is: {cop.state.ToString()}");
Log.sys("Starting connection...");

cop.start();

Log.sys("Type q+ENTER to quit.");

String? stop = "";
do {
  stop = Console.ReadLine();
  if (stop == null) {
    stop = "";
  }
} while (!stop.ToLower().StartsWith('q'));

cop.stop();