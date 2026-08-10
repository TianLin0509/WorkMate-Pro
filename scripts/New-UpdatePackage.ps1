[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TargetExe,
    [string]$SourceExe,
    [string]$OutputDirectory,
    [string]$SigningKeyPath = 'C:\VibeData\WorkMatePro\Signing\workmate-update-private.xml',
    [string]$TargetVersion,
    [string]$FromVersion,
    [string]$ReleaseBaseUrl,
    [string]$ReleaseUrl,
    [string]$Notes = 'Offline update package with signature, baseline, and SHA-256 verification.',
    [switch]$AllowVersionOverrideForTest
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $scriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot 'artifacts\release' }
if (-not (Test-Path -LiteralPath $TargetExe -PathType Leaf)) { throw "Target EXE not found: $TargetExe" }
if (-not (Test-Path -LiteralPath $SigningKeyPath -PathType Leaf)) { throw "Update signing key not found: $SigningKeyPath" }
$TargetExe = (Resolve-Path -LiteralPath $TargetExe).Path
$SigningKeyPath = (Resolve-Path -LiteralPath $SigningKeyPath).Path
if (-not [string]::IsNullOrWhiteSpace($SourceExe)) {
    if (-not (Test-Path -LiteralPath $SourceExe -PathType Leaf)) { throw "Source EXE not found: $SourceExe" }
    $SourceExe = (Resolve-Path -LiteralPath $SourceExe).Path
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

function Get-ThreePartVersion([string]$Path) {
    $raw = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion
    $parsed = [Version]::new()
    if (-not [Version]::TryParse($raw, [ref]$parsed)) { throw "Invalid file version '$raw': $Path" }
    return '{0}.{1}.{2}' -f $parsed.Major, $parsed.Minor, $parsed.Build
}

$fileTargetVersion = Get-ThreePartVersion $TargetExe
if ([string]::IsNullOrWhiteSpace($TargetVersion)) { $TargetVersion = $fileTargetVersion }
$parsedTarget = [Version]::new()
if (-not [Version]::TryParse($TargetVersion, [ref]$parsedTarget)) { throw "Invalid target version: $TargetVersion" }
if (-not $AllowVersionOverrideForTest -and ([Version]$TargetVersion) -ne ([Version]$fileTargetVersion)) {
    throw "TargetVersion $TargetVersion does not match target EXE file version $fileTargetVersion."
}
if (-not [string]::IsNullOrWhiteSpace($SourceExe) -and [string]::IsNullOrWhiteSpace($FromVersion)) {
    $FromVersion = Get-ThreePartVersion $SourceExe
}
if (-not [string]::IsNullOrWhiteSpace($SourceExe)) {
    $fileSourceVersion = Get-ThreePartVersion $SourceExe
    $parsedSource = [Version]::new()
    if (-not [Version]::TryParse($FromVersion, [ref]$parsedSource)) { throw "Invalid source version: $FromVersion" }
    if (-not $AllowVersionOverrideForTest -and ([Version]$FromVersion) -ne ([Version]$fileSourceVersion)) {
        throw "FromVersion $FromVersion does not match source EXE file version $fileSourceVersion."
    }
    if ($parsedSource -ge $parsedTarget) { throw 'Target version must be newer than source version.' }
    if ((Get-FileHash -LiteralPath $SourceExe -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $TargetExe -Algorithm SHA256).Hash) {
        throw 'Source and target EXE files are byte-identical; there is no delta to publish.'
    }
}
if ([string]::IsNullOrWhiteSpace($ReleaseBaseUrl)) {
    $ReleaseBaseUrl = "https://github.com/TianLin0509/WorkMate-Pro/releases/download/v$TargetVersion"
}
if ([string]::IsNullOrWhiteSpace($ReleaseUrl)) {
    $ReleaseUrl = "https://github.com/TianLin0509/WorkMate-Pro/releases/tag/v$TargetVersion"
}
$ReleaseBaseUrl = $ReleaseBaseUrl.TrimEnd('/')

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$utf8 = [Text.UTF8Encoding]::new($false)
$privateXml = [IO.File]::ReadAllText($SigningKeyPath, [Text.Encoding]::UTF8)

function ConvertTo-JsonBytes($Value) {
    return $utf8.GetBytes(($Value | ConvertTo-Json -Depth 8 -Compress))
}

function New-SignatureText([byte[]]$Bytes) {
    $parameters = [Security.Cryptography.CspParameters]::new()
    $parameters.ProviderType = 24
    $rsa = [Security.Cryptography.RSACryptoServiceProvider]::new($parameters)
    try {
        $rsa.PersistKeyInCsp = $false
        $rsa.FromXmlString($privateXml)
        $signature = $rsa.SignData($Bytes, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
        return [Convert]::ToBase64String($signature)
    }
    finally { $rsa.Dispose() }
}

function Add-BytesEntry([IO.Compression.ZipArchive]$Archive, [string]$Name, [byte[]]$Bytes) {
    $entry = $Archive.CreateEntry($Name, [IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    try { $stream.Write($Bytes, 0, $Bytes.Length) }
    finally { $stream.Dispose() }
}

function Quote-NativeArgument([string]$Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

function New-SignedPackage(
    [string]$Kind,
    [string]$PayloadPath,
    [string]$PayloadName,
    [string]$PackageName,
    [string]$PackageFromVersion,
    [string]$PackageFromHash) {
    $payloadInfo = Get-Item -LiteralPath $PayloadPath
    $payloadHash = (Get-FileHash -LiteralPath $PayloadPath -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $TargetExe -Algorithm SHA256).Hash
    $manifest = [pscustomobject][ordered]@{
        SchemaVersion = 1
        ProductId = 'WorkMate-Pro'
        Kind = $Kind
        FromVersion = $PackageFromVersion
        FromSha256 = $PackageFromHash
        ToVersion = $TargetVersion
        ToSha256 = $targetHash
        PayloadFile = $PayloadName
        PayloadSha256 = $payloadHash
        PayloadSize = [long]$payloadInfo.Length
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        Notes = $Notes
    }
    $manifestBytes = ConvertTo-JsonBytes $manifest
    $signatureBytes = $utf8.GetBytes((New-SignatureText $manifestBytes) + "`n")
    $packagePath = Join-Path $OutputDirectory $PackageName
    if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
    $archive = [IO.Compression.ZipFile]::Open($packagePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Add-BytesEntry $archive 'manifest.json' $manifestBytes
        Add-BytesEntry $archive 'manifest.sig' $signatureBytes
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $PayloadPath,
            $PayloadName,
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    finally { $archive.Dispose() }
    return Get-Item -LiteralPath $packagePath
}

$temporary = Join-Path $OutputDirectory ('.update-temp-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $packages = [Collections.ArrayList]::new()
    if (-not [string]::IsNullOrWhiteSpace($SourceExe)) {
        $deltaPath = Join-Path $temporary 'WorkMate.delta'
        $quotedSource = Quote-NativeArgument $SourceExe
        $quotedTarget = Quote-NativeArgument $TargetExe
        $quotedDelta = Quote-NativeArgument $deltaPath
        $argumentLine = "--create-delta --source $quotedSource --target $quotedTarget --output $quotedDelta"
        $process = Start-Process -FilePath $TargetExe -ArgumentList $argumentLine -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $deltaPath)) {
            throw "MSDelta generation failed with exit code $($process.ExitCode)."
        }
        $deltaName = "WorkMate-delta-$FromVersion-to-$TargetVersion.workmate-update.zip"
        $sourceHash = (Get-FileHash -LiteralPath $SourceExe -Algorithm SHA256).Hash
        $deltaPackage = New-SignedPackage 'delta' $deltaPath 'WorkMate.delta' $deltaName $FromVersion $sourceHash
        [void]$packages.Add([pscustomobject][ordered]@{
            Kind = 'delta'
            FromVersion = $FromVersion
            FromSha256 = $sourceHash
            FileName = $deltaPackage.Name
            Url = "$ReleaseBaseUrl/$($deltaPackage.Name)"
            SizeBytes = [long]$deltaPackage.Length
            Sha256 = (Get-FileHash -LiteralPath $deltaPackage.FullName -Algorithm SHA256).Hash
        })
        $ratio = $deltaPackage.Length / [double](Get-Item -LiteralPath $TargetExe).Length
        Write-Host ('Delta package: {0} ({1:P1} of target EXE)' -f $deltaPackage.FullName, $ratio)
    }

    $fullName = "WorkMate-full-$TargetVersion.workmate-update.zip"
    $fullPackage = New-SignedPackage 'full' $TargetExe 'WorkMate.exe' $fullName '' ''
    [void]$packages.Add([pscustomobject][ordered]@{
        Kind = 'full'
        FromVersion = '*'
        FromSha256 = ''
        FileName = $fullPackage.Name
        Url = "$ReleaseBaseUrl/$($fullPackage.Name)"
        SizeBytes = [long]$fullPackage.Length
        Sha256 = (Get-FileHash -LiteralPath $fullPackage.FullName -Algorithm SHA256).Hash
    })
    Write-Host "Full fallback package: $($fullPackage.FullName)"

    $catalog = [pscustomobject][ordered]@{
        SchemaVersion = 1
        ProductId = 'WorkMate-Pro'
        LatestVersion = $TargetVersion
        PublishedUtc = [DateTime]::UtcNow.ToString('o')
        ReleaseUrl = $ReleaseUrl
        Notes = $Notes
        Packages = @($packages)
    }
    $catalogBytes = ConvertTo-JsonBytes $catalog
    $catalogPath = Join-Path $OutputDirectory 'update-catalog.json'
    $signaturePath = Join-Path $OutputDirectory 'update-catalog.sig'
    [IO.File]::WriteAllBytes($catalogPath, $catalogBytes)
    [IO.File]::WriteAllText($signaturePath, (New-SignatureText $catalogBytes) + "`n", $utf8)
    Write-Host "Signed catalog: $catalogPath"
}
finally {
    $outputRoot = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $temporaryFull = [IO.Path]::GetFullPath($temporary).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($temporaryFull.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $temporary)) {
        Remove-Item -LiteralPath $temporary -Recurse -Force
    }
}
