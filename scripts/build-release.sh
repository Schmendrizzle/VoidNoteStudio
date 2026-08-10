#!/usr/bin/env sh
set -eu
export AVALONIA_TELEMETRY_OPTOUT=1
root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
artifacts="$root/artifacts/release"
solution="$root/VoidNoteStudio.sln"
app="$root/src/VoidNote.App/VoidNote.App.csproj"
version=$(python3 -c 'import xml.etree.ElementTree as E,sys; r=E.parse(sys.argv[1]).getroot(); p=r.find("PropertyGroup"); print(p.findtext("VersionPrefix")+"-"+p.findtext("VersionSuffix"))' "$root/Directory.Build.props")

dotnet clean "$solution" --configuration Release
dotnet restore "$solution"
dotnet build "$solution" --configuration Release --no-restore
dotnet test "$solution" --configuration Release --no-build --no-restore
mkdir -p "$artifacts"

for runtime in win-x64 linux-x64; do
  publish="$artifacts/$runtime"
  rm -rf -- "$publish"
  dotnet publish "$app" --configuration Release --runtime "$runtime" --self-contained false --output "$publish" -p:DebugType=None -p:DebugSymbols=false
  find "$publish" -type f -name '*.pdb' -delete
  cp "$root/README.md" "$root/THIRD_PARTY_NOTICES.md" "$root/LICENSE" "$publish/"
  python3 "$root/scripts/validate-package.py" "$publish"
  dotnet "$publish/VoidNote.App.dll" --version
done

python3 -c 'import pathlib,shutil,sys; p=pathlib.Path(sys.argv[1]); shutil.make_archive(str(p/("VoidNote-Studio-"+sys.argv[2]+"-win-x64")), "zip", p/"win-x64")' "$artifacts" "$version"
tar -czf "$artifacts/VoidNote-Studio-$version-linux-x64.tar.gz" -C "$artifacts/linux-x64" .
printf '%s\n' "Release artifacts: $artifacts"
