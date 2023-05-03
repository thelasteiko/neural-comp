using NeurCLib;
using NeurCApp;

static int ReadChoice() {
  String? s;
  int c = 0;
  s = Console.ReadLine();
  if (s != null) {
    int.TryParse(s, out c);
  }
  return c;
}

// TODO command line args

// initialize log that prints to console
Log.instance(Log.Levels.Debug);
Log.sys("Log initialized. Starting...");

Controller c = new(debug:false);

// to handle CTRL+c
Console.CancelKeyPress += async delegate {
  Log.sys("Exiting...");
  await c.stop();
  c.Dispose();
  Environment.Exit(0);
};

Log.sys("Press CTRL+C to exit anytime.");

// add event listener for stream events
c.Stream += (o, e) => {
  //Log.debug("Thread is " + Thread.CurrentThread.ManagedThreadId.ToString());
  Console.WriteLine($"Stream data: {e.timestamp}, {e.microvolts}");
};

bool running = true;
// poll
Task t = new(async () => {
  await c.start();
  while(running) {
    if(!c.IsRunning()) {
      if (c.status == Controller.ControlState.Error)
        await c.stop();
      Log.debug("1 Status is " + c.status.ToString());
      await c.doAWait(steps:10, sleepFor:500);
      await c.start();
      if (!c.IsRunning())
        await c.stop();
    }
  }
});
Log.sys("Options: ");
Log.sys("\t1. Start Stream");
Log.sys("\t2. Stop Stream");
Log.sys("\tq. Quit");
Log.sys("Please wait...");
await c.doAWait(5, 500);
t.Start();
while(running) {
  int choice = ReadChoice();
  Log.debug("Choice is " + choice.ToString());
  // if (choice == 1) c.toggleStream();
  // else running = false;
  if (c.IsRunning()) {
    if (choice == 1) c.startStreaming();
    else if (choice == 2) c.stopStreaming();
    else running = false;
  }
}

Log.sys("Exiting...");

await c.stop();
c.Dispose();
