param([switch]$Test)
$ErrorActionPreference = 'Stop'
$miuRoot = $PSScriptRoot
$miuFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$miuCompiler = Join-Path $miuFramework 'csc.exe'
if (-not (Test-Path -LiteralPath $miuCompiler)) { throw 'Miu needs 64-bit Windows with .NET Framework 4.8 (included in modern Windows). No .NET SDK or npm is needed.' }
$miuDist = Join-Path $miuRoot 'dist'
[IO.Directory]::CreateDirectory($miuDist) | Out-Null
$speechAssembly = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter System.Speech.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $speechAssembly) { throw 'Windows System.Speech.dll was not found. Enable the .NET Framework desktop components.' }
$references = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Security.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Xml.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $miuFramework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $miuFramework 'WPF') $_) }
$references += '/reference:' + $speechAssembly
$sources = @(Get-ChildItem -LiteralPath (Join-Path $miuRoot 'src') -Filter '*.cs' | Where-Object { $_.Name -ne 'VoskSpeech.cs' } | ForEach-Object { $_.FullName })
$sources += @(Get-ChildItem -LiteralPath (Join-Path $miuRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/main:Miu.Program',('/out:' + (Join-Path $miuDist 'Miu.build.exe')),('/win32manifest:' + (Join-Path $miuRoot 'src\app.manifest')),('/resource:' + (Join-Path $miuRoot 'src\Theme.xaml') + ',Miu.Theme.xaml'))
$icon = Join-Path $miuRoot 'assets\miu.ico'
if (Test-Path -LiteralPath $icon) { $arguments += '/win32icon:' + $icon }
& $miuCompiler @arguments @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Miu compilation failed.' }
$miuExecutable = Join-Path $miuDist 'Miu.exe'
if (Test-Path -LiteralPath $miuExecutable) {
    $miuPrevious = Join-Path $miuRoot '.cache\builds'
    [IO.Directory]::CreateDirectory($miuPrevious) | Out-Null
    Move-Item -LiteralPath $miuExecutable -Destination (Join-Path $miuPrevious ('Miu-' + [Guid]::NewGuid().ToString('N') + '.exe'))
}
Move-Item -LiteralPath (Join-Path $miuDist 'Miu.build.exe') -Destination $miuExecutable
$workerSource = Join-Path $miuRoot 'src\VoskSpeech.cs'
if (Test-Path -LiteralPath $workerSource) {
    & $miuCompiler /nologo /target:exe /platform:x64 /optimize+ /main:Miu.VoskWorker (('/out:' + (Join-Path $miuDist 'Miu.Voice.exe'))) /reference:System.dll /reference:System.Core.dll $workerSource
    if ($LASTEXITCODE -ne 0) { throw 'Offline voice worker compilation failed.' }
}
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup><runtime><AppContextSwitchOverrides value="Switch.System.Net.DontEnableSystemDefaultTlsVersions=false"/></runtime></configuration>
'@ | Set-Content -LiteralPath (Join-Path $miuDist 'Miu.exe.config') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $miuRoot 'docs') -Destination $miuDist -Recurse -Force
if (Test-Path -LiteralPath (Join-Path $miuRoot 'README.md')) { Copy-Item -LiteralPath (Join-Path $miuRoot 'README.md') -Destination $miuDist -Force }
Write-Host ('Built ' + (Join-Path $miuDist 'Miu.exe'))
if ($Test) {
    $run = Start-Process -FilePath (Join-Path $miuDist 'Miu.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath (Join-Path $miuDist 'self-test-results.txt')
    if ($run.ExitCode -ne 0) { throw 'Self tests failed.' }
    & (Join-Path $miuDist 'Miu.Voice.exe') --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Offline voice self tests failed.' }
}
