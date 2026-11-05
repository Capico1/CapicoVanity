# SolVanityCL OpenCL kernel

Source: https://github.com/WincerChan/SolVanityCL

Upstream commit: `697719aadcaee76aac485f7a506299202d16d1f4` (retrieved 2026-11-01).

Upstream file: `core/opencl/kernel.cl`. License: GPL-3.0; full license included in LICENSE. The modified source is included here and embedded in the executable. Preserve the source and license when redistributing this component and comply with the upstream license.

JmsTools modifications (2026-11-01):

- Retain the upstream Ed25519/SHA-512 and Base58 routines.
- Remove the upstream search configuration and replace its entrypoint with `jms_sol_vanity`.
- Use a fresh cryptographically random 256-bit seed per batch. Each GPU lane adds its ID to the full-width seed with carry, modulo 2^256.
- Match both prefix and suffix, with an address-length check and optional ASCII case folding.
- Use an atomic compare/exchange so exactly one lane publishes a seed/public-key pair.
- Remove the empty `__generic` macro for NVIDIA Windows compilation, as in upstream's host adaptation.

The application hosts OpenCL directly from C# and does not execute the upstream Python CLI. The kernel compiles locally through the GPU driver. CUDA Toolkit, Python, and downloaded executables are not required at runtime. Tested on NVIDIA RTX 3050 Laptop GPU, driver 591.44. Other OpenCL implementations have not been verified.

Returned private seeds are re-derived using the existing Chaos.NaCl library, compared to the GPU public key, and checked with the Solana wallet library's sign/verify functions before display or save. The local review and tests are not a security audit.

SOL results are saved separately as `VanityWallets/vanity-sol-*.json`. Wallet secrets are protected using Windows DPAPI in CurrentUser mode; no plaintext private key or secretKey array is written for new results. Legacy files are verified and encrypted when loaded. DPAPI files require the original Windows user profile and are not portable backups. No network or balance queries occur during vanity generation.

Additional modifications (2026-11-05): added mnemonic batch derivation/matching integration, cleanup of recovery OpenCL buffers before release, clearing host temporary arrays, and system-directory-only loading of OpenCL.dll. Erasure is best effort when the driver or GPU has failed; managed strings and library-owned copies cannot be reliably wiped.
