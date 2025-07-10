# DAT1 GUI

**DAT1 GUI** is a standalone toolkit for working with mod files used in PC ports of Insomniac Games titles.  
It is a fork of Tkachov’s original *Modding Tool*, enhanced with additional tools and improvements.

## Features

- Robust Project System for restoring your work
- Custom scripting system using sandboxed python
- Texture Viewer
- Config Editor
- Names for `.wem` files
- Built in `.wem` player
- Revamped User Interface
- Automatic downloads of hashes for each of the supported games 
- Various Quality of Life Features

## Requirements

- [.NET 7.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

## Usage

1. Download the latest release from the [Releases](./releases) tab.  
2. Launch `DAT1-GUI.exe`.  
3. Open supported files using the File menu.  

## Contributions

If you want to contribute, you're very welcome!

No contribution is too small. If you've found a bug, or have a suggestion, or want to write a guide for other users — feel free to help the way you can.

Of course, I'd be glad to accept code changes. If you can fix a bug, or implement a feature you've always wanted, or write a tool based on the code here, don't hesitate to send a PR or make a fork.

You can start by looking at the Issues page, where you can create a new one if it's a bug or suggestion, or find one you'd like to help with.

See Building page for information on how to build the source code on your machine.

## Building from Source

See [BUILDING.md](./BUILDING.md) for instructions on how to build the project.

## Acknowledgements

This project uses and builds upon the hard work of others:

- Thanks to the [vgmstream](https://vgmstream.org/) project for their outstanding audio decoding library used to decode `.wem` audio files.
- Thanks to [Tkachov](https://github.com/Tkachov/Overstrike) for creating the original *Modding Tool*, which this project forks and enhances.

## License

This project is licensed under [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html).  
Modified versions must also be open source under the same license.
