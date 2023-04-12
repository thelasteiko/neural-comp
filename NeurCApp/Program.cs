using NeurCLib;

Log.instance(Log.Levels.SysMsg);
Log.sys("Log initialized. Starting...");

Copper cop = new Copper(debug:false);

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
  if (cop.state == Copper.State.Error) {
    Log.warn("Entered error state, exiting...");
    cop.stop();
    // TODO add actual error codes
    Environment.Exit(1);
  }
} while (!stop.ToLower().StartsWith('q'));

cop.stop();