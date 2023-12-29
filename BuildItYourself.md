# GCodeClean - Building It Yourself

## Before We Get Started

There are standalone release builds available, for Linux, Raspberry Pi (linux-arm), and Windows at [GCodeClean releases](https://github.com/md8n/GCodeClean/releases). It is very easy to a build for MacOS / OSX (osx-64 / osx-arm) (see #Deployment below).

But you can build and run this project yourself, and for that you would need the .NET 8.0 SDK.

And if you do build it yourself then there are a very large number of possible targets including 32bit, and many specific Linux distros, etc.

## Prerequisites for Building it Yourself

.NET 8.0 SDK - get the correct version for your OS and architecture here: [.NET SDK downloads](https://dotnet.microsoft.com/download/)

A text editor if you want to change something. I recommend Visual Studio Code, or alternatively go the whole hog and use full Visual Studio.
The 'community edition' of Visual Studio is free. But anything so long as you can edit text files with it is fine.

## Building and Running it Yourself

Once you've got the .NET 8.0 SDK installed.

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

Or you can build the `cli` and run it with these two steps:
```
dotnet publish -p:PublishProfile=<profile-name>
```
Take a note of the final line output that starts `CLI ->`, the `GCC` executable will be located there.

And then run the `GCC` executable.
e.g. for Windows that might look like:
```
.\bin\Debug\net8.0\publish\gcc clean --filename FacadeFullAlternate.nc --minimise hard --annotate
```

or for Linux (Ubuntu 18.04 / 20.04 / 22.04)
```
./bin/Debug/net8.0/publish/GCC clean --filename FacadeFullAlternate.nc --minimise hard --annotate
```

## GCodeClean Solution Organisation

GCodeClean is organised into 3 projects:
1. GCodeClean - A library that contains most of the code
2. CLI - An executable to call the above library - this also handles the command line arguments and does the actual file handling
3. CodeClean.Test - A test suite - which will be 'grown' over time.

## Deployment

Run a fresh 'self-contained' `publish` of `GCodeClean` as follows:
```
dotnet publish -p:PublishProfile=<profile-name>
```
Where `<profile-name>` is one of `linux-arm`, `linux-x64`, `win-x64`, or `osx-x64`
Take a note of the final line output that starts `CLI ->`, the `GCC` executable will be located in that folder.

Copy the contents of the folder above to wherever you need them. Assuming that it is the same OS / Architecture of course.

The `dotnet restore` command above gets the runtimes for `linux-x64`, `linux-arm`, `win-x64`. But there are many, many more options. If you choose a different option then you may need to allow time for the `restore` of that runtime before publishing can actually take place.

## Authors

* **Lee HUMPHRIES** - *Initial work*, and *everything else* - [md8n](https://github.com/md8n)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
