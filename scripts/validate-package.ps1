param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Directory).Path
$errors = [System.Collections.Generic.List[string]]::new()
foreach ($relative in @('VoidNote.App.dll', 'workers/python/voidnote_ai_worker.py', 'README.md', 'THIRD_PARTY_NOTICES.md')) {
    if (-not (Test-Path (Join-Path $root $relative) -PathType Leaf)) { $errors.Add("Missing required file: $relative") }
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
Write-Host "Package validation passed: $root"
