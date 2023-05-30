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

## Expected Output
The program will start by attempting to connect to the arduino. Once it connects, it will start the stream and begin the logging and interpreting it. Therapy should start/stop upon detection of seizure state.

## Notes

The app just links to the dll in the debug folder of the library. Make sure to build the library before you try to run the app.

The log saves the actual state of delivering therapy as given by the device, not the intended state. There may be a disconnect between the seizure state detected and the therapy state, particularly when seizure detection state is switching. This is expected as the confidence in the detection grows.

While the device responds quickly to starting therapy, it delays in responding to stopping therapy.
