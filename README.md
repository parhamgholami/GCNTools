# GCNTools
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/parhamgholami/GCNTools/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/GCNTools.svg)](https://www.nuget.org/packages/GCNTools)

GCNTools is a simple, platform-agnostic C# .NET library for working with GameCube discs, including reading, extracting, and creating disc images.

## Background
This library is a byproduct of my work on [Konga Launcher](http://kongalauncher.com), a custom track manager for Donkey Konga 2 and 3. I needed a method for manipulating the user's copy of Donkey Konga and building a new disc image if they wanted to create a new version of the game with custom tracks. 

Because Konga Launcher already required Dolphin to launch the game from the user's computer, I originally planned on leveraging Dolphin Tools to handle the disc images. However, I soon discovered that Dolphin Tools did not come by default with the Mac and Linux versions of Dolphin, which meant I needed to take a different approach.

Thanks to the community's documentation, I put together my implementation in reasonably short order. Since the work to make this library for Konga Launcher has already been done, I figured sharing it with the community wouldn't hurt. I'm not sure how much demand there is for this library. That said, I still wanted to share GCNTools in case anyone finds it useful. If this library helped you, I would love to hear about it!

## Features
- Read and extract GameCube disc images
- Create new disc images from extracted files
- Read and modify disc metadata (title, region, maker code, etc.)
- Handle game banners and multi-language banner metadata

## Requirements
- .NET 8.0 or higher

## Usage

### Installation
GCNTools is available via NuGet. You can install it into your project with the following command:

```
dotnet add package GCNTools
```

#### Example
Extracting a disc image:
```C#
using GCNTools;

using FileStream gameIso = new("C:/games/mygame.iso");
using DiscImage myGameImage = new(gameIso);

// Extract everything
myGameImage.ExtractToDirectory("C:/extractedgames/mygame", ExtractionType.ALL);

// Extract only system files (boot.bin, bi2.bin, apploader.img, etc.)
myGameImage.ExtractToDirectory("C:/extractedgames/mygame", ExtractionType.SYSTEM_DATA_ONLY);

// Extract only game files
myGameImage.ExtractToDirectory("C:/extractedgames/mygame", ExtractionType.FILES_ONLY);
```

Modifying a disc image's header information and saving the changes as a new file:
```C#
using GCNTools;

using FileStream gameIso = new("C:/games/mygame.iso");
using DiscImage myGameImage = new(gameIso);
myGameImage.Title = "New Game";
myGameImage.Region = Region.NTSC_J;
myGameImage.SaveToFile("C:/games/mymodifiedgame.iso");
```

Creating a disc image from an already extracted disc image without instantiating an object:
```C#
using GCNTools;

DiscImage.CreateFile("C:/extractedgames/mygame", "C:/modifiedgames/mygame.iso");
```

## Roadmap
I do not have any specific plans yet, but I would like to expand on GCNTools to include the following:

- Asynchronous support
- Data validation
- Comprehensive error handling
- Expanded test coverage
- Additional GameCube-related features

## License

The GCNTools repository and associated NuGet package are available under the MIT license. With this in mind, attribution is appreciated but not required or expected. 

## Contribution

Contributions and bug reports are always welcome. Please keep in mind this is a hobbyist project, so I may need time to review any pull requests, bug reports, or general feedback.

## Acknowledgements

This project would not have been possible without the GameCube homebrew community. [Yet Another Gamecube Documentation](https://www.gc-forever.com/yagcd/) provided essential technical information and [Swiss](https://github.com/emukidid/swiss-gc) was instrumental in the testing process.