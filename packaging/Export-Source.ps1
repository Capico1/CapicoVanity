param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$Destination = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $Destination) { throw 'Export destination must be new; no files will be overwritten.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'jmsTools.csproj') -Encoding UTF8 -Raw
$ns = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
$ns.AddNamespace('m', $project.DocumentElement.NamespaceURI)
$files = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($node in $project.SelectNodes('//m:Compile | //m:EmbeddedResource', $ns)) { [void]$files.Add($node.Include.Replace('\','/')) }
foreach ($name in @('jmsTools.csproj','jmsTools.sln','App.config','packages.config','NuGet.Config','wordlist.txt','Properties/Settings.settings','README.md','BUILDING.md','LICENSE','RELEASE-NOTES.md','LICENSE-STATUS.md','THIRD-PARTY-NOTICES.md','.gitignore','.gitattributes','packaging/BrandIcon.cs','Assets/CapicoVanity.ico','Assets/CapicoVanity.png','ThirdParty/SolVanityCL/LICENSE','ThirdParty/SolVanityCL/README.md','ThirdParty/ProVanity/LICENSE','ThirdParty/ProVanity/README.md','packaging/Build-Share.ps1','packaging/Restore-Dependencies.ps1','packaging/Audit-Dependencies.ps1','packaging/Export-Source.ps1','packaging/Dependencies.lock.json','packaging/License-inventory.json','packaging/ShareLauncher.cs','packaging/RuntimeBootstrap.nsi','packaging/RuntimeBootstrap.md','packaging/RuntimeBootstrapVerification.cs','packaging/Verify-Share.cs','packaging/Verify-RecoveryBuffers.cs','packaging/Verify-OptionalCuda.cs','packaging/Verify-Launcher.ps1','packaging/使用说明.txt','docs/screenshots/vanity-templates.png','docs/screenshots/recovery-cpu.png','docs/screenshots/generated-results.png')) { [void]$files.Add($name) }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'LICENSES') -File) { [void]$files.Add('LICENSES/' + $file.Name) }
$manifest = @()
foreach ($name in ($files | Sort-Object)) {
    if ($name -match '(?i)(^|/)(\.git|bin|obj|packages|dist|VanityWallets|Deleted|PublicRpcNodes|\.local-build)(/|$)|\.(exe|dll|pfx|key|pem|zip|nupkg)$|(^|/)(BinanceApi\.txt|.*Tuning\.json|vanity-.*\.json|GpuCompute\.json|VanityGpu\.json|VanityOptions\.json)$') { throw "Private or binary path is not allowed: $name" }
    $source = [IO.Path]::GetFullPath((Join-Path $root $name))
    if (!$source.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Source path escaped workspace: $name" }
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Allowlisted source is missing: $name" }
    $current = Get-Item -LiteralPath $source -Force
    while ($current -and $current.FullName -ne $root) {
        if ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked source is not allowed: $name" }
        $current = if ($current -is [IO.FileInfo]) { $current.Directory } else { $current.Parent }
    }
    if ([IO.Path]::GetExtension($source) -in @('.cs','.config','.json','.ps1','.resx','.settings','.csproj','.sln','.md','.txt','.nsi')) {
        $text = [IO.File]::ReadAllText($source)
        if ($text -match '(?i)gh[pousr]_[a-z0-9]{20,}|github_pat_[a-z0-9_]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|https://[^\s/]+:[^\s/]+@|C:\\Users\\Capico|D:\\codex\\git') { throw "Possible credential or personal path in allowlisted source: $name (value omitted)" }
    }
    $manifest += [pscustomobject]@{ path=$name; sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() }
}
# Validation completes before the destination is created.
New-Item -ItemType Directory -Path $Destination | Out-Null
foreach ($entry in $manifest) {
    $target = Join-Path $Destination $entry.path
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $entry.path) -Destination $target
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Destination 'SOURCE-MANIFEST.json') -Encoding UTF8
Write-Host "Exported $($manifest.Count) allowlisted source files. No Git history, binaries, runtime data or private audit reports copied."
Write-Output "SOURCE=$Destination"
