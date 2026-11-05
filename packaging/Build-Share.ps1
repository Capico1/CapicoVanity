param([string]$OutputRoot, [string]$CacheRoot, [switch]$Offline, [switch]$IncludeCuda)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputRoot) { $OutputRoot = $projectRoot }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
& (Join-Path $PSScriptRoot 'Restore-Dependencies.ps1') -CacheRoot $CacheRoot -Offline:$Offline
Push-Location $projectRoot
try {
    $buildId = Get-Date -Format 'yyyyMMdd-HHmmss'
    $buildRoot = Join-Path $OutputRoot ".local-build\share-$buildId"
    $payloadRoot = Join-Path $buildRoot 'app'
    $distRoot = Join-Path $OutputRoot 'dist'
    New-Item -ItemType Directory -Path $payloadRoot,$distRoot -Force | Out-Null
    $compiler = Join-Path $projectRoot '.local-build\compiler\tasks\net472\csc.exe'
    if (!(Test-Path $compiler)) { throw 'Local Roslyn compiler is missing.' }
    $appIcon = Join-Path $projectRoot 'Assets\CapicoVanity.ico'

    # Regenerate the current form resources, using the .NET Framework resource reader.
    $resourceToolSource = Join-Path $buildRoot 'ResourceBuilder.cs'
    @'
using System;
using System.Collections;
using System.IO;
using System.Resources;
class ResourceBuilder {
    static void Main(string[] args) {
        using (var reader = new ResXResourceReader(args[0]))
        using (var writer = new ResourceWriter(args[1])) {
            reader.BasePath = Path.GetDirectoryName(Path.GetFullPath(args[0]));
            foreach (DictionaryEntry entry in reader) writer.AddResource((string)entry.Key, entry.Value);
        }
    }
}
'@ | Set-Content -LiteralPath $resourceToolSource -Encoding utf8
    $resourceTool = Join-Path $buildRoot 'ResourceBuilder.exe'
    & $compiler /nologo /target:exe "/out:$resourceTool" /reference:System.Windows.Forms.dll $resourceToolSource
    if ($LASTEXITCODE -ne 0) { throw 'Resource builder compilation failed.' }
    & $resourceTool 'Form1.resx' (Join-Path $buildRoot 'Form1.resources')
    if ($LASTEXITCODE -ne 0) { throw 'Form resources failed.' }
    & $resourceTool 'Properties\Resources.resx' (Join-Path $buildRoot 'Resources.resources')
    if ($LASTEXITCODE -ne 0) { throw 'Application resources failed.' }
    [xml]$project = Get-Content 'jmsTools.csproj' -Raw -Encoding UTF8
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
    $namespaceManager.AddNamespace('m', $project.DocumentElement.NamespaceURI)
    $framework = Join-Path $projectRoot '.local-build\net472\build\.NETFramework\v4.7.2'
    $rsp = @('/nostdlib+', '/target:winexe', '/platform:x64', '/langversion:latest', '/optimize+', '/deterministic+')
    # The project is the only source list; no generated local response file is required.
    $rsp += '/reference:"' + (Join-Path $framework 'mscorlib.dll') + '"'
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $framework 'Facades') -Filter '*.dll') {
        $rsp += '/reference:"' + $file.FullName + '"'
    }
    foreach ($reference in $project.SelectNodes('//m:Reference', $namespaceManager)) {
        if ($reference.HintPath) { $path = Join-Path $projectRoot $reference.HintPath }
        else {
            $name = $reference.Include.Split(',')[0] + '.dll'
            $path = Join-Path $framework $name
            if (!(Test-Path -LiteralPath $path)) { $path = Join-Path (Join-Path $framework 'Facades') $name }
        }
        if (!(Test-Path -LiteralPath $path)) { throw "Missing project reference: $path" }
        $rsp += '/reference:"' + $path + '"'
    }
    foreach ($source in $project.SelectNodes('//m:Compile', $namespaceManager)) { $rsp += '"' + (Join-Path $projectRoot $source.Include) + '"' }
    foreach ($resource in $project.Project.ItemGroup.EmbeddedResource) {
        if ($resource.LogicalName) { $rsp += '/resource:"' + (Join-Path $projectRoot $resource.Include) + '",' + $resource.LogicalName }
    }
    $rsp += '/nologo'
    $rsp += '/win32icon:"' + $appIcon + '"'
    $rsp += '/out:"' + (Join-Path $payloadRoot 'jmsTools.exe') + '"'
    $rsp += '/resource:"' + (Join-Path $buildRoot 'Form1.resources') + '",jmsTools.Form1.resources'
    $rsp += '/resource:"' + (Join-Path $buildRoot 'Resources.resources') + '",jmsTools.Properties.Resources.resources'
    $responseFile = Join-Path $buildRoot 'share.rsp'
    $rsp | Set-Content -LiteralPath $responseFile -Encoding utf8
    & $compiler /noconfig "@$responseFile"
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }

    # Allowlist only build dependencies and clean defaults; never copy bin/Debug data.
    [xml]$project = Get-Content 'jmsTools.csproj' -Raw -Encoding UTF8
    foreach ($reference in $project.Project.ItemGroup.Reference) {
        if ($reference.HintPath) {
            Copy-Item -LiteralPath (Join-Path $projectRoot $reference.HintPath) -Destination $payloadRoot
        }
    }
    Copy-Item 'LICENSES' $payloadRoot -Recurse
    New-Item -ItemType Directory -Path (Join-Path $payloadRoot 'packaging'),(Join-Path $payloadRoot 'ThirdParty\SolVanityCL'),(Join-Path $payloadRoot 'ThirdParty\ProVanity') -Force | Out-Null
    Copy-Item 'packaging\Dependencies.lock.json','packaging\License-inventory.json' (Join-Path $payloadRoot 'packaging')
    Copy-Item 'RELEASE-NOTES.md','README.md','BUILDING.md' $payloadRoot
    New-Item -ItemType Directory -Path (Join-Path $payloadRoot 'Assets'),(Join-Path $payloadRoot 'docs\screenshots') -Force | Out-Null
    Copy-Item 'Assets\CapicoVanity.png','Assets\CapicoVanity.ico' (Join-Path $payloadRoot 'Assets')
    Copy-Item 'docs\screenshots\vanity-templates.png','docs\screenshots\recovery-cpu.png','docs\screenshots\generated-results.png' (Join-Path $payloadRoot 'docs\screenshots')
    Copy-Item 'ThirdParty\SolVanityCL\LICENSE','ThirdParty\SolVanityCL\README.md','ThirdParty\SolVanityCL\kernel.cl' (Join-Path $payloadRoot 'ThirdParty\SolVanityCL')
    Copy-Item 'ThirdParty\ProVanity\LICENSE','ThirdParty\ProVanity\README.md' (Join-Path $payloadRoot 'ThirdParty\ProVanity')
    Copy-Item 'App.config' (Join-Path $payloadRoot 'jmsTools.exe.config')
    Copy-Item 'wordlist.txt' $payloadRoot
    if ($IncludeCuda) {
        $cuda = Join-Path $projectRoot 'ThirdParty\ProVanity\provanity_cuda_standard.dll'
        if (!(Test-Path -LiteralPath $cuda) -or (Get-FileHash -LiteralPath $cuda -Algorithm SHA256).Hash -ne '51BE6F32B7E0F7044F39A86DD7E5B13957A719C0A0B9F12BDD40EDD389530D57') {
            throw 'Optional CUDA backend missing or checksum mismatch. See ThirdParty/ProVanity/README.md.'
        }
        Copy-Item -LiteralPath $cuda -Destination $payloadRoot
    }
    Copy-Item 'ThirdParty\ProVanity\LICENSE' (Join-Path $payloadRoot 'ProVanity-LICENSE.txt')
    Copy-Item 'ThirdParty\SolVanityCL\LICENSE' (Join-Path $payloadRoot 'SolVanityCL-LICENSE.txt')
    @('https://ethereum-rpc.publicnode.com') | Set-Content (Join-Path $payloadRoot 'Ethrpc_nodes.txt') -Encoding utf8
    @('https://bsc-dataseed.binance.org') | Set-Content (Join-Path $payloadRoot 'Bscrpc_nodes.txt') -Encoding utf8
    @('https://api.mainnet-beta.solana.com') | Set-Content (Join-Path $payloadRoot 'Solrpc_nodes.txt') -Encoding utf8
    Copy-Item (Join-Path $PSScriptRoot '使用说明.txt') $payloadRoot
    $payloadZip = Join-Path $buildRoot 'payload.zip'
    Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal
    $outputExe = Join-Path $distRoot "CapicoVanity-Win64-$(if ($IncludeCuda) { 'CUDA-' })$buildId.exe"
    $managedExe = Join-Path $buildRoot 'ShareLauncher.exe'
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$managedExe" "/win32icon:$appIcon" /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$payloadZip,JmsTools.Payload.zip" (Join-Path $PSScriptRoot 'ShareLauncher.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
    $nsis = Join-Path $projectRoot '.local-build\nsis-tool\nsis-3.13\makensis.exe'
    if (!(Test-Path -LiteralPath $nsis)) { throw 'NSIS 3.13 portable compiler is missing; see packaging/RuntimeBootstrap.md.' }
    & $nsis /V2 /INPUTCHARSET UTF8 "/DOUTPUT_EXE=$outputExe" "/DAPP_ICON=$appIcon" "/DMANAGED_EXE=$managedExe" (Join-Path $PSScriptRoot 'RuntimeBootstrap.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'Native runtime bootstrap compilation failed.' }
    Copy-Item (Join-Path $PSScriptRoot '使用说明.txt') (Join-Path $distRoot '使用说明.txt')
    $zipPath = [IO.Path]::ChangeExtension($outputExe, '.zip')
    Compress-Archive -LiteralPath $outputExe,(Join-Path $distRoot '使用说明.txt') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($outputExe))" | Set-Content ($outputExe + '.sha256.txt')
    Write-Output "EXE=$outputExe"
    Write-Output "ZIP=$zipPath"
    Write-Output "PAYLOAD=$payloadRoot"
} finally { Pop-Location }
