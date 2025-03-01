#! /usr/bin/env bash

if [[ $# -eq 0 ]] ; then
  echo "Please provide a version number, like 1.0.0, as an argument."
  exit 1
fi

git push

rm -r bin/Release/net8.0
dotnet publish --runtime linux-x64
dotnet publish --runtime win-x64
dotnet publish --runtime osx-x64

cd bin/Release/net8.0
tar --create --gzip --file linux-x64.tar.gz linux-x64
zip --recurse-paths win-x64.zip win-x64
tar --create --gzip --file osx-x64.tar.gz osx-x64

cd ../../..

gh release create "v$1" \
  "bin/Release/net8.0/linux-x64.tar.gz" \
  "bin/Release/net8.0/win-x64.zip" \
  "bin/Release/net8.0/osx-x64.tar.gz" \
  --repo JanSharp/TwitchVodsRescueCS \
  --target main \
  --notes "There are no notes here. No nothing. It's empty. A vast void. Except for 3 files to download, those do exist." \
  --title "v$1"
git pull --tags
