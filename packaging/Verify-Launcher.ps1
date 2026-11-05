param([Parameter(Mandatory=$true)][string]$BuildRoot)
$ErrorActionPreference = 'Stop'
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
$launcher = Join-Path $BuildRoot 'ShareLauncher.exe'
$payload = Join-Path $BuildRoot 'payload.zip'
$hash = [Security.Cryptography.SHA256]::Create()
try { $bytes = $hash.ComputeHash([IO.File]::ReadAllBytes($payload)) } finally { $hash.Dispose() }
$version = [Convert]::ToBase64String($bytes).Replace('/','_').Replace('+','-').TrimEnd('=')
$cache = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('NanaVanity\Programs\' + $version)
function Run-Launcher([string]$Flag, [int]$Expected) {
    $process = Start-Process -FilePath $launcher -ArgumentList $Flag -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne $Expected) { throw "Launcher $Flag expected $Expected, got $($process.ExitCode)" }
}
Run-Launcher '--extract-only' 0
Run-Launcher '--verify-only' 0
Write-Host 'PASS: fresh embedded payload extracts and all files verify'
$probe = Join-Path $cache 'wordlist.txt'
$original = [IO.File]::ReadAllBytes($probe)
try {
    [IO.File]::WriteAllText($probe, 'tampered test fixture')
    Run-Launcher '--verify-only' 1
    Write-Host 'PASS: changed package file is rejected'
} finally { [IO.File]::WriteAllBytes($probe, $original) }
$injected = Join-Path $cache ('unexpected-' + [Guid]::NewGuid().ToString('N') + '.dll')
try {
    [IO.File]::WriteAllText($injected, 'unlisted test fixture')
    Run-Launcher '--verify-only' 1
    Write-Host 'PASS: unlisted native library is rejected'
} finally { if (Test-Path -LiteralPath $injected) { Remove-Item -LiteralPath $injected } }
$missing = Join-Path $cache 'jmsTools.exe.config'
$original = [IO.File]::ReadAllBytes($missing)
try {
    Remove-Item -LiteralPath $missing
    Run-Launcher '--verify-only' 1
    Write-Host 'PASS: missing configuration is rejected'
} finally { [IO.File]::WriteAllBytes($missing, $original) }
Run-Launcher '--verify-only' 0
Write-Host 'PASS: original payload verifies after test restoration'
