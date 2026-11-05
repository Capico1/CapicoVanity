param([string]$CacheRoot, [switch]$Offline)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Assert-PlainPath([string]$Path) {
    $current = [IO.DirectoryInfo][IO.Path]::GetFullPath($Path)
    while ($current) {
        if ($current.Exists -and ($current.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked dependency directory is not allowed: $Path" }
        $current = $current.Parent
    }
}
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Dependencies.lock.json') -Raw | ConvertFrom-Json
foreach ($package in $lock.packages) {
    $destination = [IO.Path]::GetFullPath((Join-Path $projectRoot $package.destination))
    if (!$destination.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid dependency destination.' }
    Assert-PlainPath $destination
    $archive = Join-Path $projectRoot ('.local-build\downloads\' + $package.id + '.' + $package.version + '.zip')
    New-Item -ItemType Directory -Path (Split-Path $archive -Parent) -Force | Out-Null
    if (!(Test-Path -LiteralPath $archive) -and $CacheRoot) {
        $candidate = Join-Path $CacheRoot ('.local-build\downloads\' + $package.id + '.' + $package.version + '.zip')
        if (!(Test-Path -LiteralPath $candidate)) {
            $candidate = if ($package.id -eq 'NSIS') { Join-Path $CacheRoot '.local-build\nsis-3.13.zip' }
            elseif ($package.id -eq 'Microsoft.Net.Compilers.Toolset') { Join-Path $CacheRoot '.local-build\compiler.zip' }
            elseif ($package.id -eq 'Microsoft.NETFramework.ReferenceAssemblies.net472') { Join-Path $CacheRoot '.local-build\net472.zip' }
            else { Join-Path $CacheRoot ($package.destination + '/' + $package.id + '.' + $package.version + '.nupkg') }
        }
        if (Test-Path -LiteralPath $candidate) { Copy-Item -LiteralPath $candidate -Destination $archive }
    }
    if (!(Test-Path -LiteralPath $archive)) {
        if ($Offline) { throw "Offline dependency missing: $($package.id) $($package.version)" }
        Write-Host "Downloading $($package.id) $($package.version) from its official distribution..."
        $temporary = $archive + '.download'
        if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
            & curl.exe --fail --location --silent --show-error --connect-timeout 15 --max-time 90 --retry 2 --output $temporary $package.url
            if ($LASTEXITCODE -ne 0) { throw "Dependency download failed: $($package.id)" }
        } else { Invoke-WebRequest -UseBasicParsing -TimeoutSec 90 -Uri $package.url -OutFile $temporary }
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $package.sha256) { throw "Dependency checksum mismatch: $($package.id)" }
        Move-Item -LiteralPath $temporary -Destination $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.sha256) { throw "Dependency checksum mismatch: $($package.id)" }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.Contains(':')) { throw 'Invalid dependency archive entry.' }
            $target = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
            if (!$target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid dependency archive entry.' }
            Assert-PlainPath (Split-Path $target -Parent)
            if ($entry.Name.Length -eq 0) { New-Item -ItemType Directory -Path $target -Force | Out-Null; continue }
            if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked dependency file is not allowed.' }
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $zip.Dispose() }
}
Write-Host "Restored and verified $($lock.packages.Count) pinned dependency archives."
