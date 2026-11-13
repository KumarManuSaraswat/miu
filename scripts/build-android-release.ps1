param([string]$Version = '1.0.1')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a numeric semantic version.' }
if (-not $env:JAVA_HOME -or -not $env:ANDROID_HOME) { throw 'Set JAVA_HOME and ANDROID_HOME first.' }
if (-not $env:MIU_KEYSTORE_PATH -or -not $env:MIU_STORE_PASSWORD) { throw 'Set MIU_KEYSTORE_PATH and MIU_STORE_PASSWORD without logging them.' }
if (-not (Test-Path -LiteralPath $env:MIU_KEYSTORE_PATH -PathType Leaf)) { throw 'Signing keystore was not found.' }
$gradleFile = Join-Path $root 'android/app/build.gradle'
if ([IO.File]::ReadAllText($gradleFile) -notmatch ('versionName\s+''' + [regex]::Escape($Version) + '''')) { throw 'Version differs from Android build configuration.' }
$signer = Join-Path $env:ANDROID_HOME 'build-tools/36.0.0/apksigner.bat'
if (-not (Test-Path -LiteralPath $signer)) { throw 'Install Android Build Tools 36.0.0.' }
& (Join-Path $PSScriptRoot 'prepare-local-ai.ps1') -Platform android
Push-Location (Join-Path $root 'android')
try {
    & .\gradlew.bat --no-daemon --console=plain -PwithLocalAi :app:testDebugUnitTest :app:lintRelease :app:assembleRelease
    if ($LASTEXITCODE -ne 0) { throw 'Android release checks failed.' }
} finally { Pop-Location }
$apk = Join-Path $root 'android/app/build/outputs/apk/release/app-release.apk'
if (-not (Test-Path -LiteralPath $apk)) { throw 'Signed release APK was not produced.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bundle = [IO.Compression.ZipFile]::OpenRead($apk)
try {
    $model = $bundle.GetEntry('assets/models/gemma3-1b.gguf')
    if (-not $model -or $model.Length -ne 806058400) { throw 'Release APK must include the complete offline model.' }
    $stream = $model.Open(); $sha = [Security.Cryptography.SHA256]::Create()
    try { $modelHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
    if ($modelHash -ne '2f8c3d137f2496dcb4019e17a50f2974b89c1be1a742db3c80cc86f91cafeeef') { throw 'APK model integrity check failed.' }
    foreach ($name in @('lib/arm64-v8a/libmiu_model.so', 'assets/legal/NOTICE.txt', 'assets/legal/GEMMA-TERMS.txt', 'assets/legal/GEMMA-PROHIBITED-USE.txt')) {
        if (-not $bundle.GetEntry($name)) { throw ('Missing APK input: ' + $name) }
    }
} finally { $bundle.Dispose() }
$savedErrorAction = $ErrorActionPreference
try {
    # Current JDKs print native-access warnings to stderr even when verification
    # succeeds. Windows PowerShell converts those lines to ErrorRecords.
    $ErrorActionPreference = 'Continue'
    $verification = & $signer verify --verbose --print-certs $apk 2>&1
    $signerExit = $LASTEXITCODE
} finally { $ErrorActionPreference = $savedErrorAction }
if ($signerExit -ne 0) { throw 'APK signature verification failed.' }
$verification = @($verification | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^(Verifies|Verified |Number of signers:|Signer #1 )' })
if (-not ($verification -match '^Signer #1 certificate SHA-256 digest: [a-f0-9]{64}$')) { throw 'Signing certificate digest missing.' }
$destination = Join-Path $root ('artifacts/android/' + $Version)
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$name = 'miu-' + $Version + '-android.apk'
Copy-Item -LiteralPath $apk -Destination (Join-Path $destination $name)
$hash = (Get-FileHash -LiteralPath (Join-Path $destination $name) -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), $hash + '  ' + $name + "`n", (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $destination 'SIGNING-CERTIFICATE.txt'), ($verification -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Output ('Verified APK: ' + (Join-Path $destination $name))
Write-Output ('SHA256: ' + $hash)
