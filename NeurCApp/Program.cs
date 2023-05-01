using NeurCLib;

// initialize log that prints to console
Log.instance(Log.Levels.Debug);
Log.sys("Log initialized. Starting...");

Controller c = new(debug:false);

// to handle CTRL+c
Console.CancelKeyPress += async delegate {
  await c.stop();
  Log.sys("Exiting...");
  Environment.Exit(0);
};

Log.debug($"State is: {c.status.ToString()}");
Log.sys("Starting connection...");

// begin the timer
c.start();

Log.sys("Type q+ENTER to quit.");

// keep going until told to stop or we have an error
String? stop = "";
do {
  stop = Console.ReadLine();
  if (stop == null) {
    stop = "";
  }
} while (!stop.ToLower().StartsWith('q'));

c.stop();
