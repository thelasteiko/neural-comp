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

Press q+ENTER or CTRL+c to exit at any time.

### Arguments

The following command line arguments are recognized:

* l, log  : Set the log level. Options are quiet, critical, sys, warn, and debug.
* a, auto : Automatically try to connect when the program starts.

## Notes

The app just links to the dll in the debug folder of the library. Make sure to build the library before you try to run the app.