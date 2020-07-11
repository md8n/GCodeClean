# GCodeClean

A library and command line utility to do some 'cleaning' of a gcode (an `.nc`, `.gcode`) file.
The primary objective is to be a `GCode Linter`, `per line` linting of gcode is already done.

We also have:
* eliminating redundant lines (within tolerances),
* converting very short arcs (G2, G3) to simple lines (G1), also within tolerances,
* linear to arc deduplication, converting several simple lines to a single arc,
* eliminate meaningless movement commands - especially G0 without any arguments,
* clipping decimal places on arguments to meaningful values,
* `per line` linting: splitting lines to match the actual execution order as per the NIST gcode spec, and then reorganising the 'words' on a line to conform to some common practices (but not all),
* `annotate` the GCode with explanatory comments (optional),
* 'soft', 'medium', 'hard' or custom removal of superfluous tokens (`minimise`).
* `preamble linting`: Adding a 'standard' set of gcode declarations, i.e. converting the 'implicit' to 'explicit'.

We'll also look at supporting:
* injecting blank lines to highlight significant instructions (tool raising, tool changes),
* `postamble linting`: Similar to the `peramble`, but at the end of the file (obviously).

## Getting Started

There are standalone 64bit release builds available, for Linux, Windows and OSX at [GCodeClean releases](https://github.com/md8n/GCodeClean/releases)

The standalone releases include all the relevant .NET Core 3.1 libraries for this application.

To build and run this project needs the .NET Core 3.1 SDK.

There are a very large number of possible targets including 32bit, ARM, etc.

### Command Line Parameters

Throw the `--help` command line option at GCodeClean and you'll get back the following:

```
Copyright (C) 2020 gcodeclean
USAGE:
Clean GCode file:
  GCodeClean --filename facade.nc

  --filename      Required. Full path to the input filename.

  --annotate      Annotate the GCode with inline comments.

  --minimise      (Default: soft) Select preferred minimisation strategy, 'soft' - (default) FZ only, 'medium' - All
                   codes excluding IJK (but leave spaces in place), 'hard' - All codes excluding IJK and remove spaces,
                   or list of codes e.g. FGXYZ

  --tolerance     Enter a clipping tolerance for the various deduplication operations

  --arcTolerance  Enter a tolerance for the 'point-to-point' length of arcs (G2, G3) below which 
                   they will be converted to lines (G1)

  --zClamp        Restrict z-axis positive values to the supplied value

  --help          Display this help screen.

  --version       Display version information.

  
```

`--annotate` is a simple switch, include it on its own to have your GCode annotated with inline comments (even if you specify hard minimisation).

`--minimise` accepts 'soft', 'hard', or a selection you choose of codes to be deduplicated.
- soft = 'F', 'Z' only - this is also the default.
- medium = All codes excluding IJK, but there is a space between each 'word'.
- hard = All codes excluding IJK, and spaces between 'words' are eliminated also.
- Or a custom selection of codes from the official list of `ABCDFGHLMNPRSTXYZ` (i.e. excluding IJK) and the 'others' `EOQUV`.

`--tolerance` accepts values from 0.0005 to 0.05 for inches or 0.005 to 0.5 for millimeters and uses this value when 'clipping' all arguments with the exception of I, J or K.

`--arcTolerance` accepts values from 0.0005 to 0.05 for inches or 0.005 to 0.5 for millimeters and uses this value to 'simplify' very short arcs to lines.

`--zClamp` accepts values from 0.05 to 0.5 for inches or 0.5 to 10.0 for millimeters and uses this value for all positive z-axis values.

For the tolerance values, the smallest value (inch or mm specific) is used as the default value, whereas for clamping values the largest value is used as the default.

Now find yourself a gcode (`.nc`, `.gcode`, etc.) file to use for the option `--filename <filename>`.
And replace `<filename>` with the full path to your gcode file (as per what your OS requires).

`GCodeClean` will require Read access to that file, and Write access to the folder where that file is located.

And then run the `GCodeCleanCLI` executable.
e.g. for Windows that might look like:
```
.\gcodecleancli --filename FacadeFullAlternate.nc --minimise soft --annotate
```

or for Linux (Ubuntu 18.04)
```
./GCodeCleanCLI --filename FacadeFullAlternate.nc --minimise hard --annotate
```

After processing `GCodeCleanCLI` will report the number of lines that it output.
The output file will have `-gcc` appended to name of the input file (but before the file extension) that you provided on the command line.

Note: If the input file does not exist (or can't be found, i.e. your typo) then `GCodeCleanCLI` will fail, but it won't do any harm.

### Prerequisites for Building it Yourself

.NET Core 3.1 SDK - get the correct version for your OS and architecture here: [.NET Core 3.1 SDK downloads](https://dotnet.microsoft.com/download/dotnet-core/3.1)

A text editor if you want to change something.

### Building and Running it Yourself

Once you've got the .NET Core SDK installed.

Get yourself to a command line prompt, change to the folder where you've cloned this repository to, and then to the CLI folder, and enter:
```
dotnet restore
```

... and, after it has downloaded all of the packages it needs with nuget (which may take a while), you can then build and run it with:

```
dotnet run
```

and ... it will probably give you the help text because you haven't specified a filename.


To get it to process a file try the following command:
```
dotnet run -- --filename <filename>
```
Obviously replacing `<filename>` with your file's name (and path if needed).

Or you can build `GCodeCleanCLI` and run it with these two steps:
```
dotnet publish
```
Take a note of the `publish` folder, the `GCodeCleanCLI` executable will be located there.

And then run the `GCodeCleanCLI` executable.
e.g. for Windows that might look like:
```
.\bin\Debug\netcoreapp3.1\publish\gcodecleancli --filename FacadeFullAlternate.nc --minimise hard --annotate
```

or for Linux (Ubuntu 18.04)
```
./bin/Debug/netcoreapp3.1/publish/GCodeCleanCLI --filename FacadeFullAlternate.nc --minimise hard --annotate
```

## GCodeClean Solution Organisation

GCodeClean is organised into 3 projects:
1. GCodeClean - A library that contains most of the code
2. GCodeCleanCLI - An executable to call the above library - this also handles the command line arguments and does the actual file handling
3. CodeClean.Test - A test suite - which will be 'grown' over time.

## What's Special about GCodeClean?

GCodeClean uses async streaming throughout from input to output.  Hopefully this should keep memory consumption and the number of threads to a minimum regardless of what OS / Architecture you use.

### GCode Linting

The GCode specification allows a lot of flexibility, for example the single letter 'codes' at the start of each 'word' can be upper or lower case, and spaces are allowable after the 'code' and before the number.  The specification also allows for a lot of assumptions about the 'state' of a machine when it starts processing a given GCode file.

However, certain conventions have arisen in how GCode should be presented.  There are also strict guidelines within the GCode specification as regards the execution order of various commands when they appear on the same line.

GCodeClean's linting approach is to respect those conventions while prioritising the execution order, and deliberately injecting commands to turn the implicit assumptions about the state of the machine into explicit assertions about what state is desired.

This means that any line that has multiple commands on it (G, M, F, S, T) will be split into multiple lines, and those line will appear in execution order.
Also GCodeClean defines a set of GCode commands as a 'preamble'. When the first movement command is detected, any of these 'preamble' codes that have not yet been seen are injected into the GCode above that movement command.
This also adds the concept of a 'Context' (i.e. the state as identified by the various commands seen so far, or the state that is desired), the preamble is the first such 'Context'.

For example, it's common to see a Feed Rate command (F) at the end of a movement command, such as:
```
G01 Z -4.0000 F 800.0000
G03 X 109.5488 Y 450.7407 Z -4.0000 I -229.6457 J 52.6435 F 550.0000
```
The linting function splits these lines according to the execution order of the various parts to give:
```
F800
G01 Z-4

F550
G03 X109.549 Y450.741 I-229.646 J52.644
```

The start of a GCode file may appear as follows:
```
G0 G40 G90 G17
G21

T1 M6
M3 S5000
G0 X39.29 Y-105.937
```
The linting function will split the line with multiple GCodes according to their execution order, and it will also 'inject' any 'missing' commands that should reasonably be present at the start of the file to define the machine's desired state at the start of processing.
```
G17
G40
G90

G21

T1
M6

S5000
M3

(Preamble completed by GCodeClean)
G94
G49
(Preamble completed by GCodeClean)
G0 X39.29 Y-105.937
```

## Deployment

Run a fresh 'self-contained' `publish` of `GCodeClean` as follows:
```
dotnet publish  /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary --output bin/release/netcoreapp3.1/publish --self-contained
```
You can also include the option `--configuration Release` for a release build.
And target many different platforms by adding `--runtime ` and specifying a runtime platform, e.g. `--runtime win-x64`

Copy the contents of the `publish` folder above to wherever you need them.  Assuming that it is the same OS / Architecture of course.

The `dotnet restore` command above gets the runtimes for `linux-x64`, `osx-x64`, `win-x64`.  But there are many, many more options.  If you choose a different option then you may need to allow time for the `restore` of that runtime before publishing can actually take place.

## Authors

* **Lee HUMPHRIES** - *Initial work* - [md8n](https://github.com/md8n)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Acknowledgments

* To all those comments I picked up out of different people's posts in Stack Overflow
* The quality info on C# 8, and IAsyncEnumerable were invaluable
* All the sample GCode files provided by the Maslow CNC community [Maslow CNC](https://forums.maslowcnc.com/)
