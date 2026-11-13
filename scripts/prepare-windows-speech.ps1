# Builds a clean redistribution input tree from pinned public downloads, never
# from a user's installed speech/configuration folder. Model/runtime licenses
# and corresponding source accompany the ready-to-use Windows archive.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache = Join-Path $root '.cache/speech-bundle-downloads'
$stage = Join-Path $root '.cache/windows-speech-bundle'
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $cache, $stage | Out-Null
function Fetch([string]$Name, [string]$Url, [string]$Hash) {
    $path = Join-Path $cache $Name
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $Hash) {
        & curl.exe --fail --location --silent --show-error --retry 3 --max-time 1200 -o ($path + '.partial') $Url
        if ($LASTEXITCODE -ne 0) { throw ('Download failed: ' + $Name) }
        if ((Get-FileHash -LiteralPath ($path + '.partial')).Hash.ToLowerInvariant() -ne $Hash) { throw ('Integrity check failed: ' + $Name) }
        Move-Item -LiteralPath ($path + '.partial') -Destination $path -Force
    }
    return $path
}
function Extract([string]$Zip, [string]$Prefix, [string]$Destination) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            if (-not $entry.FullName.StartsWith($Prefix, [StringComparison]::Ordinal)) { throw 'Unexpected upstream ZIP layout.' }
            $relative = $entry.FullName.Substring($Prefix.Length)
            if ($relative -match '(^|/)\.\.?(\p{P}|$)|[:\\]' -or $relative.StartsWith('/')) { throw 'Unsafe upstream ZIP path.' }
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not $target.StartsWith([IO.Path]::GetFullPath($Destination) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe extraction destination.' }
            [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $archive.Dispose() }
}
$vosk = Fetch 'vosk-win64-0.3.45.zip' 'https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip' 'f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b'
$recognition = Fetch 'vosk-model-small-en-us-0.15.zip' 'https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip' '30f26242c4eb449f948e42cb302dd7a686cb29a3423a8367f99ff41780942498'
$piper = Fetch 'piper_windows_amd64.zip' 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' 'f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea'
Extract $vosk 'vosk-win64-0.3.45/' (Join-Path $stage 'dist/voice')
Extract $recognition 'vosk-model-small-en-us-0.15/' (Join-Path $stage 'dist/voice/model')
Extract $piper 'piper/' (Join-Path $stage 'dist/natural-voice/piper')
$voiceBase = 'https://huggingface.co/rhasspy/piper-voices/resolve/bae641dcb5a608cff81ccaf7ff018baaca1a33e1/en/en_US/ljspeech/medium/'
foreach ($entry in @(
    @('en_US-ljspeech-medium.onnx','6f52a751e2349abe7a76735eb09dc1875298c77ea2342ffd2fef79ff81b87f22'),
    @('en_US-ljspeech-medium.onnx.json','141d612cc0a95ed7efc1ca936b845c2364967f2e9217c5dbfcf69fc4d6c65860'),
    @('MODEL_CARD','fbee1529c89d36b3fe76d7e9f3f832dce17f44900a52d76a9bda735654766b4d')
)) {
    $file = Fetch $entry[0] ($voiceBase + $entry[0]) $entry[1]
    Copy-Item -LiteralPath $file -Destination (Join-Path $stage ('dist/natural-voice/' + $entry[0])) -Force
}
$sources = @(
    @('piper-2023.11.14-2.zip','rhasspy/piper','refs/tags/2023.11.14-2','48b4eef63a3694c1349c3227c94d6dc54c1d07849e058f40f33e8391f8487539'),
    @('piper-phonemize-fccd4f3.zip','rhasspy/piper-phonemize','fccd4f335aa68ac0b72600822f34d84363daa2bf','d4d899cca03114a6b2e68635dcdb7fba48b70599e36a481dfe6b6d9e2b37aa84'),
    @('espeak-ng-0f65aa3.zip','rhasspy/espeak-ng','0f65aa301e0d6bae5e172cc74197d32a6182200f','150fc955926a084a77ebe25c9e8287bde2e2a17bbf9b23a25c147b556837949f'),
    @('onnxruntime-1.14.1.zip','microsoft/onnxruntime','refs/tags/v1.14.1','a7328d078482b2257fdf234805fac621af3e820cbb4216e218c3bebab6e35624'),
    @('fmt-10.0.0.zip','fmtlib/fmt','refs/tags/10.0.0','5bf4d5358301fdf3bd100c01b9d4c1fbb2091dc2267fb4fa6d7cd522b3e47179'),
    @('spdlog-1.12.0.zip','gabime/spdlog','refs/tags/v1.12.0','6174bf8885287422a6c6a0312eb8a30e8d22bcfcee7c48a6d02d1835d7769232')
)
$sourceDir = Join-Path $stage 'third-party-source'
[IO.Directory]::CreateDirectory($sourceDir) | Out-Null
foreach ($entry in $sources) {
    $file = Fetch $entry[0] ('https://codeload.github.com/' + $entry[1] + '/zip/' + $entry[2]) $entry[3]
    Copy-Item -LiteralPath $file -Destination (Join-Path $sourceDir $entry[0]) -Force
}
Write-Host 'Clean, verified speech inputs prepared. Packaging checks every file against the reviewed bundle manifest.'
