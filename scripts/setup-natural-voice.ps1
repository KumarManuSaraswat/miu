param([switch]$Force, [string]$ResumeStage = '')

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$miuRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$miuDestination = [IO.Path]::GetFullPath((Join-Path $miuRoot 'dist\natural-voice'))
$miuModelName = 'en_US-ljspeech-medium.onnx'
$miuModelHash = '6f52a751e2349abe7a76735eb09dc1875298c77ea2342ffd2fef79ff81b87f22'
$miuVoiceRevision = 'bae641dcb5a608cff81ccaf7ff018baaca1a33e1'
$miuVoiceBase = 'https://huggingface.co/rhasspy/piper-voices/resolve/' + $miuVoiceRevision + '/en/en_US/ljspeech/medium/'
$miuPiperUrl = 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip'

function Assert-MiuChildPath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($miuRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Refusing a path outside the Miu workspace: ' + $resolved)
    }
    # Do not let a junction/symlink redirect an install or backup outside the workspace.
    $ancestor = $resolved
    while ($ancestor -and $ancestor -ne $miuRoot) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw ('Refusing a redirected installation path: ' + $ancestor)
            }
        }
        $ancestor = Split-Path -Parent $ancestor
    }
}

function Get-MiuArtifact([string]$Url, [string]$Path, [string]$Sha256 = '', [switch]$ReuseExisting) {
    Assert-MiuChildPath $Path
    $uri = [Uri]$Url
    if ($uri.Scheme -ne 'https' -or $uri.Host -notin @('github.com', 'raw.githubusercontent.com', 'huggingface.co')) {
        throw 'Natural voice downloads must come from the documented upstream HTTPS sources.'
    }
    $reuse = $ReuseExisting -and (Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -gt 0
    if ($reuse -and $Sha256) {
        $reuse = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha256
    }
    if ($reuse) { Write-Host ('Reusing ' + [IO.Path]::GetFileName($Path)) }
    else { Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path -TimeoutSec 300 }
    if ((Get-Item -LiteralPath $Path).Length -eq 0) { throw ('Empty download: ' + [IO.Path]::GetFileName($Path)) }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($Sha256 -and $actual -ne $Sha256.ToLowerInvariant()) { throw ('SHA-256 mismatch: ' + [IO.Path]::GetFileName($Path)) }
    return $actual
}

Assert-MiuChildPath $miuDestination
if (-not $Force -and (Test-Path -LiteralPath (Join-Path $miuDestination 'piper\piper.exe')) -and
    (Test-Path -LiteralPath (Join-Path $miuDestination ($miuModelName + '.json'))) -and
    (Test-Path -LiteralPath (Join-Path $miuDestination $miuModelName))) {
    if ((Get-FileHash -LiteralPath (Join-Path $miuDestination $miuModelName) -Algorithm SHA256).Hash.ToLowerInvariant() -eq $miuModelHash) {
        Write-Host 'The natural offline English voice is already installed. Use -Force to reinstall.'
        return
    }
}

if ($ResumeStage) {
    $miuStage = [IO.Path]::GetFullPath($ResumeStage).TrimEnd('\', '/')
    $miuCache = [IO.Path]::GetFullPath((Join-Path $miuRoot '.cache'))
    if ((Split-Path -Parent $miuStage) -ne $miuCache -or
        (Split-Path -Leaf $miuStage) -notmatch '^natural-voice-[0-9a-f]{10}$' -or
        -not (Test-Path -LiteralPath $miuStage -PathType Container)) {
        throw '-ResumeStage must name an existing .cache\natural-voice-xxxxxxxxxx directory inside this workspace.'
    }
}
else { $miuStage = [IO.Path]::GetFullPath((Join-Path $miuRoot ('.cache\natural-voice-' + [Guid]::NewGuid().ToString('N').Substring(0, 10)))) }
$miuExtract = Join-Path $miuStage 'unpacked'
$miuPackage = Join-Path $miuStage 'natural-voice'
Assert-MiuChildPath $miuStage
Assert-MiuChildPath $miuExtract
Assert-MiuChildPath $miuPackage
[IO.Directory]::CreateDirectory($miuExtract) | Out-Null
[IO.Directory]::CreateDirectory($miuPackage) | Out-Null
if ($ResumeStage) { Write-Host ('Resuming the natural voice installation from ' + $miuStage) }
else { Write-Host 'Downloading the free offline neural voice (about 85 MB). No account, Python, or payment is needed.' }

# Old GitHub releases do not always publish a digest. Verify it when GitHub
# supplies one; otherwise retain the exact pinned release URL and local digest.
$miuPublishedPiperHash = ''
try {
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/rhasspy/piper/releases/tags/2023.11.14-2' -Headers @{ 'User-Agent' = 'Miu-Natural-Voice-Setup' } -TimeoutSec 20
    $asset = $release.assets | Where-Object { $_.name -eq 'piper_windows_amd64.zip' } | Select-Object -First 1
    if ($asset.digest -match '^sha256:([0-9a-fA-F]{64})$') { $miuPublishedPiperHash = $Matches[1] }
}
catch { Write-Host 'Release digest metadata unavailable; using the pinned official HTTPS release.' }

$miuArchive = Join-Path $miuStage 'piper_windows_amd64.zip'
$miuArchiveHash = Get-MiuArtifact $miuPiperUrl $miuArchive $miuPublishedPiperHash -ReuseExisting:([bool]$ResumeStage)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$miuZip = [IO.Compression.ZipFile]::OpenRead($miuArchive)
$miuEngineDestination = Join-Path $miuPackage 'piper'
Assert-MiuChildPath $miuEngineDestination
$miuReuseEngine = [bool]$ResumeStage -and (Test-Path -LiteralPath (Join-Path $miuEngineDestination 'piper.exe') -PathType Leaf)
try {
    [long]$expandedBytes = 0
    foreach ($entry in $miuZip.Entries) {
        $entryPath = [IO.Path]::GetFullPath((Join-Path $miuExtract $entry.FullName))
        if (-not $entryPath.StartsWith($miuExtract + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The downloaded archive contains an unsafe path.'
        }
        $expandedBytes += $entry.Length
        if ($expandedBytes -gt 300MB) { throw 'The downloaded engine exceeds the expected extraction size.' }
    }
    # Reuse only an extraction whose files still match the saved official ZIP.
    # This also reads every ZIP payload, detecting an incomplete saved archive.
    if ($miuReuseEngine) {
        $engineEntries = @($miuZip.Entries | Where-Object { $_.Name -eq 'piper.exe' })
        if ($engineEntries.Count -ne 1) { throw 'The official archive did not contain exactly one Piper executable.' }
        $enginePrefix = $engineEntries[0].FullName.Substring(0, $engineEntries[0].FullName.Length - 'piper.exe'.Length)
        foreach ($entry in $miuZip.Entries) {
            if (-not $entry.Name -or -not $entry.FullName.StartsWith($enginePrefix, [StringComparison]::Ordinal)) { continue }
            $existing = Join-Path $miuEngineDestination $entry.FullName.Substring($enginePrefix.Length)
            Assert-MiuChildPath $existing
            if (-not (Test-Path -LiteralPath $existing -PathType Leaf) -or (Get-Item -LiteralPath $existing).Length -ne $entry.Length) {
                throw 'The saved Piper extraction is incomplete. Run again without -ResumeStage to use a fresh stage.'
            }
            $entryStream = $entry.Open()
            $entryHasher = [Security.Cryptography.SHA256]::Create()
            try { $entryHash = [BitConverter]::ToString($entryHasher.ComputeHash($entryStream)).Replace('-', '') }
            finally { $entryHasher.Dispose(); $entryStream.Dispose() }
            if ((Get-FileHash -LiteralPath $existing -Algorithm SHA256).Hash -ne $entryHash) {
                throw 'The saved Piper extraction failed verification. Run again without -ResumeStage to use a fresh stage.'
            }
        }
    }
}
finally { $miuZip.Dispose() }
if ($miuReuseEngine) { Write-Host 'Verified and reused the existing Piper extraction.' }
else {
# A failed earlier extraction may have left files behind. Preserve them and use
# a new empty extraction directory rather than overwriting or deleting them.
if (@(Get-ChildItem -LiteralPath $miuExtract -Force).Count -gt 0) {
    $miuExtract = Join-Path $miuStage ('unpacked-' + [Guid]::NewGuid().ToString('N').Substring(0, 10))
    Assert-MiuChildPath $miuExtract
    [IO.Directory]::CreateDirectory($miuExtract) | Out-Null
}
[IO.Compression.ZipFile]::ExtractToDirectory($miuArchive, $miuExtract)
$miuEngineFiles = @(Get-ChildItem -LiteralPath $miuExtract -Filter 'piper.exe' -File -Recurse)
if ($miuEngineFiles.Count -ne 1) { throw 'The official archive did not contain exactly one Piper executable.' }
$miuEngineDirectory = $miuEngineFiles[0].Directory.FullName
Assert-MiuChildPath $miuEngineDirectory
Assert-MiuChildPath $miuEngineDestination
if (Test-Path -LiteralPath $miuEngineDestination) { throw 'The stage contains an incomplete engine directory. Run again without -ResumeStage.' }
Move-Item -LiteralPath $miuEngineDirectory -Destination $miuEngineDestination
}
if (-not (Test-Path -LiteralPath (Join-Path $miuEngineDestination 'espeak-ng-data'))) { throw 'The archive is missing its phonemizer data.' }

Write-Host 'Downloading and verifying the LJSpeech voice model...'
$miuDownloadedModelHash = Get-MiuArtifact ($miuVoiceBase + $miuModelName) (Join-Path $miuPackage $miuModelName) $miuModelHash -ReuseExisting:([bool]$ResumeStage)
$miuConfigHash = Get-MiuArtifact ($miuVoiceBase + $miuModelName + '.json') (Join-Path $miuPackage ($miuModelName + '.json'))
$miuCardHash = Get-MiuArtifact ($miuVoiceBase + 'MODEL_CARD') (Join-Path $miuPackage 'MODEL_CARD')
# Piper's phoneme map intentionally contains distinct keys such as X and x.
# PowerShell's case-insensitive ConvertFrom-Json cannot represent that map.
# The framework JSON reader preserves case and lets us inspect only metadata.
Add-Type -AssemblyName System.Runtime.Serialization
$miuConfigBytes = [IO.File]::ReadAllBytes((Join-Path $miuPackage ($miuModelName + '.json')))
$miuJsonReader = [Runtime.Serialization.Json.JsonReaderWriterFactory]::CreateJsonReader($miuConfigBytes, [Xml.XmlDictionaryReaderQuotas]::Max)
$miuConfig = New-Object Xml.XmlDocument
$miuConfig.XmlResolver = $null
try { $miuConfig.Load($miuJsonReader) }
finally { $miuJsonReader.Dispose() }
if ($miuConfig.SelectSingleNode('/root/audio/sample_rate').InnerText -cne '22050' -or
    $miuConfig.SelectSingleNode('/root/num_speakers').InnerText -cne '1' -or
    $miuConfig.SelectSingleNode('/root/dataset').InnerText -cne 'ljspeech') {
    throw 'The voice configuration does not match the expected mono 22050 Hz LJSpeech model.'
}

# Keep all notices supplied in the archive and add the principal upstream
# licenses next to it. These files contain licensing text, never user data.
$miuLicenseDirectory = Join-Path $miuPackage 'licenses'
[IO.Directory]::CreateDirectory($miuLicenseDirectory) | Out-Null
$miuLicenses = @(
    @{ Name = 'piper-MIT.txt'; Url = 'https://raw.githubusercontent.com/rhasspy/piper/2023.11.14-2/LICENSE.md' },
    @{ Name = 'piper-phonemize-MIT.txt'; Url = 'https://raw.githubusercontent.com/rhasspy/piper-phonemize/master/LICENSE.md' },
    @{ Name = 'espeak-ng-GPL-3.0.txt'; Url = 'https://raw.githubusercontent.com/rhasspy/espeak-ng/0f65aa301e0d6bae5e172cc74197d32a6182200f/COPYING' },
    @{ Name = 'onnxruntime-MIT.txt'; Url = 'https://raw.githubusercontent.com/microsoft/onnxruntime/v1.14.1/LICENSE' },
    @{ Name = 'fmt-MIT.txt'; Url = 'https://raw.githubusercontent.com/fmtlib/fmt/10.0.0/LICENSE.rst' },
    @{ Name = 'spdlog-MIT.txt'; Url = 'https://raw.githubusercontent.com/gabime/spdlog/v1.12.0/LICENSE' },
    @{ Name = 'nlohmann-json-MIT.txt'; Url = 'https://raw.githubusercontent.com/nlohmann/json/v3.11.2/LICENSE.MIT' }
)
foreach ($license in $miuLicenses) { Get-MiuArtifact $license.Url (Join-Path $miuLicenseDirectory $license.Name) | Out-Null }
$miuThirdParty = Join-Path $miuRoot 'docs\THIRD_PARTY.md'
if (Test-Path -LiteralPath $miuThirdParty) { Copy-Item -LiteralPath $miuThirdParty -Destination (Join-Path $miuPackage 'THIRD_PARTY.md') }

$miuManifest = [ordered]@{
    piperRelease = '2023.11.14-2'
    piperSource = $miuPiperUrl
    piperArchiveSha256 = $miuArchiveHash
    piperPublishedDigestVerified = [bool]$miuPublishedPiperHash
    voiceRevision = $miuVoiceRevision
    modelSource = $miuVoiceBase + $miuModelName
    modelSha256 = $miuDownloadedModelHash
    modelPublishedDigestVerified = $true
    configSha256 = $miuConfigHash
    modelCardSha256 = $miuCardHash
    installedUtc = [DateTime]::UtcNow.ToString('o')
}
$miuManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $miuPackage 'install-manifest.json') -Encoding UTF8

$miuBackup = $null
Assert-MiuChildPath $miuPackage
Assert-MiuChildPath $miuDestination
[IO.Directory]::CreateDirectory((Split-Path -Parent $miuDestination)) | Out-Null
if (Test-Path -LiteralPath $miuDestination) {
    $miuBackup = Join-Path $miuStage 'previous-natural-voice'
    Assert-MiuChildPath $miuBackup
    Move-Item -LiteralPath $miuDestination -Destination $miuBackup
}
try { Move-Item -LiteralPath $miuPackage -Destination $miuDestination }
catch {
    if ($miuBackup -and -not (Test-Path -LiteralPath $miuDestination)) {
        Assert-MiuChildPath $miuBackup
        Assert-MiuChildPath $miuDestination
        Move-Item -LiteralPath $miuBackup -Destination $miuDestination
    }
    throw
}
Write-Host ('Natural voice installed in ' + $miuDestination)
Write-Host 'Enable the natural offline voice in Miu Settings. The voice process runs only while synthesizing a reply.'
Write-Host ('Downloaded archive and any previous installation were preserved in ' + $miuStage)
