param([ValidateSet('windows','android','all')][string]$Platform = 'all', [switch]$SkipModel)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache = Join-Path $root '.cache/local-ai'
[IO.Directory]::CreateDirectory($cache) | Out-Null
$curl = if (Get-Command curl.exe -ErrorAction SilentlyContinue) { 'curl.exe' } else { 'curl' }
$tar = if (Get-Command tar.exe -ErrorAction SilentlyContinue) { 'tar.exe' } else { 'tar' }

# Immutable upstream files. No credentials, models or native binaries enter Git.
function Get-Verified([string]$Name, [string]$Url, [string]$Hash, [long]$Size) {
    $path = Join-Path $cache $Name
    if ((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -eq $Size -and
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Hash) { return $path }
    $partial = $path + '.partial'
    & $curl --fail --location --silent --show-error --retry 3 --connect-timeout 20 --max-time 3600 --output $partial $Url
    if ($LASTEXITCODE -ne 0) { throw ('Download failed: ' + $Name) }
    if ((Get-Item -LiteralPath $partial).Length -ne $Size -or
        (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Hash) { throw ('Integrity check failed: ' + $Name) }
    Move-Item -LiteralPath $partial -Destination $path -Force
    return $path
}

if (-not $SkipModel) {
    $model = Get-Verified 'gemma3-1b.gguf' 'https://registry.ollama.ai/v2/library/gemma3/blobs/sha256:2f8c3d137f2496dcb4019e17a50f2974b89c1be1a742db3c80cc86f91cafeeef' '2f8c3d137f2496dcb4019e17a50f2974b89c1be1a742db3c80cc86f91cafeeef' 806058400
}
if ($Platform -in @('windows','all')) {
    $zip = Get-Verified 'llama-windows.zip' 'https://github.com/ggml-org/llama.cpp/releases/download/b12110/llama-b12110-bin-win-cpu-x64.zip' '98901e327c252875accaf2d1bf364e10e868843b0b63468baad1aa945cdc5da2' 24708445
    $unpack = Join-Path $cache 'windows-b12110'
    Expand-Archive -LiteralPath $zip -DestinationPath $unpack -Force
    $destination = Join-Path $root 'dist/local-ai'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    $files = @('llama-completion.exe','llama-completion-impl.dll','llama-common.dll','llama.dll','ggml.dll','ggml-base.dll','libomp.dll','LICENSE-LLVM-OpenMP')
    $files += @('alderlake','cannonlake','cascadelake','cooperlake','haswell','icelake','ivybridge','piledriver','sandybridge','sapphirerapids','skylakex','sse42','x64','zen4' | ForEach-Object { 'ggml-cpu-' + $_ + '.dll' })
    foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $unpack $file) -Destination $destination -Force }
    if (-not $SkipModel) {
        [IO.Directory]::CreateDirectory((Join-Path $root 'dist/models')) | Out-Null
        Copy-Item -LiteralPath $model -Destination (Join-Path $root 'dist/models/gemma3-1b.gguf') -Force
    }
}
if ($Platform -in @('android','all')) {
    $archive = Get-Verified 'llama-android.tar.gz' 'https://github.com/ggml-org/llama.cpp/releases/download/b12110/llama-b12110-bin-android-arm64.tar.gz' '44462ddcc09a841942e5cfe76554240ed6d7763bdf8538232075eb0ffd46ccbe' 79929855
    $unpack = Join-Path $cache 'android-b12110'
    [IO.Directory]::CreateDirectory($unpack) | Out-Null
    & $tar -xzf $archive -C $unpack
    if ($LASTEXITCODE -ne 0) { throw 'Android runtime extraction failed.' }
    $upstream = Join-Path $unpack 'llama-b12110'
    $destination = Join-Path $cache 'android/jniLibs/arm64-v8a'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($file in @('libggml.so','libggml-base.so','libllama.so','libllama-common.so','libllama-completion-impl.so')) {
        Copy-Item -LiteralPath (Join-Path $upstream $file) -Destination $destination -Force
    }
    foreach ($variant in @('armv8.0_1','armv8.2_1','armv8.2_2','armv8.6_1','armv9.0_1','armv9.2_1','armv9.2_2')) {
        Copy-Item -LiteralPath (Join-Path $upstream ('libggml-cpu-android_' + $variant + '.so')) -Destination $destination -Force
    }
    Copy-Item -LiteralPath (Join-Path $upstream 'llama-completion') -Destination (Join-Path $destination 'libmiu_model.so') -Force
    if (-not $SkipModel) {
        $models = Join-Path $cache 'android/assets/models'
        [IO.Directory]::CreateDirectory($models) | Out-Null
        Copy-Item -LiteralPath $model -Destination (Join-Path $models 'gemma3-1b.gguf') -Force
    }
}
Write-Host 'Verified local AI files prepared. These are build inputs, not a published release.'
