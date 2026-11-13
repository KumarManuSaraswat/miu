param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.1', [string]$OutputDirectory = 'artifacts/ready/1.0.1')
$ErrorActionPreference = 'Stop'
$selectedVersion = $Version; $selectedOutput = $OutputDirectory
. (Join-Path $PSScriptRoot 'verify-release.ps1') -DefinitionsOnly
$Version = $selectedVersion; $OutputDirectory = $selectedOutput
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = Resolve-MiuReleasePath $root $OutputDirectory
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count) { throw 'Use an empty output directory. Release artifacts are never overwritten.' }
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $root 'dist/Miu.exe')).Version.ToString() -ne ($Version + '.0')) { throw 'Build the matching version first.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$inputs = @{}; $hashes = @{}
foreach ($name in @(Get-MiuReleaseFiles 'windows-x64')) {
    $source = if ($name.StartsWith('dist/docs/')) { $name.Substring(5) } else { $name }
    $path = Resolve-MiuReleasePath $root $source
    Assert-MiuPublicBytes $name ([IO.File]::ReadAllBytes($path))
    $inputs[$name] = $path
    $hashes[$name] = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
}
# In-app Markdown relative links must resolve beside dist/docs too.
foreach ($name in @($inputs.Keys | Where-Object { $_.StartsWith('licenses/') })) {
    $inputs['dist/' + $name] = $inputs[$name]; $hashes['dist/' + $name] = $hashes[$name]
}
foreach ($line in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'windows-bundle.sha256')) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw 'Malformed bundle manifest.' }
    $hash = $Matches[1]; $name = $Matches[2]
    if ($inputs.ContainsKey($name)) { throw 'Duplicate bundle input.' }
    $origin = if ($name.StartsWith('dist/local-ai/') -or $name.StartsWith('dist/models/')) { $root } else { Join-Path $root '.cache/windows-speech-bundle' }
    $path = Resolve-MiuReleasePath $origin $name
    if ((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $hash) { throw ('Bundle hash mismatch: ' + $name) }
    $inputs[$name] = $path; $hashes[$name] = $hash
}
if (-not $inputs.ContainsKey('dist/models/gemma3-1b.gguf') -or -not $inputs.ContainsKey('dist/voice/libvosk.dll') -or -not $inputs.ContainsKey('dist/natural-voice/piper/piper.exe')) { throw 'Ready-to-use runtime inputs missing.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$base = 'miu-' + $Version + '-windows-x64'
$output = Join-Path $destination ($base + '.zip')
$file = [IO.File]::Open($output, [IO.FileMode]::CreateNew)
try {
    $zip = New-Object IO.Compression.ZipArchive($file, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in @($inputs.Keys | Sort-Object -CaseSensitive)) {
            $level = if ($name.EndsWith('.zip') -or $name.EndsWith('.gguf')) { [IO.Compression.CompressionLevel]::NoCompression } else { [IO.Compression.CompressionLevel]::Optimal }
            $entry = $zip.CreateEntry($base + '/' + $name, $level)
            $entry.LastWriteTime = New-Object DateTimeOffset(2000,1,1,0,0,0,[TimeSpan]::Zero)
            $input = [IO.File]::OpenRead($inputs[$name]); $stream = $entry.Open()
            try { $input.CopyTo($stream, 131072) } finally { $input.Dispose(); $stream.Dispose() }
        }
        $manifest = (@($hashes.Keys | Sort-Object -CaseSensitive | ForEach-Object { $hashes[$_] + '  ' + $_ }) -join "`n") + "`n"
        $entry = $zip.CreateEntry($base + '/MANIFEST.sha256')
        $entry.LastWriteTime = New-Object DateTimeOffset(2000,1,1,0,0,0,[TimeSpan]::Zero)
        $stream = $entry.Open(); $bytes = [Text.Encoding]::UTF8.GetBytes($manifest)
        try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    } finally { $zip.Dispose() }
} finally { $file.Dispose() }
# Stream verification keeps packaging memory bounded even with a large model.
$zip = [IO.Compression.ZipFile]::OpenRead($output)
try {
    if ($zip.Entries.Count -ne $inputs.Count + 1) { throw 'Unexpected ZIP inventory.' }
    foreach ($name in $inputs.Keys) {
        $entry = $zip.GetEntry($base + '/' + $name)
        if (-not $entry) { throw ('Missing ZIP entry: ' + $name) }
        $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($hash -cne $hashes[$name]) { throw ('ZIP content mismatch: ' + $name) }
    }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $output).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), $hash + '  ' + [IO.Path]::GetFileName($output) + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host ('Verified ready-to-use Windows bundle: ' + $output)
