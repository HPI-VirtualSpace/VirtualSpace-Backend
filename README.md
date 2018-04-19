# VirtualSpaceBackend

## How to Build

### Setup

To compile the project, you need to install the .NET Core tools from [here](https://www.microsoft.com/net/core#windowsvs2015) (The download labeled "Install the .NET Core tools preview for Visual Studio").

Afterwards, run `dotnet restore` in the base directory.

### Building and running with Visual Studio Code

The selected launch configuration can be changed in debug view (Ctrl+Shift+D). Afterwards, build and start by pressing F5.

If you want to build without running, use either the default build target (Ctrl+Shift+B), or select a target using the command palette.

### Building and running from command line

For building, run `dotnet build` followed by `Backend` and/or `Bots` in the base directory.

Run the executables by either calling `dotnet run` inside the respective project directory, or by calling run.bat which starts both the backend and the bots.