using NeurCLib;
using NeurCApp;

// TODO command line args

// initialize log that prints to console
Log.instance(Log.Levels.Debug);
Log.sys("Log initialized. Starting...");

Controller c = new(debug:false);

// to handle CTRL+c
Console.CancelKeyPress += delegate {
  c.stop();
  Log.sys("Exiting...");
  Environment.Exit(0);
};

Log.sys("Press CTRL+C to exit anytime.");

// add event listener for stream events
c.Stream += async (o, e) => {
  Console.WriteLine($"Stream data: {e.timestamp}, {e.microvolts}");
};

MenuFactory mf = MenuFactory.BuildSimple("----- MAIN -----", "Connect to Arduino");

mf.WaitPrint();

int choice = mf.ReadChoice();

switch(choice) {
  case 0:
    c.stop();
    Environment.Exit(0);
    break;
  case 1:
    await c.start();
    break;
}

mf = MenuFactory.BuildSimple("----- START STREAM -----", "Start Streaming");
MenuFactory mf2 = MenuFactory.BuildSimple("----- STOP STREAM -----", "Stop Streaming");

do {
  if (c.IsStreaming) {
    mf2.WaitPrint();
    choice = mf2.ReadChoice();
    if (choice == 1) await c.stopStreaming();
  } else {
    mf.WaitPrint();
    choice = mf.ReadChoice();
    if (choice == 1) await c.startStream();
  }
} while (choice != 0);

Log.sys("Exiting...");

c.stop();
