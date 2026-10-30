$ErrorActionPreference = 'Stop'
$version = '@@VERSION@@'
$sourceHash = '@@SOURCE_HASH@@'
$payloadHash = '@@PAYLOAD_HASH@@'
$sourceLength = @@SOURCE_LENGTH@@
$buildId = $sourceHash.Substring(0, 12)
function Get-Hash([byte[]] $bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha.Dispose() }
}
function Assert-Stopped {
    $running = $null
    try { $running = [Threading.Mutex]::OpenExisting('Local\WpsClockPortableV2') }
    catch [Threading.WaitHandleCannotBeOpenedException] { return }
    if ($null -ne $running) {
        $running.Dispose()
        throw 'WpsClock is running. Use its tray menu > Exit, then run this build again. Closing Settings is not Exit.'
    }
}
Write-Host ('WpsClock ' + $version + ' / source ' + $buildId)
Write-Host 'Checking the text package...'
$packText = [IO.File]::ReadAllText($env:WPSCLOCK_PACK)
$matches = [regex]::Matches($packText, '(?ms)^:WPSCLOCK_PAYLOAD_BEGIN\r?\n(.*?)^:WPSCLOCK_PAYLOAD_END\r?$')
if ($matches.Count -ne 1) { throw 'Payload missing, duplicated or truncated. Copy the complete text again.' }
$encoded = [regex]::Replace($matches[0].Groups[1].Value, '\s', '')
$packed = [Convert]::FromBase64String($encoded)
if ((Get-Hash $packed) -ne $payloadHash) { throw 'Payload checksum mismatch. Do not use this copy.' }
$inputStream = New-Object IO.MemoryStream(,$packed)
$outputStream = New-Object IO.MemoryStream
$gzip = New-Object IO.Compression.GZipStream($inputStream, [IO.Compression.CompressionMode]::Decompress)
try {
    $buffer = New-Object byte[] 8192
    while (($count = $gzip.Read($buffer, 0, $buffer.Length)) -gt 0) {
        if ($outputStream.Length + $count -gt $sourceLength) { throw 'Unexpected source length.' }
        $outputStream.Write($buffer, 0, $count)
    }
    $source = $outputStream.ToArray()
} finally { $gzip.Dispose(); $inputStream.Dispose(); $outputStream.Dispose() }
if ($source.Length -ne $sourceLength -or (Get-Hash $source) -ne $sourceHash) {
    throw 'Source checksum mismatch. Copy the complete text again.'
}
Write-Host 'OK: script, compressed payload and source checksums verified.'
Assert-Stopped
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not [IO.File]::Exists($compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not [IO.File]::Exists($compiler)) { throw 'Windows .NET Framework compiler not found. Ask IT for an approved build computer.' }
$parent = [IO.Path]::GetDirectoryName($env:WPSCLOCK_PACK)
$destination = Join-Path $parent ('WpsClock-' + $version + '-' + $buildId)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$lockPath = Join-Path $destination '.build.lock'
$lockStream = $null
$stage = $null
try {
    $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $stage = Join-Path $destination ('.build-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    $sourcePath = Join-Path $stage 'WpsClock.cs'
    $exeName = 'WpsClock-' + $version + '.exe'
    $exePath = Join-Path $stage $exeName
    $logPath = Join-Path $destination 'build.log'
    [IO.File]::WriteAllBytes($sourcePath, $source)
    Write-Host 'Compiling locally. No download or installation is performed...'
    $compileArgs = @('/nologo', '/langversion:5', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
        ('/out:' + $exePath), '/reference:System.dll', '/reference:System.Core.dll',
        '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
        '/reference:System.Xml.dll', '/reference:Microsoft.CSharp.dll', $sourcePath)
    & $compiler @compileArgs > $logPath 2>&1
    if ($LASTEXITCODE -ne 0 -or -not [IO.File]::Exists($exePath)) { throw ('Compilation failed. Read ' + $logPath) }
    Assert-Stopped
    $targetExe = Join-Path $destination $exeName
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $destination 'WpsClock.cs') -Force
    Copy-Item -LiteralPath $exePath -Destination $targetExe -Force
    # Reuse settings only from the folder holding this text pack. Never guess among old versions.
    $settingsName = 'WpsClock.settings.xml'
    $existingSettings = Join-Path $parent $settingsName
    $newSettings = Join-Path $destination $settingsName
    $settingsNote = 'No settings imported. The application will use its normal settings location.'
    if ([IO.File]::Exists($newSettings)) { $settingsNote = 'Existing destination settings preserved.' }
    elseif ([IO.File]::Exists($existingSettings)) {
        try {
            $document = New-Object Xml.XmlDocument
            $document.XmlResolver = $null
            $document.Load($existingSettings)
            if ($document.DocumentElement.Name -ne 'Settings') { throw 'Unexpected settings format.' }
            # Keep relative custom sound paths working after moving into the version folder.
            foreach ($name in @('NearFile', 'EndFile')) {
                $node = $document.SelectSingleNode('/Settings/' + $name)
                if ($null -ne $node -and $node.InnerText.Trim().Length -gt 0 -and -not [IO.Path]::IsPathRooted($node.InnerText)) {
                    $node.InnerText = [IO.Path]::GetFullPath((Join-Path $parent $node.InnerText))
                }
            }
            $settingsStage = Join-Path $stage $settingsName
            $document.Save($settingsStage)
            [IO.File]::Move($settingsStage, $newSettings)
            $settingsNote = 'Settings imported from: ' + $existingSettings
        } catch {
            $settingsNote = 'WARNING: settings were not imported: ' + $_.Exception.Message
            Write-Host $settingsNote
        }
    }
    $info = @(('WpsClock ' + $version), ('Source SHA256: ' + $sourceHash),
        ('Built locally: ' + [DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss')),
        ('EXE: ' + $exeName), $settingsNote)
    [IO.File]::WriteAllLines((Join-Path $destination 'BUILD-INFO.txt'), $info, [Text.Encoding]::UTF8)
    Write-Host ''
    Write-Host ('SUCCESS: ' + $targetExe)
    Write-Host $settingsNote
    Write-Host 'Run the EXE for daily use. This text build file is needed only when rebuilding.'
    try { Start-Process -FilePath 'explorer.exe' -ArgumentList ('"' + $destination + '"') | Out-Null }
    catch { Write-Host ('Open this folder manually: ' + $destination) }
} finally {
    if ($null -ne $stage -and [IO.Directory]::Exists($stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
    if ($null -ne $lockStream) { $lockStream.Dispose() }
}
