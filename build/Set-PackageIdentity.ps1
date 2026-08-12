[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$IdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [string]$PublisherDisplayName
)

$ErrorActionPreference = 'Stop'

$versionText = $Tag.Trim()
if ($versionText.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) {
    $versionText = $versionText.Substring(1)
}

if ($versionText -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Tag '$Tag' must use vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH.REVISION."
}

$parts = @($versionText.Split('.') | ForEach-Object { [int]$_ })
if ($parts.Count -eq 3) {
    $parts += 0
}

if ($parts | Where-Object { $_ -lt 0 -or $_ -gt 65535 }) {
    throw "Every MSIX version component must be between 0 and 65535: '$versionText'."
}

$packageVersion = $parts -join '.'
$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path

[xml]$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw
$identity = $manifest.Package.Identity
$properties = $manifest.Package.Properties

if ($null -eq $identity -or $null -eq $properties) {
    throw "The file '$resolvedManifestPath' is not a supported package manifest."
}

$identity.SetAttribute('Name', $IdentityName)
$identity.SetAttribute('Publisher', $Publisher)
$identity.SetAttribute('Version', $packageVersion)
$properties.PublisherDisplayName = $PublisherDisplayName

$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)

$writer = [System.Xml.XmlWriter]::Create($resolvedManifestPath, $settings)
try {
    $manifest.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Output $packageVersion
