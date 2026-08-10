param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/release'
$app = Join-Path $root 'src/VoidNote.App/VoidNote.App.csproj'
$buildProps = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
$version = $buildProps.Project.PropertyGroup.VersionPrefix + '-' + $buildProps.Project.PropertyGroup.VersionSuffix

dotnet clean (Join-Path $root 'VoidNoteStudio.sln') --configuration Release
dotnet restore (Join-Path $root 'VoidNoteStudio.sln')
dotnet build (Join-Path $root 'VoidNoteStudio.sln') --configuration Release --no-restore
if (-not $SkipTests) { dotnet test (Join-Path $root 'VoidNoteStudio.sln') --configuration Release --no-build --no-restore }

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
foreach ($runtime in @('win-x64', 'linux-x64')) {
    $publish = Join-Path $artifacts $runtime
    if (Test-Path $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    dotnet publish $app --configuration Release --runtime $runtime --self-contained false --output $publish -p:DebugType=None -p:DebugSymbols=false
    Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb' | Remove-Item -Force
    Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'THIRD_PARTY_NOTICES.md'), (Join-Path $root 'LICENSE') -Destination $publish
    & (Join-Path $root 'scripts/validate-package.ps1') -Directory $publish
    dotnet (Join-Path $publish 'VoidNote.App.dll') --version
}

$windowsArchive = Join-Path $artifacts "VoidNote-Studio-$version-win-x64.zip"
$windowsArchiveTemporary = Join-Path $artifacts ("VoidNote-Studio-$version-win-x64." + [Guid]::NewGuid().ToString('N') + '.zip')
Compress-Archive -Path (Join-Path $artifacts 'win-x64/*') -DestinationPath $windowsArchiveTemporary
if (Test-Path $windowsArchive) {
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        try { [System.IO.File]::Delete($windowsArchive); break }
        catch { if ($attempt -eq 4) { throw }; Start-Sleep -Milliseconds 200 }
    }
}
[System.IO.File]::Move($windowsArchiveTemporary, $windowsArchive)
tar -czf (Join-Path $artifacts "VoidNote-Studio-$version-linux-x64.tar.gz") -C (Join-Path $artifacts 'linux-x64') .
Write-Host "Release artifacts: $artifacts"
