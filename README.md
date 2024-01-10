# GCodeClean

A library and command line utility to do some 'cleaning' of a gcode (an `.nc`, `.gcode`) file.
The primary objective is to be a `GCode Linter`, as part of that `per line` linting of gcode is already done.

We also have:
* eliminating redundant lines (within tolerances),
* converting very short arcs (G2, G3) to simple lines (G1), also within tolerances,
* linear to arc deduplication, converting several simple lines to a single arc,
* eliminate meaningless movement commands - especially G0 without any arguments,
* correcting G1 to G0 when the z-axis is at a positive value,
* clipping decimal places on arguments to meaningful values (as per the NIST spec),
* `per line` linting: splitting lines to match the actual execution order as per the NIST gcode spec, and then reorganising the 'words' on a line to conform to some common practices (but not all),
* `annotate` the GCode with explanatory comments (optional),
* 'soft', 'medium', 'hard' or custom removal of superfluous tokens (`minimise`).
* `preamble linting`: Adding a 'standard' set of gcode declarations, i.e. converting the 'implicit' to 'explicit'.
* `postamble linting`: Similar to the `preamble`, but at the end of the file (obviously).
* `file terminator matching`: Ensuring that if the file demarcation character `%` is used at the start of the file then it is also used at the end.
* `split`ting of GCode files into individual files, each with a single cutting path.
* `merge`ing of previously `split` files, with some effort at ordering them to reduce the amount of travelling (`G0`) distance in total.

## Getting Started

There are standalone release builds available, for Linux, Raspberry Pi (linux-arm), and Windows at [GCodeClean releases](https://github.com/md8n/GCodeClean/releases). It is very easy to a build for MacOS / OSX (osx-64) (see `BuildItYourself.md`). Download the release you need and unzip it in a folder that works for you. GCodeClean is a command line application, so you run it by using a 'terminal' and typing the command in to do what you want.

The standalone releases are single file executables.

Alternatively you can build and run this project yourself. See `BuildItYourself.md` for instructions and tips. And how to deploy. And if you do build it yourself then there are a very large number of possible targets including 32bit, and many specific Linux distros, etc.

## Running GCodeClean - the tl;dr version

After downloading the release you need for your OS and unpacking it into its own folder (or building it yourself), then it's ready for use.

Change directory to the location where you unpacked (unzipped) the release - you're looking for the file called `GCC.exe`, this is the command line app you'll use.

GCodeClean has three 'commands' `clean`, `split` and `merge`. `clean` is the one you'll be most interested in.

for Windows you would type in something like and press `enter`:
```
.\gcc --filename <full path to your gcode file here>
```

Or for Linux (e.g. Ubuntu 18.04 / 20.04 / 22.04) it would be:

```
./GCC --filename <full path to your gcode file here>
```

For the above `<full path to your gcode file here>` tells `GCC` not just the name of your GCode file, but also where to find it.

`GCC` will find your file and process it, producing a new file that has a very similar name, and telling you how many lines are in the new file.

### Command Line Parameters

Throw the `--help` command line option at the GCodeClean `GCC` (in other words type in `.\gcc --help`) and you'll get back the following:

```
Description:
  GCodeClean

Usage:
  GCC [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  clean  Clean your GCode file.
  split  Split your GCode file into individual cutting actions.
  merge  Merge a folder of files, produced by split, back into a single GCode file.
```

#### The `clean` Command

Repeat the above `--help` command, but specify `clean` as the command (in other words type in `.\gcc clean --help`) and you'll get back the following:

```
Description:
  Clean your GCode file.

Usage:
  GCC clean [options]

Options:
  --filename <filename> (REQUIRED)  Full path to the input filename
  --tokenDefs <tokenDefs>           Full path to the tokenDefinitions.json file [default:
                                    C:\GitHub\GCodeClean\tokenDefinitions.json]
  --annotate                        Annotate the GCode with inline comments [default: False]
  --lineNumbers                     Keep line numbers [default: False]
  --minimise <minimise>             Select preferred minimisation strategy,
                                    'soft' - (default) FZ only,
                                    'medium' - All codes excluding IJK(but leave spaces in place),
                                    'hard' - All codes excluding IJK and remove spaces,
                                    or list of codes e.g.FGXYZ [default: soft]
  --tolerance <tolerance>           Enter a clipping tolerance for the various deduplication operations. Default value
                                    ultimately depends on the units
  --arcTolerance <arcTolerance>     Enter a tolerance for the 'point-to-point' length of arcs (G2, G3) below which they will
                                    be converted to lines (G1)
  --zClamp <zClamp>                 Restrict z-axis positive values to the supplied value
  -?, -h, --help                    Show help and usage information
```

`--annotate` is a simple switch, include it to have your GCode annotated with inline comments (even if you specify hard minimisation).

`--lineNumbers` is also a simple switch. Normally line numbers will be stripped out (they are NOT recommended), but adding this flag will ensure they are preserved (if you must).

`--minimise` accepts 'soft', 'medium', 'hard', or a selection you choose of codes to be deduplicated.
- soft = 'F', 'Z' only - this is also the default.
- medium = All codes excluding IJK, but there is a space between each 'word'.
- hard = All codes excluding IJK, and spaces between 'words' are eliminated also.
- Or a custom selection of codes from the official list of `ABCDFGHLMNPRSTXYZ` (i.e. excluding IJK) and the 'others' `EOQUV`.

`--tolerance` accepts a value to use when 'clipping' all arguments, with the exception of I, J or K.
  - 0.00005 to 0.05 for inches or
  - 0.005 to 0.5 for millimeters

`--arcTolerance` accepts a value to use to 'simplify' very short arcs to lines.
  - 0.00005 to 0.05 for inches or
  - 0.005 to 0.5 for millimeters

`--zClamp` accepts a value to 'clamp' all positive z-axis values.
  - 0.02 to 0.5 for inches or
  - 0.5 to 10.0 for millimeters

For the tolerance and clamp values, the smallest value (inch or mm specific) is used as the default value.

Now find yourself a gcode (`.nc`, `.gcode`, etc.) file to use for the option `--filename <filename>`.
And replace `<filename>` with the full path to your gcode file (as per what your OS requires).

The GCodeClean `GCC` will require Read access to that file, and Write access to the folder where that file is located.

And then run the `GCC` executable.
e.g. for Windows that might look like:
```
.\gcc --filename FacadeFullAlternate.nc --minimise soft --annotate
```

or for Linux (Ubuntu 18.04 / 20.04)
```
./GCC --filename FacadeFullAlternate.nc --minimise hard --annotate
```

After processing the GCodeClean `GCC` will report the number of lines that it output.
The output file will have `-gcc` appended to name of the input file (but before the file extension) that you provided on the command line.

Note: If the input file does not exist (or can't be found, i.e. your typo) then GCodeClean `GCC` will fail, but it won't do any harm.

`Exit code`:
* `0` - Success
* some other number - some exception, for example file / folder not found - check for typos etc.


#### The `split` Command

Repeat the above `--help` command, but specify `split` as the command (in other words type in `.\gcc split --help`) and you'll get back the following:

```
Description:
  Split your GCode file into individual cutting actions.

Usage:
  GCC split [options]

Options:
  --filename <filename> (REQUIRED)  Full path to the input filename
  -?, -h, --help                    Show help and usage information
```

`split` assumes the `filename` provided, is for a GCode file that has already been `clean`ed. It will create a folder with the same name as `filename` but minus the filename extension (`.nc` etc.). And within that folder it will create one individual file for each cutting path in the original file.

Each individual file should be a valid GCode file that can be run independently.

The name of these files will be made up of several parts, each part of the filename is delimited with an underscore `_`.
For a filename like `0_1_17_2_X558.657Y373.418_X563.676Y407.742_gcc.nc` you can understand the parts of it as follows:
* `0_` - The main 'sequence' number this cutting path belongs to. You can think of this as which tool change has occurred. So this would be the first (`0` based index) tool change.
* `1_` - The 'sub-sequence' number. The range of cutting depths from the shallowest to the deepest for the entire original file is divided into 10 groupings, and then each cutting path is assigned to one of these groupings depending on its maximum cutting depth. Files in the same 'sequence' and 'sub-sequence' will be grouped together when any merge is performed.
* `17_` - The original index for this cutting path, i.e. its order in the original file.
* `2_` - The tool name/number for this cutting path. You may see 'notset' if no tool was defined.
* `X558.657Y373.418_` - The starting coordinates for this cutting path.
* `X563.676Y407.742_` - The finishing coordinates.
* `gcc.nc` - yep it is a GCode (nc) file that GCodeClean has messed with.


#### The `merge` Command

Repeat the above `--help` command, but specify `merge` as the command (in other words type in `.\gcc merge --help`) and you'll get back the following:

```
Description:
  Merge a folder of files, produced by split, back into a single GCode file.

Usage:
  GCC merge [options]

Options:
  --folder <folder> (REQUIRED)  Full path to the input folder
  -?, -h, --help                Show help and usage information
```

`merge` assumes the `folder` provided, is for a GCode file that has already been `split`. It will examine all of the names of the files in that folder and extract from them the necessary details for it to attempt a 'better' order for each of the cutting paths (individual files). Once it has determined that it will merge all of the files back together in that 'better' order.

The output file will have `-ts.nc` appended to name of the input folder that you provided on the command line.

## What's Special about GCodeClean?

GCodeClean uses async streaming throughout from input to output. Hopefully this should keep memory consumption and the number of threads to a minimum regardless of what OS / Architecture you use.

### GCode Linting

The GCode specification allows a lot of flexibility, for example the single letter 'codes' at the start of each 'word' can be upper or lower case, and spaces are allowable after the 'code' and before the number. The specification also allows for a lot of assumptions about the 'state' of a machine when it starts processing a given GCode file.

However, certain conventions have arisen in how GCode should be presented. There are also strict guidelines within the GCode specification as regards the execution order of various commands when they appear on the same line.

GCodeClean's linting approach is to respect those conventions while prioritising the execution order, and deliberately injecting commands to turn the implicit assumptions about the state of the machine into explicit assertions about what state is desired.

This means that any line that has multiple commands on it (G, M, F, S, T) will be split into multiple lines, and those lines will appear in execution order.

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
Z2
(Preamble completed by GCodeClean)
G0 X39.29 Y-105.937
```

## Authors

* **Lee HUMPHRIES** - *Initial work*, and *everything else* - [md8n](https://github.com/md8n)

## License

This project is licensed under the AGPL License - see the [LICENSE](LICENSE) file for details

## Acknowledgments

* To all those comments I picked up out of different people's posts in Stack Overflow
* The quality info on C# 8, and IAsyncEnumerable (which came out with C# 5) were invaluable
* All the sample GCode files provided by the Maslow CNC community [Maslow CNC](https://forums.maslowcnc.com/)
