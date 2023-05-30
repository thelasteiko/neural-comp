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

// initialize log that prints to console
Log.instance(Log.Levels.Debug);
Log.sys("Log initialized. Starting...");

Controller c = new();

// to handle CTRL+c
Console.CancelKeyPress += async delegate {
  Log.sys("Exiting...");
  await c.stop();
  c.Dispose();
  Environment.Exit(0);
};

Log.sys("Press CTRL+C to exit anytime.");

// add event listener for stream events
// c.StreamData += (o, e) => {
//   //Log.debug("Thread is " + Thread.CurrentThread.ManagedThreadId.ToString());
//   Console.WriteLine($"Stream data: {e.timestamp}, {e.microvolts}");
// };
c.StreamStarted += (o, e) => {
  Console.WriteLine("Stream started.");
};
c.StreamStopped += (o, e) => {
  Console.WriteLine("Stream stopped.");
};
c.TherapyStarted += (o, e) => {
  Console.WriteLine("Therapy started.");
};
c.TherapyStopped += (o, e) => {
  Console.WriteLine("Therapy stopped.");
};

bool running = true;
// task for running the controller so it's not blocked by read
Task t = new(async () => {
  await c.start();
  while(running) {
    // try to restart if failed
    if(!c.IsRunning()) {
      if (c.status == Controller.ControlState.Error)
        await c.stop();
      Log.debug("Controller status is " + c.status.ToString());
      await Controller.doAWait(steps:10, sleepFor:500);
      await c.start();
      if (!c.IsRunning())
        await c.stop();
    }
    Thread.Sleep(10);
  }
});
// show menu, wait a bit so user can read it
string menu = "Options:\n\t1. Start Stream\n\t2. Stop Stream\n\t3. Start Therapy\n\t4. Stop Therapy\n\t5. Quit";
Log.sys(menu);
Log.sys("Please wait...");
await Controller.doAWait(6, 500);
// start task and wait for user input
t.Start();
while(running) {
  int choice = ReadChoice();
  Log.debug("Choice is " + choice.ToString());
  if (c.IsRunning()) {
    if (choice == 1) c.startStreaming();
    else if (choice == 2) c.stopStreaming();
    else if (choice == 3) c.startTherapy();
    else if (choice == 4) c.stopTherapy();
    else if (choice == 5) running = false;
    else {
      Log.sys(menu);
    }
  }
}

Log.sys("Exiting...");

await c.stop();
c.Dispose();
