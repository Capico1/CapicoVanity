# Windows build and verification

Build host: Windows x64, PowerShell 5.1+, .NET Framework 4.7.2+ (4.8 recommended), network access on first restore. Python, a CUDA SDK, a preexisting bin folder and the original developer's local response file are not required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Build-Share.ps1
```

The script reads explicit Compile, EmbeddedResource and Reference items from jmsTools.csproj. It restores pinned Roslyn 4.12.0, .NET 4.7.2 reference assemblies 1.0.3, NSIS 3.13 and all packages.config entries. Official HTTPS archive locations and SHA-256 digests are in packaging/Dependencies.lock.json. Restore verifies archives, extracts only inside the dependency directories, rejects linked directories and unsafe zip paths, and does not execute NuGet install scripts. Review the scripts and lock file before building. Source compilation is repeatable from a clean folder; timestamped package names and compression mean bit-for-bit identical release EXEs are not promised.

Results: dist/CapicoVanity-Win64-<timestamp>.exe, its ZIP and an EXE SHA-256 sidecar. The native outer launcher checks the installed .NET runtime without requiring .NET itself. The inner launcher verifies all embedded package files at every launch. User data is separated from the immutable cache.

Build tools and dependencies are restored into .local-build and packages. With a verified cache already present, use -Offline. A separate existing checkout may supply archive cache files through -CacheRoot C:\path\to\checkout; missing or mismatched archives fail instead of silently fetching in offline mode. The cache path is optional, and no fixed developer path is used.

The default package excludes the optional CUDA binary. To opt in, obtain the exact backend described in ThirdParty/ProVanity/README.md, place it at ThirdParty/ProVanity/provanity_cuda_standard.dll, and add -IncludeCuda. The build and runtime reject any other hash. CPU and SOL OpenCL remain available without that component. Installing a DLL into an already extracted immutable cache is rejected; build a new package instead.

## Verify

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Audit-Dependencies.ps1
```

This downloads NuGet's vulnerability data and evaluates the pinned runtime package versions against advisory version ranges. Network or feed failures fail the check. The JSON result stays in .local-build and is excluded from the source snapshot. No matching advisories is not a guarantee of safety.

After building, replace <build> below with the generated .local-build/share-<timestamp> folder:

```powershell
$compiler = '.local-build\compiler\tasks\net472\csc.exe'
& $compiler /nologo /out:.local-build\Verify-Share.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll packaging\Verify-Share.cs
& .local-build\Verify-Share.exe '<build>\app' --cpu
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Verify-Launcher.ps1 -BuildRoot '<build>'
```

The UI/CPU test runs with isolated temporary test data and public standard wallet test vectors. It checks the encrypted save path, CPU fallback, templates/stop counts, three-chain recovery and secret-display behavior. To exercise a real compatible OpenCL GPU, run the UI verification with --sol. For recovery buffer erasure, compile packaging/Verify-RecoveryBuffers.cs with the same compiler and run it with '<build>\app'. These GPU checks require actual compatible hardware and drivers.

Do not run verification against a personal wallet directory. Do not overwrite or remove a .NET installation to simulate a missing runtime: RuntimeBootstrapVerification.cs uses dedicated test builds; see packaging/RuntimeBootstrap.md.

## Clean source export

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Export-Source.ps1 -Destination C:\path\to\new-source-folder
```

The destination must not exist. Export copies explicit project source/resources and public documentation/tools only, hashes the source manifest, and fails on binary/private-file patterns or suspicious embedded credentials. It does not copy old Git history, bin/obj/packages, local audits or personal data. Export does not publish or create a GitHub repository. Licensing and retained attribution are described in LICENSE-STATUS.md.
