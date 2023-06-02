# NeurC

Library and application for the Neural Computation project.

* NeurCLib: The API portion
* NeurCApp: The command line application

## Usage

This project was made with Visual Studio Code. If all else fails, the following commands can be run from the command line.

```bash
C:\..\neural-comp> dotnet build "NeurCLib\NeurCLib.csproj"
C:\..\neural-comp> dotnet run --project "NeurCApp\NeurCApp.csproj"
```

The program will start with the following options:

1. Start Stream
2. Stop Stream
4. Start Therapy
5. Stop Therapy
6. Quit

Press 1|2|3|4|5|6 + ENTER to make a choice.

Press 6+ENTER or CTRL+c to exit at any time.

The program pauses for ~3 seconds after showing the menu options.

The manually entered options are there for convenience. The program should run automatically, start/stop both the stream and the therapy independent of the user.

By default the signal is processed every other data point. This is working on the test equipment (aka my computer) but may be too often for other setups. You may change the sample rate and number of predictions from the controller initialization:

```c#
// default
Controller c = new(Log.Levels.SysMsg, sample_rate:2, max_predict_size:5);

// example
Controller c = new(Log.Levels.SysMsg, sample_rate:10, max_predict_size:3);
```

* sample_rate: Changes how often the prediction runs. A lower sample rate means that the predictions will run more often.
* max_predict_size: This is the number of prior predictions used to calculate the confidence in the current prediction. A higher predict size will mean that more prior predictions are used, and the longer the program will take to switch between seizure and non-seizure.

## Expected Output
The program will start by attempting to connect to the arduino. Once it connects, it will start the stream and begin the logging and interpreting it. Therapy should start/stop upon detection of seizure state.

## Notes

The app just links to the dll in the debug folder of the library. Make sure to build the library before you try to run the app.

The log saves the actual state of delivering therapy as given by the device, not the intended state. There may be a slight disconnect between the seizure state detected and the therapy state when seizure detection state is switching. This is expected as the confidence in the detection grows.

Confidence will be displayed and logged in debug mode. Set the Log level on the controller to Debug to see it.