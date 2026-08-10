param(
    [Parameter(Mandatory=$true)][string]$Directory,
    [Parameter(Mandatory=$true)][ValidateSet('win-x64', 'linux-x64')][string]$Runtime
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Directory).Path
$errors = [System.Collections.Generic.List[string]]::new()
$nativeRuntimeFiles = if ($Runtime -eq 'win-x64') {
    @('VoidNote.App.exe', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll')
} else {
    @('VoidNote.App', 'libcoreclr.so', 'libhostfxr.so', 'libhostpolicy.so')
}
foreach ($relative in @('VoidNote.App.dll', 'VoidNote.App.deps.json', 'VoidNote.App.runtimeconfig.json', 'System.Private.CoreLib.dll', 'workers/python/voidnote_ai_worker.py', 'README.md', 'THIRD_PARTY_NOTICES.md') + $nativeRuntimeFiles) {
    if (-not (Test-Path (Join-Path $root $relative) -PathType Leaf)) { $errors.Add("Missing required file: $relative") }
}
$depsPath = Join-Path $root 'VoidNote.App.deps.json'
if (Test-Path $depsPath -PathType Leaf) {
    try {
        $deps = Get-Content -Raw -Encoding utf8 -LiteralPath $depsPath | ConvertFrom-Json
        $runtimeTarget = [string]$deps.runtimeTarget.name
        if (-not $runtimeTarget.EndsWith("/$Runtime", [StringComparison]::Ordinal)) { $errors.Add("Publish metadata runtime target is not ${Runtime}: $runtimeTarget") }
        $runtimePackPrefix = "runtimepack.Microsoft.NETCore.App.Runtime.$Runtime/"
        if (-not ($deps.libraries.PSObject.Properties.Name | Where-Object { $_.StartsWith($runtimePackPrefix, [StringComparison]::Ordinal) })) {
            $errors.Add("Publish metadata does not contain the .NET runtime pack for $Runtime")
        }
    } catch { $errors.Add("Cannot read publish dependency metadata: $($_.Exception.Message)") }
}
$runtimeConfigPath = Join-Path $root 'VoidNote.App.runtimeconfig.json'
if (Test-Path $runtimeConfigPath -PathType Leaf) {
    try {
        $runtimeOptions = (Get-Content -Raw -Encoding utf8 -LiteralPath $runtimeConfigPath | ConvertFrom-Json).runtimeOptions
        if ($null -ne $runtimeOptions.framework -or $null -ne $runtimeOptions.frameworks) { $errors.Add('Runtime config still requests a globally installed shared framework') }
        if (-not ($runtimeOptions.includedFrameworks | Where-Object { $_.name -eq 'Microsoft.NETCore.App' -and ([string]$_.version).StartsWith('10.', [StringComparison]::Ordinal) })) {
            $errors.Add('Runtime config does not record an included .NET 10 framework')
        }
    } catch { $errors.Add("Cannot read runtime config metadata: $($_.Exception.Message)") }
}
$forbiddenDirectories = @('.git', 'bin', 'obj', 'TestResults', 'models', '.venv', 'venv')
$forbiddenExtensions = @('.log', '.pt', '.pth', '.onnx', '.safetensors', '.user', '.suo', '.pdb')
$textExtensions = @('.json', '.xml', '.config', '.md', '.txt', '.axaml', '.xaml', '.py', '.sh', '.ps1')
foreach ($item in Get-ChildItem -LiteralPath $root -Recurse -Force) {
    $relative = $item.FullName.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (($relative -split '[\\/]') | Where-Object { $forbiddenDirectories -contains $_ }) { $errors.Add("Forbidden package content: $relative") }
    if (-not $item.PSIsContainer -and $forbiddenExtensions -contains $item.Extension.ToLowerInvariant()) { $errors.Add("Forbidden package content: $relative") }
    if (-not $item.PSIsContainer -and $item.Length -lt 2000000 -and ($textExtensions -contains $item.Extension.ToLowerInvariant() -or $item.Name -eq 'LICENSE')) {
        try {
            $content = Get-Content -Raw -Encoding utf8 -LiteralPath $item.FullName -ErrorAction Stop
            if ($content -match '(?i)(C:\\Users\\[^\\\s]+|/home/[^/\s]+|/Users/[^/\s]+)') { $errors.Add("Possible personal absolute path: $relative") }
        } catch { }
    }
}
if ($errors.Count -gt 0) { throw ($errors -join [Environment]::NewLine) }
Write-Host "Self-contained package validation passed for ${Runtime}: $root"
