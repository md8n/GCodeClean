# GCodeClean

A command line utility to do some 'cleaning' of a gcode (an `.nc`, `.gcode`) file.
The primary objective is to be a `GCode Linter`, `per line` linting of gcode is already done.

We also have:
* eliminating redundant lines (within tolerances),
* clipping decimal places on arguments to meaningful values,
* `per line` linting: splitting lines to match the actual execution order as per the NIST gcode spec, and then reorganising the 'words' on a line to conform to some common practices (but not all),
* `annotate` the GCode with explanatory comments (optional),
* 'soft', 'hard' or custom removal of superfluous tokens (`minimise`).

We'll also look at supporting:
* injecting blank lines to highlight significant instructions (tool raising, tool changes),
* `preamble linting`: Adding a 'standard' set of gcode declarations, i.e. converting the 'implicit' to 'explicit'.
* `postamble linting`: Similar to the `peramble`, but at the end of the file (obviously).

## Getting Started

There are standalone 64bit release builds available, for Linux, Windows and OSX at [GCodeClean releases](https://github.com/md8n/GCodeClean/releases)

The standalone releases include all the relevant .NET Core 3.1 libraries for this application.

To build and run this project needs the .NET Core 3.1 SDK.

There are a very large number of possible targets including 32bit, ARM, etc.

### Command Line Parameters

Throw the `--help` command line option at GCodeClean and you'll get back the following:

```
gcodeclean 1.0.0
Copyright (C) 2020 gcodeclean
USAGE:
Clean GCode file:
  GCodeClean --filename facade.nc

  --filename    Required. Full path to the input filename.

  --annotate    Annotate the GCode with inline comments.

  --minimise    Select preferred minimisation strategy, 'soft' - (default) FZ only, 'hard' - All codes, or list of codes
                e.g. FGXYZIJK

  --help        Display this help screen.

  --version     Display version information.
```
Please note that the version number is currently incorrect, but all the rest of it is correct.

`--annotate` is a simple switch, include it on its own to have your GCode annotated with inline comments (even if you specify hard minimisation).

`--minimise` accepts 'soft', 'hard', or a selection you choose of codes to be deduplicated.
- soft = 'F', 'Z' only - this is also the default.
- hard = All codes, and spaces between 'words' are eliminated also.
- Or a custom selection of codes from the official list of `ABCDFGHIJKLMNPRSTXYZ`.

Now find yourself a gcode (`.nc`, `.gcode`, etc.) file to use for the option `--filename <filename>`.
And replace `<filename>` with the full path to your gcode file (as per what your OS requires).

`GCodeClean` will require Read access to that file, and Write access to the folder where that file is located.

And then run the `GCodeClean` executable.
e.g. for Windows that might look like:
```
.\gcodeclean --filename FacadeFullAlternate.nc --minimise soft --annotate
```

or for Linux (Ubuntu 18.04)
```
./GCodeClean --filename FacadeFullAlternate.nc --minimise hard --annotate
```

After processing `GCodeClean` will report the number of lines that it output.
The output file will have `-gcc` appended to name of the input file (but before the file extension) that you provided on the command line.

Note: If the input file does not exist (or can't be found, i.e. your typo) then `GCodeClean` will fail, but it won't do any harm.

### Prerequisites for Building it Yourself

.NET Core 3.1 SDK - get the correct version for your OS and architecture here: [.NET Core 3.1 SDK downloads](https://dotnet.microsoft.com/download/dotnet-core/3.1)

A text editor if you want to change something.

### Building and Running it Yourself

Once you've got the .NET Core SDK installed.

Get yourself to a command line prompt, change to the folder where you've cloned this repository to, and enter:
```
dotnet restore
```

... and, after it has downloaded all of the packages it needs with nuget (which may take a while), you can then build and run it with:

```
dotnet run
```

and ... it will probably break because you haven't specified a filename.

So to start with you can try:
```
dotnet run -- --help
```

And get some help info, as described above.

Then you can process it with the command:
```
dotnet run -- --filename <filename>
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
.\bin\Debug\netcoreapp3.1\publish\gcodeclean --filename FacadeFullAlternate.nc --minimise hard --annotate
```

or for Linux (Ubuntu 18.04)
```
./bin/Debug/netcoreapp3.1/publish/GCodeClean --filename FacadeFullAlternate.nc --minimise hard --annotate
```

## What's Special about GCodeClean?

GCodeClean uses async streaming throughout from input to output.  Hopefully this should keep memory consumption and the number of threads to a minimum regardless of what OS / Architecture you use.

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
* The quality info on C# 8, and IAsyncEnumerable were invaluable.
