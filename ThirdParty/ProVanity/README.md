# ProVanity CUDA backend

Used only for local EVM vanity-address generation, through its public-key-only native API.

- Upstream: https://github.com/WooMai/ProVanity
- Downloaded CLI: `https://github.com/WooMai/ProVanity/releases/latest/download/provanity-windows-amd64.exe`
- Download date: 2026-11-01; executable reports version `1.1.2`.
- CLI SHA256: `4c7382d666b2cf86d590532c9f2ce68b23520db725ce5b4e9da05398d31e1bd1`
- Backend extracted by the upstream CLI's benchmark command: `provanity_cuda_standard.dll`.
- Backend SHA256: `51be6f32b7e0f7044f39a86dd7e5b13957a719c0a0b9f12bdd40edd389530d57`
- Native API version: `provanity-cuda/3` (checked before use).
- Source inspected at commit `1d45939078fb62b12eeb8894d08fe7506ded596d`, including native header and Windows ABI wrapper. This was an integration review, not a security audit.
- License: MIT; see LICENSE.

The DLL is optional and excluded from the default project/build output. An explicit `-IncludeCuda` package build (or MSBuild `IncludeCuda=true`) requires the locally supplied pinned DLL. Both packaging and runtime verify the SHA-256 above before use. Runtime loads only from the application folder and resolves dependencies from Windows System32. Add it before packaging; do not modify the extracted immutable program cache. No automatic downloads occur at runtime. Windows x64 and a compatible NVIDIA CUDA driver are required. No CUDA SDK or separate CLI installation is needed at runtime. Unsupported systems retain CPU generation.

The application creates a cryptographically random seed using the existing Nethereum library. Only its uncompressed public key is passed to CUDA. A returned scalar offset is added modulo the secp256k1 order, then Nethereum re-derives the address and validates the requested prefix/suffix. EIP-55 case mismatches cause a new seed/search. No balance or network queries are involved.

The native API only scores a single fixed pattern. With additional EVM templates selected, the host rotates concrete searches with a five-second budget per template, or advances after a result. SOL can match additional templates within a single GPU search. Each EVM result is independently checked before Windows DPAPI encryption and saving under `VanityWallets`.
