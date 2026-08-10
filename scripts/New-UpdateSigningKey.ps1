[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$KeyDirectory = 'C:\VibeData\WorkMatePro\Signing',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$keyDirectoryFull = [IO.Path]::GetFullPath($KeyDirectory)
$privatePath = Join-Path $keyDirectoryFull 'workmate-update-private.xml'
$publicPath = Join-Path $keyDirectoryFull 'workmate-update-public.xml'
if ((Test-Path -LiteralPath $privatePath) -and -not $Force) {
    throw "Signing key already exists: $privatePath. Replacing it would make installed clients reject future updates; use -Force only for an intentional trust reset."
}
if (-not $PSCmdlet.ShouldProcess($keyDirectoryFull, 'Generate WorkMate update signing key')) { return }

New-Item -ItemType Directory -Path $keyDirectoryFull -Force | Out-Null
$parameters = [Security.Cryptography.CspParameters]::new()
$parameters.ProviderType = 24
$rsa = [Security.Cryptography.RSACryptoServiceProvider]::new(3072, $parameters)
try {
    $rsa.PersistKeyInCsp = $false
    [IO.File]::WriteAllText($privatePath, $rsa.ToXmlString($true), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($publicPath, $rsa.ToXmlString($false), [Text.UTF8Encoding]::new($false))
}
finally { $rsa.Dispose() }

# The private key is readable only by the current Windows identity.
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$acl = [Security.AccessControl.FileSecurity]::new()
$acl.SetOwner($identity.User)
$acl.SetAccessRuleProtection($true, $false)
$rule = [Security.AccessControl.FileSystemAccessRule]::new(
    $identity.Name,
    [Security.AccessControl.FileSystemRights]::FullControl,
    [Security.AccessControl.AccessControlType]::Allow)
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $privatePath -AclObject $acl

$fingerprint = (Get-FileHash -LiteralPath $publicPath -Algorithm SHA256).Hash
Write-Host "Private key: $privatePath"
Write-Host "Public key:  $publicPath"
Write-Host "Public-key file SHA256: $fingerprint"
