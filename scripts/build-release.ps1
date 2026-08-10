param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/release'
$app = Join-Path $root 'src/VoidNote.App/VoidNote.App.csproj'
$packager = Join-Path $root 'tools/VoidNote.Packaging/VoidNote.Packaging.csproj'
$buildProps = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
$version = $buildProps.Project.PropertyGroup.VersionPrefix + '-' + $buildProps.Project.PropertyGroup.VersionSuffix

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code ${LASTEXITCODE}: dotnet $($args -join ' ')" }
}

Invoke-DotNet clean (Join-Path $root 'VoidNoteStudio.sln') --configuration Release
Invoke-DotNet restore (Join-Path $root 'VoidNoteStudio.sln')
Invoke-DotNet build (Join-Path $root 'VoidNoteStudio.sln') --configuration Release --no-restore
if (-not $SkipTests) { Invoke-DotNet test (Join-Path $root 'VoidNoteStudio.sln') --configuration Release --no-build --no-restore }

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
foreach ($runtime in @('win-x64', 'linux-x64')) {
    $publish = Join-Path $artifacts $runtime
    if (Test-Path $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    Invoke-DotNet publish $app --configuration Release -p:PublishProfile="$runtime-portable" --output $publish
    Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb' | Remove-Item -Force
    Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'THIRD_PARTY_NOTICES.md'), (Join-Path $root 'LICENSE') -Destination $publish
    & (Join-Path $root 'scripts/validate-package.ps1') -Directory $publish -Runtime $runtime
    $appHost = if ($runtime -eq 'win-x64') { Join-Path $publish 'VoidNote.App.exe' } else { Join-Path $publish 'VoidNote.App' }
    $hostMatchesRuntime = if ($runtime -eq 'win-x64') {
        [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    } else {
        [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
    }
    if ($hostMatchesRuntime) {
        & $appHost --version
        if ($LASTEXITCODE -ne 0) { throw "$appHost --version failed with exit code $LASTEXITCODE" }
    }
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
Invoke-DotNet run --project $packager --configuration Release -- (Join-Path $artifacts 'linux-x64') (Join-Path $artifacts "VoidNote-Studio-$version-linux-x64.tar.gz")
Write-Host "Release artifacts: $artifacts"
