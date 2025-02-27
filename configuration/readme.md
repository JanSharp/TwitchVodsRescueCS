
TODO: fix docs, using just the csv file now

This folder contains:

- required: `details.txt`
- required: `vods_filtered_xxxx-xx-xx_xx-xx-xx.csv` (it specifically looks for any `.csv` file, there must only be one)
- optional: `collections/` folder containing text files

**There is a complete example setup in the `example_configuration` folder.**

- For the [details.txt](../example_configuration/details.txt) file
  - go to https://twitch-tools.rootonline.de/vod_manager.php
  - log in
  - let it load
  - control A
  - control C
  - open the [details.txt](../example_configuration/details.txt) file
  - control A
  - control V
  - remove the header, all the way to and including the "Found ### videos." line
- For the [vods_filtered_xxxx-xx-xx_xx-xx-xx.csv](../example_configuration/vods_filtered_2025-02-19_20-39-36.csv) file
  - go to https://twitch-tools.rootonline.de/vod_manager.php
  - log in
  - let it load
  - press "Export video URL list"
  - put the file in the `configuration` folder. Again what matters is that there's exactly 1 `.csv` file in the folder
- For collections
  - Go to https://dashboard.twitch.tv
  - Go to Content
  - Go to Collections
  - click on a collection
  - control A
  - control C
  - create a file in `configuration/collections`, the file name (excluding file extension) will be treated as the collection name (doesn't have to match the one you have on twitch)
  - open it
  - control V
  - remove the header, all the way to and including the "## of 100 videos added to collection" line
  - repeat that for each collection
