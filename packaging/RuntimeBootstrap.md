# Runtime bootstrap

The outer share EXE is compiled with NSIS 3.13 and does not require the CLR.
It checks Windows x64 and the HKLM .NET Framework v4 Full Release DWORD in
the 32-bit and 64-bit registry views before starting the managed extractor.
Release >= 461808 accepts .NET Framework 4.7.2 and later 4.x releases.
The threshold follows Microsoft's framework detection documentation.

If missing, the default choice is No. Choosing Yes opens only Microsoft's
.NET Framework 4.8 runtime download page. The bootstrap does not download,
install, elevate, or change the registry. Users reopen the EXE after installation.
This applies to the outer share EXE, not direct execution of jmsTools.exe.

## Build

Run `powershell -NoProfile -ExecutionPolicy Bypass -File packaging/Build-Share.ps1`.
The build restores NSIS 3.13, compiler 4.12.0, reference assemblies 1.0.3 and the runtime packages from the pinned HTTPS sources in Dependencies.lock.json. Every archive is checked against its SHA-256 before extraction. No local response file or preexisting bin directory is required. Restore executes no NuGet install scripts. See ../BUILDING.md.

## Verification

--check-runtime exits without unpacking, networking or opening windows:
0 = runtime available; 2 = missing/too old; 3 = unsupported Windows architecture.
--extract-only retains the managed extractor's test behavior after the check.

For regression tests only, compile a separate EXE with the
RUNTIME_TEST_RELEASE definition to test 0, 461807, 461808, 461814,
528040 and 533320. Never use that definition when building release artifacts.
RuntimeBootstrapVerification.cs verifies that declining the missing-runtime
dialog exits cleanly. Do not uninstall the developer machine's .NET to test this.
These checks simulate version values; they are not a clean OS/VM compatibility test.
