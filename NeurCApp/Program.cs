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
Console.CancelKeyPress += delegate {
  c.stop();
  Log.sys("Exiting...");
  Environment.Exit(0);
};

Log.sys("Press CTRL+C to exit anytime.");

// add event listener for stream events
c.Stream += async (o, e) => {
  Log.debug("Thread is " + Thread.CurrentThread.ManagedThreadId.ToString());
  Console.WriteLine($"Stream data: {e.timestamp}, {e.microvolts}");
};

bool running = true;
// poll
Task t = new(async () => {
  while(running) {
    if(!c.IsRunning()) {
      await c.stop();
      Log.debug("1 Status is " + c.status.ToString());
      await c.doAWait(steps:3, sleepFor:1000);
      await c.start();
      if (!c.IsRunning())
        await c.stop();
    }
  }
});
Log.sys("Press 1+ENTER to toggle stream, q+ENTER to quit.");
Log.sys("Please wait...");
t.Start();
while(running) {
  int choice = ReadChoice();
  Log.debug("Choice is " + choice.ToString());
  if (choice == 1) await c.toggleStream();
  else running = false;
}

Log.sys("Exiting...");

await c.stop();
