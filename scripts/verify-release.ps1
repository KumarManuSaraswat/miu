param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.0',
    [string]$OutputDirectory = '',
    [switch]$SelfTest,
    [switch]$DefinitionsOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

# This is the single, reviewed release allowlist. Never replace these entries
# with recursive directory copies, git archive, or a wildcard over dist.
function Get-MiuReleaseFiles([ValidateSet('source', 'windows-x64')][string]$Kind) {
    $common = @(
        'README.md', 'LICENSE', 'PRIVACY.md', 'SECURITY.md', 'CHANGELOG.md', 'CONTRIBUTING.md',
        'CODE_OF_CONDUCT.md', 'GOVERNANCE.md', 'SUPPORT.md', 'docs/MAINTAINING.md',
        'assets/miu-speaking-preview.png',
        'Start Miu.cmd', 'docs/COMMANDS.md', 'docs/GMAIL_SETUP.md',
        'docs/THIRD_PARTY.md', 'docs/RELEASE.md', 'docs/NEON_SPONSORSHIP.md',
        'scripts/setup-voice.ps1', 'scripts/setup-natural-voice.ps1'
    )
    if ($Kind -eq 'windows-x64') {
        return @($common + @('dist/Miu.exe', 'dist/Miu.exe.config', 'dist/Miu.Voice.exe',
            'dist/docs/COMMANDS.md', 'dist/docs/GMAIL_SETUP.md', 'dist/docs/THIRD_PARTY.md', 'dist/docs/RELEASE.md'))
    }
    return @($common + @(
        '.gitignore', '.editorconfig', '.gitattributes', 'build.ps1',
        '.github/workflows/windows.yml',
        '.github/dependabot.yml', '.github/pull_request_template.md', '.github/ISSUE_TEMPLATE/config.yml',
        '.github/ISSUE_TEMPLATE/bug_report.yml', '.github/ISSUE_TEMPLATE/feature_request.yml',
        'assets/miu.ico', 'assets/miu.png',
        'scripts/package-release.ps1', 'scripts/verify-release.ps1',
        'scripts/make-icon.ps1', 'scripts/preview-natural-voice.ps1',
        'src/app.manifest', 'src/Theme.xaml', 'src/AppController.cs',
        'src/AssemblyInfo.cs', 'src/AssistantEngine.cs', 'src/ChatWindow.cs',
        'src/CommunityCatalog.cs', 'src/CommunityWindow.cs',
        'src/Configuration.cs', 'src/DeviceActions.cs', 'src/ExpressionCatalog.cs',
        'src/GmailService.cs', 'src/MascotFace.cs', 'src/MascotWindow.cs',
        'src/Native.cs', 'src/PiperSpeech.cs', 'src/Program.cs',
        'src/ReactionsWindow.cs', 'src/SettingsWindow.cs', 'src/Skin.cs',
        'src/SpeechService.cs', 'src/VoskSpeech.cs',
        'tests/EngineTests.cs', 'tests/GmailTests.cs', 'tests/CommunityCatalogTests.cs',
        'services/catalog/package.json', 'services/catalog/package-lock.json',
        'services/catalog/.gitignore', 'services/catalog/.dev.vars.example',
        'services/catalog/wrangler.jsonc', 'services/catalog/README.md',
        'services/catalog/src/contract.js', 'services/catalog/src/database.js',
        'services/catalog/src/worker.js', 'services/catalog/src/index.js',
        'services/catalog/migrations/001_catalog.sql', 'services/catalog/migrations/002_seed_presets.sql',
        'services/catalog/scripts/check-integration.js',
        'services/catalog/test/catalog.test.js', 'services/catalog/test/database.test.js'
    ))
}

function Get-MiuReleaseHash([byte[]]$Bytes) {
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hasher.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
}

function Assert-MiuX64Executable([string]$Name, [byte[]]$Bytes) {
    if ($Bytes.Length -lt 64 -or $Bytes[0] -ne 77 -or $Bytes[1] -ne 90) { throw ('Invalid Windows executable: ' + $Name) }
    $offset = [BitConverter]::ToInt32($Bytes, 60)
    if ($offset -lt 64 -or $offset -gt $Bytes.Length - 24 -or
        $Bytes[$offset] -ne 80 -or $Bytes[$offset + 1] -ne 69 -or
        $Bytes[$offset + 2] -ne 0 -or $Bytes[$offset + 3] -ne 0 -or
        [BitConverter]::ToUInt16($Bytes, $offset + 4) -ne 0x8664) {
        throw ('The release requires a valid x64 executable: ' + $Name)
    }
}

function Resolve-MiuReleasePath([string]$Root, [string]$Path) {
    $basePath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $resolved = if ([IO.Path]::IsPathRooted($Path)) { [IO.Path]::GetFullPath($Path) }
        else { [IO.Path]::GetFullPath((Join-Path $basePath $Path)) }
    if (-not $resolved.StartsWith($basePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Release paths must stay inside the project directory.'
    }
    $ancestor = $resolved
    while ($ancestor -and $ancestor.Length -ge $basePath.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            if (((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Release paths cannot contain symlinks or junctions.'
            }
        }
        if ($ancestor -eq $basePath) { break }
        $ancestor = Split-Path -Parent $ancestor
    }
    return $resolved
}

function Assert-MiuPublicBytes([string]$Name, [byte[]]$Bytes) {
    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 16MB) { throw ('Unexpected release file size: ' + $Name) }
    # Defense in depth, not a substitute for reviewing the exact allowlist.
    # Report only the file name: never print the matched credential/content.
    $texts = @([Text.Encoding]::UTF8.GetString($Bytes))
    if ($Name -match '\.(exe|ico|png)$') { $texts += [Text.Encoding]::Unicode.GetString($Bytes) }
    $patterns = @(
        'AIza[0-9A-Za-z_-]{35}',
        'AQ\.[0-9A-Za-z_-]{40,}',
        'GOCSPX-[0-9A-Za-z_-]{20,}',
        '1//[0-9A-Za-z_-]{25,}',
        '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
        '(?i)postgres(?:ql)?://[^\s/:<>\{\}]+:[^\s@<>\{\}]+@(?!(?:localhost|(?:[a-z0-9-]+\.)*(?:example|invalid|test)|(?:[a-z0-9-]+\.)*example\.(?:com|net|org))(?=[:/\s]|$))',
        '(?i)[A-Z]:\\(?:Users|Documents and Settings)\\(?!Public\\|Default\\|<|YOUR_|USERNAME)[a-z0-9._ -]+\\'
    )
    foreach ($value in $texts) {
        foreach ($pattern in $patterns) {
            if ([regex]::IsMatch($value, $pattern)) { throw ('Potential private content in release file: ' + $Name) }
        }
    }
}

function Read-MiuHashManifest([string]$Text, [string[]]$Expected) {
    $found = @{}
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line.Length -eq 0) { continue }
        if ($line -notmatch '^([0-9a-f]{64})  ([A-Za-z0-9_. /-]+)$') { throw 'Malformed SHA-256 manifest.' }
        $hash = $Matches[1]; $name = $Matches[2]
        if ($Expected -cnotcontains $name -or $found.ContainsKey($name)) { throw 'Unexpected or duplicate manifest entry.' }
        $found[$name] = $hash
    }
    if ($found.Count -ne $Expected.Count) { throw 'The SHA-256 manifest is incomplete.' }
    return $found
}

function Read-MiuCheckedZip([IO.Compression.ZipArchive]$Archive, [string]$Prefix, [string[]]$Expected) {
    $files = @{}
    [long]$total = 0
    foreach ($entry in $Archive.Entries) {
        $path = $entry.FullName
        if ($path.Contains('\') -or $path.Contains(':') -or $path.StartsWith('/') -or
            $path -match '(^|/)\.\.?(/|$)' -or -not $path.StartsWith($Prefix, [StringComparison]::Ordinal)) {
            throw 'Unsafe or unexpected ZIP path.'
        }
        $name = $path.Substring($Prefix.Length)
        if ($Expected -cnotcontains $name -or $files.ContainsKey($name)) { throw ('Unexpected or duplicate ZIP entry: ' + $name) }
        $total += $entry.Length
        if ($entry.Length -le 0 -or $entry.Length -gt 16MB -or $total -gt 64MB) { throw 'ZIP contents exceed the lean release size limits.' }
        # Deny Unix symlinks, even though archives are verified without extraction.
        if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'ZIP symlinks are not allowed.' }
        $inputStream = $entry.Open()
        $buffer = New-Object IO.MemoryStream
        try {
            $chunk = New-Object byte[] 8192
            while (($read = $inputStream.Read($chunk, 0, $chunk.Length)) -gt 0) {
                if ($buffer.Length + $read -gt $entry.Length) { throw 'ZIP entry expanded beyond its declared length.' }
                $buffer.Write($chunk, 0, $read)
            }
            if ($buffer.Length -ne $entry.Length) { throw 'Truncated ZIP entry.' }
            $bytes = $buffer.ToArray()
        }
        finally { $buffer.Dispose(); $inputStream.Dispose() }
        Assert-MiuPublicBytes $name $bytes
        $files[$name] = $bytes
    }
    if ($files.Count -ne $Expected.Count) { throw 'The release archive is missing an allowlisted file.' }
    return $files
}

function Test-MiuReleaseGuards {
    $script:guardChecks = 0
    function Expect-Rejected([scriptblock]$Action, [string]$Name) {
        $rejected = $false
        try { & $Action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw ('Release guard failed: ' + $Name) }
        $script:guardChecks++
    }
    function Check-ZipNames([string[]]$Names, [string[]]$Expected) {
        $memory = New-Object IO.MemoryStream
        try {
            $zip = New-Object IO.Compression.ZipArchive($memory, [IO.Compression.ZipArchiveMode]::Create, $true)
            try {
                foreach ($name in $Names) {
                    $stream = $zip.CreateEntry($name).Open()
                    try { $stream.WriteByte(65) } finally { $stream.Dispose() }
                }
            } finally { $zip.Dispose() }
            $memory.Position = 0
            $zip = New-Object IO.Compression.ZipArchive($memory, [IO.Compression.ZipArchiveMode]::Read, $true)
            try { Read-MiuCheckedZip $zip 'miu-test/' $Expected | Out-Null }
            finally { $zip.Dispose() }
        } finally { $memory.Dispose() }
    }
    Expect-Rejected { Check-ZipNames @('miu-test/../outside') @('ok') } 'traversal'
    Expect-Rejected { Check-ZipNames @('miu-test/credentials.json') @('ok') } 'unallowlisted credentials'
    Expect-Rejected { Check-ZipNames @('miu-test/ok', 'miu-test/ok') @('ok') } 'duplicates'
    Expect-Rejected { Check-ZipNames @('miu-test/ok') @('ok', 'missing') } 'missing files'
    Expect-Rejected { Check-ZipNames @('miu-test\ok') @('ok') } 'backslash paths'
    Expect-Rejected { Check-ZipNames @('/miu-test/ok') @('ok') } 'absolute paths'
    Expect-Rejected { Check-ZipNames @('miu-test/OK') @('ok') } 'case mismatch'
    Expect-Rejected { Assert-MiuPublicBytes 'fixture.txt' ([Text.Encoding]::UTF8.GetBytes(('AI' + 'za' + ('A' * 35)))) } 'Google key'
    Expect-Rejected { Assert-MiuPublicBytes 'fixture.txt' ([Text.Encoding]::UTF8.GetBytes(('AQ' + '.' + ('B' * 45)))) } 'Google auth key'
    Expect-Rejected { Assert-MiuPublicBytes 'fixture.txt' ([Text.Encoding]::UTF8.GetBytes(('1' + '//' + ('C' * 30)))) } 'refresh token'
    Expect-Rejected { Assert-MiuPublicBytes 'fixture.txt' ([Text.Encoding]::UTF8.GetBytes(('postgresql' + '://person:secret@host/db'))) } 'database password'
    Expect-Rejected { Assert-MiuPublicBytes 'fixture.txt' ([Text.Encoding]::UTF8.GetBytes(('C:' + '\Users\private-person\data'))) } 'local profile'
    Expect-Rejected { Read-MiuHashManifest (('0' * 64) + '  unknown') @('ok') } 'manifest extra file'
    Expect-Rejected { Read-MiuHashManifest '' @('ok') } 'manifest missing file'
    $pe = New-Object byte[] 128
    $pe[0] = 77; $pe[1] = 90; $pe[60] = 64; $pe[64] = 80; $pe[65] = 69; $pe[68] = 0x64; $pe[69] = 0x86
    Assert-MiuX64Executable 'fixture.exe' $pe; $script:guardChecks++
    $pe[68] = 0x4c; $pe[69] = 0x01
    Expect-Rejected { Assert-MiuX64Executable 'fixture.exe' $pe } 'x86 executable in x64 package'
    $pe[68] = 0x64; $pe[69] = 0x86; $pe[64] = 0
    Expect-Rejected { Assert-MiuX64Executable 'fixture.exe' $pe } 'invalid PE signature'
    $pe[60] = 127
    Expect-Rejected { Assert-MiuX64Executable 'fixture.exe' $pe } 'out-of-bounds PE header'
    Check-ZipNames @('miu-test/ok') @('ok'); $script:guardChecks++
    Assert-MiuPublicBytes 'example.txt' ([Text.Encoding]::UTF8.GetBytes('postgresql://<USER>:<PASSWORD>@<HOST>/<DATABASE>')); $script:guardChecks++
    Write-Host ('PASS: ' + $script:guardChecks + ' release guard checks; no credentials, microphone, network, or app launch used.')
}

if ($DefinitionsOnly) { return }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
if ($SelfTest) { Test-MiuReleaseGuards; return }
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run release verification with Windows PowerShell (powershell.exe), which provides .NET Framework reflection-only inspection.' }
$miuReleaseRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = 'artifacts/releases/' + $Version }
$miuReleaseOutput = Resolve-MiuReleasePath $miuReleaseRoot $OutputDirectory
$archiveNames = @(('miu-' + $Version + '-source.zip'), ('miu-' + $Version + '-windows-x64.zip'))
$manifestPath = Resolve-MiuReleasePath $miuReleaseRoot (Join-Path $miuReleaseOutput 'SHA256SUMS.txt')
$outerHashes = Read-MiuHashManifest ([IO.File]::ReadAllText($manifestPath)) $archiveNames
$expectedOutput = @($archiveNames + @('SHA256SUMS.txt'))
foreach ($item in (Get-ChildItem -LiteralPath $miuReleaseOutput -Force)) {
    if ($item.PSIsContainer -or $expectedOutput -cnotcontains $item.Name) { throw 'The release output directory contains an unexpected item.' }
}
foreach ($kind in @('source', 'windows-x64')) {
    $baseName = 'miu-' + $Version + '-' + $kind
    $archivePath = Resolve-MiuReleasePath $miuReleaseRoot (Join-Path $miuReleaseOutput ($baseName + '.zip'))
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($archiveHash -cne $outerHashes[$baseName + '.zip']) { throw ('Archive SHA-256 mismatch: ' + $baseName) }
    $expected = @(Get-MiuReleaseFiles $kind)
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try { $files = Read-MiuCheckedZip $archive ($baseName + '/') @($expected + @('MANIFEST.sha256')) }
    finally { $archive.Dispose() }
    $hashes = Read-MiuHashManifest ([Text.Encoding]::UTF8.GetString($files['MANIFEST.sha256'])) $expected
    foreach ($name in $expected) {
        if ((Get-MiuReleaseHash $files[$name]) -cne $hashes[$name]) { throw ('File SHA-256 mismatch: ' + $name) }
    }
    if ($kind -eq 'windows-x64') {
        foreach ($exe in @('dist/Miu.exe', 'dist/Miu.Voice.exe')) { Assert-MiuX64Executable $exe $files[$exe] }
        $assembly = [Reflection.Assembly]::ReflectionOnlyLoad([byte[]]$files['dist/Miu.exe'])
        if ($assembly.GetName().Version.ToString() -cne ($Version + '.0')) { throw 'The portable app version does not match the release version.' }
    }
    Write-Host ('PASS: ' + $baseName + '.zip; ' + $expected.Count + ' allowlisted files and verified file hashes.')
}
Write-Host 'Both lean release archives and SHA256SUMS.txt passed verification. Checksums establish integrity, not publisher identity or a code signature.'
