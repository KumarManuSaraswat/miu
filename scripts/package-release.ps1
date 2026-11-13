param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.0.0',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run packaging with Windows PowerShell: powershell.exe -NoProfile -File scripts\package-release.ps1' }
$miuPackageVersion = $Version
$miuPackageOutputArgument = $OutputDirectory
. (Join-Path $PSScriptRoot 'verify-release.ps1') -DefinitionsOnly
$Version = $miuPackageVersion
$OutputDirectory = $miuPackageOutputArgument
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$miuPackageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = 'artifacts/releases/' + $Version }
$miuPackageOutput = Resolve-MiuReleasePath $miuPackageRoot $OutputDirectory
if ((Test-Path -LiteralPath $miuPackageOutput) -and @(Get-ChildItem -LiteralPath $miuPackageOutput -Force).Count -gt 0) {
    throw 'The output directory is not empty. Use a new -OutputDirectory; existing release artifacts are never overwritten.'
}

$sourceNames = @(Get-MiuReleaseFiles 'source')
$portableNames = @(Get-MiuReleaseFiles 'windows-x64')
$allNames = @($sourceNames + $portableNames | Sort-Object -Unique)
# A new code file must be consciously added to the allowlist. This prevents
# creating a source release that accidentally omits a required build input.
foreach ($folder in @('src', 'tests')) {
    foreach ($item in (Get-ChildItem -LiteralPath (Join-Path $miuPackageRoot $folder) -File)) {
        if ($item.Extension -in @('.cs', '.xaml', '.manifest') -and $sourceNames -cnotcontains ($folder + '/' + $item.Name)) {
            throw ('Build input is missing from the source allowlist: ' + $folder + '/' + $item.Name)
        }
    }
}
$snapshot = @{}
$newestAppInput = [DateTime]::MinValue
$newestWorkerInput = [DateTime]::MinValue
foreach ($name in $allNames) {
    # In-app help resolves beside Miu.exe. Mirror only approved public docs,
    # reading their current root originals, never the recursively populated dist/docs.
    $sourceName = if ($name.StartsWith('dist/docs/', [StringComparison]::Ordinal)) { $name.Substring(5) } else { $name }
    $path = Resolve-MiuReleasePath $miuPackageRoot $sourceName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Missing required release input: ' + $name) }
    $item = Get-Item -LiteralPath $path
    if ($item.Length -le 0 -or $item.Length -gt 16MB) { throw ('Unexpected release input size: ' + $name) }
    $bytes = [IO.File]::ReadAllBytes($path)
    Assert-MiuPublicBytes $name $bytes
    $snapshot[$name] = $bytes
    if ($name -eq 'src/VoskSpeech.cs') { $newestWorkerInput = $item.LastWriteTimeUtc }
    elseif ($name -match '^(src/|tests/|assets/miu.ico$|build.ps1$)') {
        if ($item.LastWriteTimeUtc -gt $newestAppInput) { $newestAppInput = $item.LastWriteTimeUtc }
    }
}
$appPath = Resolve-MiuReleasePath $miuPackageRoot 'dist/Miu.exe'
$workerPath = Resolve-MiuReleasePath $miuPackageRoot 'dist/Miu.Voice.exe'
if ((Get-Item -LiteralPath $appPath).LastWriteTimeUtc -lt $newestAppInput -or
    (Get-Item -LiteralPath $workerPath).LastWriteTimeUtc -lt $newestWorkerInput) {
    throw 'The compiled app is older than its source. Run build.ps1 -Test before packaging; packaging never rebuilds.'
}
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($appPath)
if ($assemblyName.Version.ToString() -cne ($Version + '.0')) { throw 'The compiled app version does not match -Version. Update AssemblyInfo.cs and rebuild first.' }
foreach ($exe in @('dist/Miu.exe', 'dist/Miu.Voice.exe')) { Assert-MiuX64Executable $exe $snapshot[$exe] }

[IO.Directory]::CreateDirectory($miuPackageOutput) | Out-Null
$checksums = New-Object 'System.Collections.Generic.List[string]'
foreach ($kind in @('source', 'windows-x64')) {
    $baseName = 'miu-' + $Version + '-' + $kind
    $names = @(Get-MiuReleaseFiles $kind | Sort-Object -CaseSensitive)
    $innerManifest = ($names | ForEach-Object { (Get-MiuReleaseHash $snapshot[$_]) + '  ' + $_ }) -join "`n"
    $payload = @{}
    foreach ($name in $names) { $payload[$name] = $snapshot[$name] }
    $payload['MANIFEST.sha256'] = [Text.Encoding]::UTF8.GetBytes($innerManifest + "`n")
    $archivePath = Resolve-MiuReleasePath $miuPackageRoot (Join-Path $miuPackageOutput ($baseName + '.zip'))
    $fileStream = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $zip = New-Object IO.Compression.ZipArchive($fileStream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($name in @($payload.Keys | Sort-Object -CaseSensitive)) {
                $entry = $zip.CreateEntry($baseName + '/' + $name, [IO.Compression.CompressionLevel]::Optimal)
                # Fixed timestamps and sorted entries avoid leaking local file times
                # and make packaging the same byte inputs stable on the same runtime.
                $entry.LastWriteTime = New-Object DateTimeOffset(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $stream = $entry.Open()
                try { $bytes = [byte[]]$payload[$name]; $stream.Write($bytes, 0, $bytes.Length) }
                finally { $stream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $fileStream.Dispose() }
    $checksums.Add((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $baseName + '.zip')
}
[IO.File]::WriteAllText((Join-Path $miuPackageOutput 'SHA256SUMS.txt'), (($checksums | Sort-Object) -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Version $Version -OutputDirectory $miuPackageOutput
Write-Host ('Release files prepared in ' + $miuPackageOutput)
Write-Host 'No files were published. Optional speech engines/models are not bundled; use the included official setup scripts after extraction.'
