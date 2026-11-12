param([switch]$SelfTest, [switch]$CheckCache)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression

$miuRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$miuDestination = Join-Path $miuRoot 'dist\voice'
$miuCache = Join-Path $miuRoot '.cache\voice'

function Assert-VoicePath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($miuRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Voice paths must stay inside the project.' }
    $ancestor = $resolved
    while ($ancestor -and $ancestor.Length -ge $miuRoot.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw 'Voice paths cannot contain symlinks or junctions.'
        }
        if ($ancestor -eq $miuRoot) { break }
        $ancestor = Split-Path -Parent $ancestor
    }
}

function Assert-VoiceEntries([object[]]$Entries, [string]$Destination, [long]$Limit = 512MB) {
    Assert-VoicePath $Destination
    $prefix = [IO.Path]::GetFullPath($Destination).TrimEnd('\', '/') + '\'
    if ($Entries.Count -eq 0 -or $Entries.Count -gt 10000) { throw 'Unexpected voice archive entry count.' }
    $names = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    foreach ($entry in $Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains(':') -or $name.StartsWith('/') -or $name -match '(^|/)\.\.?(/|$)' -or
            $name -match '[\x00-\x1f]' -or -not $names.Add($name.TrimEnd('/'))) { throw 'Unsafe or duplicate path in voice archive.' }
        if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'Voice archive contains a symlink.' }
        $target = [IO.Path]::GetFullPath((Join-Path $Destination $name.Replace('/', '\')))
        if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Voice archive attempted to leave its extraction folder.' }
        Assert-VoicePath $target
        if ($entry.Length -lt 0 -or $entry.Length -gt 256MB) { throw 'Oversized voice archive entry.' }
        $total += $entry.Length
        if ($total -gt $Limit) { throw 'Voice archive exceeds the extraction size limit.' }
    }
}

if ($SelfTest) {
    $checks = 0
    $destination = Join-Path $miuCache 'guard-check-no-extraction'
    $cases = @(
        @([pscustomobject]@{ FullName='../outside'; Length=1; ExternalAttributes=0 }),
        @([pscustomobject]@{ FullName='/outside'; Length=1; ExternalAttributes=0 }),
        @([pscustomobject]@{ FullName='file:stream'; Length=1; ExternalAttributes=0 }),
        @([pscustomobject]@{ FullName='model/a'; Length=1; ExternalAttributes=0 }, [pscustomobject]@{ FullName='model/A'; Length=1; ExternalAttributes=0 }),
        @([pscustomobject]@{ FullName='oversized'; Length=257MB; ExternalAttributes=0 }),
        @([pscustomobject]@{ FullName='link'; Length=1; ExternalAttributes=-1610612736 })
    )
    foreach ($case in $cases) {
        $rejected = $false
        try { Assert-VoiceEntries @($case) $destination } catch { $rejected = $true }
        if (-not $rejected) { throw 'Voice archive guard accepted unsafe input.' }
        $checks++
    }
    $valid = @([pscustomobject]@{ FullName='model/README'; Length=2; ExternalAttributes=0 })
    Assert-VoiceEntries $valid $destination; $checks++
    $rejected = $false
    try { Assert-VoiceEntries $valid $destination 1 } catch { $rejected = $true }
    if (-not $rejected) { throw 'Voice archive total-size guard failed.' }; $checks++
    Write-Host ('PASS: ' + $checks + ' voice installer guards; no download, extraction, or installation changes.')
    return
}

Assert-VoicePath $miuCache
Assert-VoicePath $miuDestination
if ($CheckCache) {
    foreach ($name in @('vosk-win64-0.3.45.zip', 'vosk-model-small-en-us-0.15.zip')) {
        $path = Join-Path $miuCache $name
        Assert-VoicePath $path
        if ((Get-Item -LiteralPath $path).Length -gt 128MB) { throw 'Cached voice archive exceeds its size limit.' }
        $archive = [IO.Compression.ZipFile]::OpenRead($path)
        try { Assert-VoiceEntries @($archive.Entries) (Join-Path $miuCache 'guard-check-no-extraction') }
        finally { $archive.Dispose() }
    }
    Write-Host 'PASS: cached voice archive inventories satisfy containment, symlink, duplicate, count, and size limits. No extraction performed.'
    return
}
$miuStage = Join-Path $miuCache ('install-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
$miuVoice = Join-Path $miuStage 'voice'
Assert-VoicePath $miuStage
[IO.Directory]::CreateDirectory($miuStage) | Out-Null

function Get-VoiceArchive([string]$Url, [string]$Name) {
    $archivePath = Join-Path $miuCache $Name
    Assert-VoicePath $archivePath
    if (-not (Test-Path -LiteralPath $archivePath)) {
        Write-Host "Downloading $Name from the official Vosk publisher..."
        $partialPath = Join-Path $miuStage ($Name + '.download')
        Assert-VoicePath $partialPath
        $download = New-Object Net.WebClient
        try { $download.DownloadFile($Url, $partialPath) }
        finally { $download.Dispose() }
        if ((Get-Item -LiteralPath $partialPath).Length -gt 128MB) { throw 'Voice download exceeds its expected size.' }
        Move-Item -LiteralPath $partialPath -Destination $archivePath
    }
    if ((Get-Item -LiteralPath $archivePath).Length -le 0 -or (Get-Item -LiteralPath $archivePath).Length -gt 128MB) { throw 'Invalid cached voice archive size.' }
    return $archivePath
}

function Expand-VoiceArchive([string]$ArchivePath, [string]$Destination) {
    Assert-VoicePath $ArchivePath
    Assert-VoicePath $Destination
    $destinationFull = [IO.Path]::GetFullPath($Destination)
    $destinationPrefix = $destinationFull.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $destinationFull.StartsWith($miuCache + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Voice extraction must stay inside the project cache.'
    }
    if (Test-Path -LiteralPath $destinationFull) { throw 'Voice extraction requires a new empty directory.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        Assert-VoiceEntries @($archive.Entries) $destinationFull
        [IO.Directory]::CreateDirectory($destinationFull) | Out-Null
        foreach ($entry in $archive.Entries) {
            $relativeName = $entry.FullName.Replace('/', '\')
            if ($relativeName.Contains(':') -or [IO.Path]::IsPathRooted($relativeName)) { throw 'Unsafe path in voice archive.' }
            $target = [IO.Path]::GetFullPath((Join-Path $destinationFull $relativeName))
            Assert-VoicePath $target
            if (-not $target.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Voice archive attempted to leave its extraction folder.' }
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) {
                [IO.Directory]::CreateDirectory($target) | Out-Null
            } else {
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                $inputStream = $entry.Open()
                $outputStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try {
                    $buffer = New-Object byte[] 65536
                    [long]$written = 0
                    while (($read = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $written += $read
                        if ($written -gt $entry.Length) { throw 'Voice archive expanded beyond its declared size.' }
                        $outputStream.Write($buffer, 0, $read)
                    }
                    if ($written -ne $entry.Length) { throw 'Voice archive is truncated.' }
                } finally { $outputStream.Dispose(); $inputStream.Dispose() }
            }
        }
    } finally { $archive.Dispose() }
}

$nativeArchive = Get-VoiceArchive 'https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip' 'vosk-win64-0.3.45.zip'
$modelArchive = Get-VoiceArchive 'https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip' 'vosk-model-small-en-us-0.15.zip'
$nativeStage = Join-Path $miuStage 'native-extracted'
$modelStage = Join-Path $miuStage 'model-extracted'
Expand-VoiceArchive $nativeArchive $nativeStage
Expand-VoiceArchive $modelArchive $modelStage

$libraries = @(Get-ChildItem -LiteralPath $nativeStage -Filter 'libvosk.dll' -File -Recurse)
if ($libraries.Count -ne 1) { throw 'The Vosk archive must contain exactly one libvosk.dll.' }
$nativeRoot = $libraries[0].Directory.FullName
Assert-VoicePath $nativeRoot; Assert-VoicePath $miuVoice
# Preserve the entire official engine directory, including upstream notices.
Move-Item -LiteralPath $nativeRoot -Destination $miuVoice
$modelSource = Join-Path $modelStage 'vosk-model-small-en-us-0.15'
if (-not (Test-Path -LiteralPath (Join-Path $modelSource 'am\final.mdl'))) { throw 'The model archive is incomplete.' }
$modelTarget = Join-Path $miuVoice 'model'
Assert-VoicePath $modelSource; Assert-VoicePath $modelTarget
Move-Item -LiteralPath $modelSource -Destination $modelTarget

@'
Vosk API 0.3.45 and vosk-model-small-en-us-0.15
Copyright Alpha Cephei Inc. and respective contributors.
Vosk API and this small English model are provided under Apache License 2.0.
Source: https://github.com/alphacep/vosk-api/tree/v0.3.45
Models: https://alphacephei.com/vosk/models
License: https://www.apache.org/licenses/LICENSE-2.0
Native archive: https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip
Model archive: https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip

The native Vosk package bundles third-party libraries. See the upstream project
and library licenses for Kaldi, OpenFst, OpenBLAS, and the MinGW runtime.
Downloads are pinned to official HTTPS publisher URLs. No checksum is claimed
as an upstream authenticity guarantee. The hashes below record your downloads.
'@ | Set-Content -LiteralPath (Join-Path $miuVoice 'MIU-DOWNLOAD-NOTICE.txt') -Encoding UTF8
Get-FileHash -Algorithm SHA256 -LiteralPath $nativeArchive, $modelArchive | ForEach-Object { $_.Hash + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath (Join-Path $miuVoice 'SHA256SUMS.local.txt') -Encoding ASCII

$worker = Join-Path $miuRoot 'dist\Miu.Voice.exe'
if (Test-Path -LiteralPath $worker) {
    Assert-VoicePath $worker
    $checkWorker = Join-Path $miuStage 'Miu.Voice.exe'
    Copy-Item -LiteralPath $worker -Destination $checkWorker
    $check = New-Object Diagnostics.Process
    $check.StartInfo = New-Object Diagnostics.ProcessStartInfo
    $check.StartInfo.FileName = $checkWorker
    $check.StartInfo.Arguments = '--check'
    $check.StartInfo.WorkingDirectory = $miuStage
    $check.StartInfo.UseShellExecute = $false
    $check.StartInfo.CreateNoWindow = $true
    $check.StartInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    try {
        if (-not $check.Start()) { throw 'The staged voice model check could not start.' }
        if (-not $check.WaitForExit(30000)) { $check.Kill(); throw 'Staged model check timed out; the existing installation was not changed.' }
        if ($check.ExitCode -ne 0) { throw 'Staged model check failed; the existing installation was not changed.' }
    } finally { $check.Dispose() }
}
$backup = Join-Path $miuStage 'previous-voice'
foreach ($path in @($miuVoice, $miuDestination, $backup)) { Assert-VoicePath $path }
[IO.Directory]::CreateDirectory((Split-Path -Parent $miuDestination)) | Out-Null
$movedOld = $false
if (Test-Path -LiteralPath $miuDestination) {
    if (-not (Test-Path -LiteralPath $miuDestination -PathType Container)) { throw 'Existing voice path is not a directory.' }
    Move-Item -LiteralPath $miuDestination -Destination $backup
    $movedOld = $true
}
try { Move-Item -LiteralPath $miuVoice -Destination $miuDestination }
catch {
    if ($movedOld -and -not (Test-Path -LiteralPath $miuDestination)) {
        Assert-VoicePath $backup; Assert-VoicePath $miuDestination
        Move-Item -LiteralPath $backup -Destination $miuDestination
    }
    throw
}
Write-Host 'Offline English voice installed. Audio stays on this PC; the worker exits after each turn.'
Write-Host ('Archives and any previous installation are preserved under ' + $miuCache)
Write-Host 'Small models can use roughly 300 MB during recognition; they are not resident while idle.'
