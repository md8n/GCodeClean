# GCodeClean

A command line utility to do some 'cleaning' of a gcode (an `.nc`, `.gcode`) file.
The primary objective is to be a `GCode Linter`.
But we'll also look at supporting:
* annotating the GCode with explanatory comments (optional),
* eliminating redundant lines (within tolerances),
* clipping decimal places on arguments to meaningful values,
* reorganising the 'words' on a line to meet the official rules, then conform to some common practices (but not all), and then actually replicate the order they must be processed in,
* injecting blank lines to highlight significant instructions (tool raising, tool changes),
* removing some superfluous tokens (soft minimisation), or
* removing all superfluous tokens and spaces (hard minimisation)

## Getting Started

This project needs the .net Core 3.1 SDK to build and run.
And that should be pretty much about it.

### Prerequisites

.net Core 3.1 SDK - get the correct version for your OS and architecture here: [.net Core 3.1 SDK downloads](https://dotnet.microsoft.com/download/dotnet-core/3.1)

A text editor if you want to change something.

### Running and Building

Once you've got the .net Core SDK installed.
Get yourself to a command line prompt, change to the folder where you've cloned this repository to, and enter:
```
dotnet run
```

... and, after it has downloaded all of the packages it needs with nuget (which may take a while), and built `GCodeClean`, you'll get a little bit of help info like this:
```
GCode Clean
-----------
Usage:
gccodeclean <filename>
```

So find yourself a gcode (`.nc`, `.gcode`, etc.) file to use for `<filename>`.
And replace `<filename>` with the full path to your gcode file (as per what your OS requires).

`GCodeClean` will require Read acces to that file, and Write access to the folder where that file is located.

Then you can process it with the command:
```
dotnet run -- <filename>
```
Obviously replacing `<filename>` with your file's name (and path if needed).

Or you can build `GCodeClean` and run it with these two steps:
```
dotnet publish
```
Take a note of the `publish` folder, the `GCodeClean` executable will be located there.

And then run the `GCodeClean` executable.
e.g. for Windows that might look like:
```
D:\GitHub\gcodeclean>bin\Debug\netcoreapp3.1\publish\gcodeclean FacadeFullAlternate.nc
```

or for Linux (Ubuntu 18.04)
```
./bin/Debug/netcoreapp3.1/publish/GCodeClean FacadeFullAlternate.nc
```

After processing `GCodeClean` will report the number of lines that it output.
The output file will have `-gcc` appended to name of the input file (but before the file extension) that you provided on the command line.

Note: If the input file does not exist then `GCodeClean` will silently fail, that's how it is designed.

## What's Special about GCodeClean?

GCodeClean uses async streaming throughout from input to output.  Hopefully this should keep memory consumption and the number of threads to a minimum regardless of what OS / Architecture you use.

## Deployment

Copy the contents of the `publish` folder above to wherever you need them.  Assuming that it is the same OS / Architecture of course.

If you need to build it for a different OS / Architecture then start poking around in the dotnet info - there's a LOT of it.

## Authors

* **Lee HUMPHRIES** - *Initial work* - [md8n](https://github.com/md8n)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Acknowledgments

* To all those comments I picked up out of different people's posts in Stack Overflow
* The quality info on C# 8, and IAsyncEnumerable were invaluable.
