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
3. Quit

Press 1|2|3 + ENTER to make a choice.

Press 3+ENTER or CTRL+c to exit at any time.

The program pauses for ~3 seconds after showing the menu options.

## Expected Output
The program will start by attempting to connect to the arduino.

## Notes

The app just links to the dll in the debug folder of the library. Make sure to build the library before you try to run the app.

One artifact of testing with the arduino was that sometimes, after the arduino reset while the data stream was running, restarting the stream took a few more seconds to come back after the connect was acknowledged and the keepalive started.
