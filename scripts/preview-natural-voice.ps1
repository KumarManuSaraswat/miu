$ErrorActionPreference = 'Stop'
$miuRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$miuVoice = Join-Path $miuRoot 'dist\natural-voice'
$miuEngine = Join-Path $miuVoice 'piper\piper.exe'
$miuModel = Join-Path $miuVoice 'en_US-ljspeech-medium.onnx'
$miuPreview = Join-Path $miuRoot 'artifacts\miu-natural-voice.wav'
if (-not (Test-Path -LiteralPath $miuEngine)) { throw 'Install the natural voice first.' }
[IO.Directory]::CreateDirectory((Split-Path -Parent $miuPreview)) | Out-Null
$miuInfo = New-Object Diagnostics.ProcessStartInfo
$miuInfo.FileName = $miuEngine
$miuInfo.WorkingDirectory = Split-Path -Parent $miuEngine
$miuInfo.Arguments = '--model "' + $miuModel + '" --output_file "' + $miuPreview + '" --quiet --sentence_silence 0.12'
$miuInfo.UseShellExecute = $false
$miuInfo.CreateNoWindow = $true
$miuInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$miuInfo.RedirectStandardInput = $true
$miuInfo.RedirectStandardOutput = $true
$miuInfo.RedirectStandardError = $true
$miuProcess = New-Object Diagnostics.Process
$miuProcess.StartInfo = $miuInfo
$miuClock = [Diagnostics.Stopwatch]::StartNew()
[long]$miuPeak = 0
try {
    if (-not $miuProcess.Start()) { throw 'Natural voice did not start.' }
    $miuOut = $miuProcess.StandardOutput.ReadToEndAsync()
    $miuErr = $miuProcess.StandardError.ReadToEndAsync()
    # Only this public demonstration text is saved as an audio artifact.
    # Miu's actual spoken replies are synthesized and played entirely in RAM.
    $miuWriter = New-Object IO.StreamWriter($miuProcess.StandardInput.BaseStream, (New-Object Text.UTF8Encoding($false)))
    $miuWriter.WriteLine("Hi, I'm Miu. I'm here to help. What would you like to do today?")
    $miuWriter.Dispose()
    while (-not $miuProcess.WaitForExit(30)) {
        $miuProcess.Refresh()
        $miuPeak = [Math]::Max($miuPeak, $miuProcess.PeakWorkingSet64)
        if ($miuClock.Elapsed.TotalSeconds -gt 60) { throw 'Natural voice generation timed out.' }
    }
    $miuClock.Stop()
    $miuOut.GetAwaiter().GetResult() | Out-Null
    $miuErr.GetAwaiter().GetResult() | Out-Null
    if ($miuProcess.ExitCode -ne 0) { throw 'Natural voice preview failed.' }
    $miuBytes = [IO.File]::ReadAllBytes($miuPreview)
    if ($miuBytes.Length -lt 44 -or [Text.Encoding]::ASCII.GetString($miuBytes, 0, 4) -ne 'RIFF') { throw 'Preview is not a valid WAV file.' }
    [pscustomobject]@{
        Preview = $miuPreview
        GeneratedSeconds = [Math]::Round($miuClock.Elapsed.TotalSeconds, 2)
        SampledPeakWorkingSetMiB = [Math]::Round($miuPeak / 1MB, 1)
        AudioBytes = $miuBytes.Length
    } | Format-List
}
finally {
    try { if (-not $miuProcess.HasExited) { $miuProcess.Kill(); $miuProcess.WaitForExit(3000) | Out-Null } } catch { }
    $miuProcess.Dispose()
}
