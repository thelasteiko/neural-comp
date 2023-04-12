using NeurCLib;

// initialize log that prints to console
Log.instance(Log.Levels.SysMsg);
Log.sys("Log initialized. Starting...");

// the cop sends messages to the arduino
Copper cop = new Copper(debug:false);

// to handle CTRL+c
Console.CancelKeyPress += delegate {
  cop.stop();
  Log.sys("Exiting...");
  Environment.Exit(0);
};

Log.debug($"State is: {cop.state.ToString()}");
Log.sys("Starting connection...");

// begin the timer
cop.start();

Log.sys("Type q+ENTER to quit.");

// keep going until told to stop or we have an error
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