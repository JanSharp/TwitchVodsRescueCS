
# Twitch Vods Rescue

This is a command line tool which can download vods (highlights and uploads), their thumbnails and chat histories from twitch.

The program uses the [TwitchDownloaderCLI](https://github.com/lay295/TwitchDownloader), and requires a bit of manual configuration telling it the list of all your videos, and if you'd like you can also tell it which videos are in which collection, which is just a bit of manual work per collection. Nothing major, but it is a form of tedium and it is optional.

# Installation

- Download the latest [Release](https://github.com/JanSharp/TwitchVodsRescueCS/releases) for your platform
- Extract it
- Download the latest [TwitchDownloaderCLI Release](https://github.com/lay295/TwitchDownloader/releases) for your platform and architecture
  - Linux Note: You may also be able to use the package manager, like on Arch you can `yay -S twitch-downloader-bin`. Similarly you can definitely use your package manager to download `ffmpeg` though you very very most likely already have it installed anyway
- Extract it
- Move all the extracted files into the same folder that the previously extracted `TwitchVodsRescueCS` executable is in
- Open a terminal / command line in the folder with the `TwitchVodsRescueCS` executable in it
  - On Windows 11 right click in the folder and there should be a "Open in Terminal"
  - On Windows 10 or lower:
    - Search for "command line" in the windows search bar
    - Open that
    - Type `cd ` ("cd" and 1 white space)
    - Drag the folder that contains the `TwitchVodsRescueCS` executable (not the executable itself) from your file explorer into the command line window
    - Hit enter
  - On Linux there should be a "Open Terminal Here" option when right clicking in a folder in basically every file explorer
- Run the command `dotnet sdk check`
  - It will either:
    - Fail entirely, in which case you don't have any `dotnet` installed
    - Or it will print a list of installed SDKs and Runtimes. In which case check under `.Net Runtimes:` if it lists `Microsoft.NETCore.App  8.0.x`
    - If you are missing dotnet 8 then go to https://dotnet.microsoft.com/en-us/download/dotnet/8.0 and download the dotnet 8 runtime (just the `.Net Runtime`, no need for `.Net Desktop Runtime`)
- Run the command `ffmpeg`
  - If it prints out version info and a bunch of other random help text then you're good
  - If it says no such program exists run the command `./TwitchDownloaderCLI ffmpeg --download`
- Run `./TwitchVodsRescueCS --help`
- You can read through the help message to get an idea of everything the program can do

## Configuration Setup

- Create a `configuration` folder (all lower case) in the folder that the `TwitchVodsRescueCS` executable is in. Aka "next to"
- Go to https://twitch-tools.rootonline.de/vod_manager.php
- Log in via Twitch
- Let it load
- Do not apply any filters
- Click Export video list as CSV
- Put the downloaded csv file into the newly created `configuration` folder

## Collections Setup

- In the `configuration` folder create a `collections` folder (all lower case)
- Go to https://dashboard.twitch.tv
- Go to Content
- Go to Collections
- Click on a collection
- Control + A
- Control + C
- Create a text file in `configuration/collections`, the file name (excluding file extension - so excluding `.txt`) will be treated as the collection name (doesn't have to match the one you have on twitch)
- Open the file
- Control + V
- Control + S
- Close the file
- Repeat that for each collection

# Usage

- Open a terminal in the folder that contains the `TwitchVodsRescueCS` executable (the same way as described in [Installation](#installation))
- If we assume the drive you'd like to save vods to is the `G:` drive then:
- Linux note: Lucky you, you don't have to think about drive letters! Just use whatever folder path you mounted the drive to
- Run the command `./TwitchVodsRescueCS --temp-dir "G:/Temp" --output-dir "G:/TwitchVods" --download-video --download-thumbnails --download-chat --dry-run`
- This will try to tell you what it would do if that command was run without the `--dry-run` (note that thumbnail downloads likely won't be in the dry run output, because reasons)
- Use the up arrow key to quickly get the same command you just ran to be able to edit it
- Remove `--dry-run` and it will start downloading
- Once the program is running, press `Q` to request it to stop after it finished processing the current vod, and `Q` again to cancel the stop request
- You can always add `--help` to the command to see the list of all options
- In regards to downloading specific collections
  - Use the `--list-collections` option
  - Copy some collection name
  - Remove `--list-collections` from the command
  - Add `--collections "Collection Name"`
  - To download multiple collections do `--collections "Collection Name One" --collections "Collection Name Two"`
  - To download videos which aren't in any collections use `--non-collections`
- Other notable options may be
  - `--newest-first` which reverses download order
  - `--time-limit` which limits how long the program should run in minutes
  - `--list-duplicate-titles`, `--list-videos`, `--list-videos-in-multiple-collections`
  - `--help` you cannot stop me from telling you to read the help message
- `--validate-videos` validates all downloaded videos to make sure they actually fully downloaded
  - If some video didn't get downloaded fully and trying to play it back on twitch at the time stamp where it fails to play in the downloaded video and it ends up also failing on twitch, then you'll have to find the video in the video producer list and use the download feature there
  - Requires the `ffprobe` tool
    - On linux install it through your package manager, it maybe probably already comes with ffmpeg
    - On windows either somehow install it system wide or
      - Go to https://ffbinaries.com/downloads
      - Download latest `ffprobe`
      - Extract it and put it in the same folder as this executable
- `split` sub command
  - Requires `ffprobe`, see instructions above
  - Requires `ffmpeg`, see instructions in [Installation](#installation)
  - Extracts parts of videos without re-encoding them
    - `--file` to specify the input video file
    - One or more `--parts`, each specifying a time frame to extract out of the input video
    - Use `--help` for format instructions
    - Use `--dry-run` to see what files it would create
  - Example: `TwitchVodRescueCS split --file "video.mp4" --parts 0:00-1:34:00`

## Videos in Multiple Collections

The program never downloads the same video twice. If a video is in multiple collections the program will chose one collection folder to download the video into (should be the first one when sorted alphabetically, case insensitive) and all other collections will just receive a metadata json file with the `(extern)` marker. The metadata file contains the collection name that the video is actually saved in.

# Libraries, Dependencies and Licenses

This project itself is licensed under the MIT License, see [LICENSE.txt](LICENSE.txt).

<!-- cSpell:ignore Mischak, justarandomgeek, justchen1369 -->

- [TwitchDownloader](https://github.com/lay295/TwitchDownloader) Copyright (c) lay295

For license details see the [LICENSE_THIRD_PARTY.txt](LICENSE_THIRD_PARTY.txt) file and or the linked repositories above.
