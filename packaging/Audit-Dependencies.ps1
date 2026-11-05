param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputPath) { $OutputPath = Join-Path $projectRoot '.local-build\dependency-audit.json' }
function In-Range([string]$Version, [string]$Range) {
    $value = [version]$Version
    $bounds = $Range.Substring(1, $Range.Length - 2).Split(',')
    if ($bounds.Count -eq 1) { return $value -eq [version]$bounds[0].Trim() }
    $low = $bounds[0].Trim(); $high = $bounds[1].Trim()
    if ($low) { $limit = [version]$low; if ($value -lt $limit -or ($value -eq $limit -and $Range[0] -eq '(')) { return $false } }
    if ($high) { $limit = [version]$high; if ($value -gt $limit -or ($value -eq $limit -and $Range[$Range.Length-1] -eq ')')) { return $false } }
    return $true
}
$index = Invoke-RestMethod -Uri 'https://api.nuget.org/v3/vulnerabilities/index.json'
if (!$index -or $index.Count -eq 0) { throw 'Vulnerability source returned no pages; audit did not run.' }
[xml]$packages = Get-Content -LiteralPath (Join-Path $projectRoot 'packages.config')
$findings = @()
foreach ($page in $index) {
    if (!$page.'@id'.StartsWith('https://api.nuget.org/')) { throw 'Unexpected audit data URL.' }
    $data = Invoke-RestMethod -Uri $page.'@id'
    foreach ($package in $packages.packages.package) {
        foreach ($advisory in $data.($package.id.ToLowerInvariant())) {
            if (In-Range $package.version $advisory.versions) {
                $findings += [pscustomobject]@{ package=$package.id; version=$package.version; severity=$advisory.severity; range=$advisory.versions; url=$advisory.url }
            }
        }
    }
}
$report = [ordered]@{ checkedAtUtc=[DateTime]::UtcNow.ToString('o'); source='https://api.nuget.org/v3/vulnerabilities/index.json'; packagesChecked=$packages.packages.package.Count; pages=@($index); findings=@($findings); limitation='Known NuGet advisories only. No finding is not a security guarantee; native engines and unlisted transitive packages need separate review.' }
New-Item -ItemType Directory -Path (Split-Path ([IO.Path]::GetFullPath($OutputPath)) -Parent) -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
if ($findings.Count -gt 0) { $findings | Format-Table; throw "Known vulnerable dependency versions detected. See $OutputPath" }
Write-Host "PASS: checked $($packages.packages.package.Count) pinned runtime packages against $($index.Count) NuGet vulnerability pages; no matching advisories."
