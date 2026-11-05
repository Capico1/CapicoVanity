# Third-party notices

CapicoVanity derives from [Boom-Caoq/JmsTools](https://github.com/Boom-Caoq/JmsTools). Maintainer Capico (@NanaWiti) confirmed authorization for the planned publication on 2026-11-05. The original project's authorship is preserved. The combined distribution uses GNU GPL v3; see [LICENSE](LICENSE) and [license and provenance](LICENSE-STATUS.md).

The modified SOL kernel is from WincerChan/SolVanityCL, commit `697719aadcaee76aac485f7a506299202d16d1f4`. Preserve its GPLv3 license and corresponding modified source: [license](ThirdParty/SolVanityCL/LICENSE), [source](ThirdParty/SolVanityCL/kernel.cl), [modifications](ThirdParty/SolVanityCL/README.md). The combined distribution uses GNU GPL v3; component notices remain applicable.

Optional CUDA integration: WooMai/ProVanity, commit `1d45939078fb62b12eeb8894d08fe7506ded596d`, MIT. [License](ThirdParty/ProVanity/LICENSE). No CUDA binary is included in the default package or clean source snapshot. The optional prebuilt binary is not independently audited.

The native launcher is built using NSIS 3.13. [NSIS license and component notices](LICENSES/NSIS-COPYING.txt). Build dependencies and archive SHA-256 values are pinned in [Dependencies.lock.json](packaging/Dependencies.lock.json). Tool package license notices are also retained in LICENSES.

NuGet package license metadata and embedded notices follow. NativeWebSocket also retains [original upstream Apache notices](LICENSES/NativeWebSocket-original-Apache-2.0.txt). Some binary packages do not identify an exact source commit; upstream text snapshots are notice preservation, not proof of complete provenance.

A package declaring MIT without bundling its text is supplemented with an upstream license snapshot where available; these snapshots do not replace provenance verification. License source URLs, hashes, authors, and repository commits are recorded in [License-inventory.json](packaging/License-inventory.json).

| Package | Version | License metadata | Included notices |
|---|---|---|---|
| ADRaffy.ENSNormalize | 0.3.1 | MIT | [ADRaffy.ENSNormalize-upstream-LICENSE.txt](LICENSES/ADRaffy.ENSNormalize-upstream-LICENSE.txt) |
| Chaos.NaCl.Standard | 1.0.0 | See upstream license | [Chaos.NaCl.Standard-upstream-LICENSE.txt](LICENSES/Chaos.NaCl.Standard-upstream-LICENSE.txt) |
| IndexRange | 1.0.3 | MIT | [IndexRange-upstream-LICENSE.txt](LICENSES/IndexRange-upstream-LICENSE.txt) |
| Microsoft.Bcl.AsyncInterfaces | 9.0.2 | MIT | [Microsoft.Bcl.AsyncInterfaces-LICENSE.TXT](LICENSES/Microsoft.Bcl.AsyncInterfaces-LICENSE.TXT), [Microsoft.Bcl.AsyncInterfaces-THIRD-PARTY-NOTICES.TXT](LICENSES/Microsoft.Bcl.AsyncInterfaces-THIRD-PARTY-NOTICES.TXT) |
| Microsoft.Extensions.Logging.Abstractions | 6.0.0 | MIT | [Microsoft.Extensions.Logging.Abstractions-LICENSE.TXT](LICENSES/Microsoft.Extensions.Logging.Abstractions-LICENSE.TXT), [Microsoft.Extensions.Logging.Abstractions-THIRD-PARTY-NOTICES.TXT](LICENSES/Microsoft.Extensions.Logging.Abstractions-THIRD-PARTY-NOTICES.TXT) |
| native-websocket | 0.0.3 | MIT | [native-websocket-upstream-LICENSE.txt](LICENSES/native-websocket-upstream-LICENSE.txt) |
| NBitcoin | 7.0.6 | MIT | [NBitcoin-upstream-LICENSE.txt](LICENSES/NBitcoin-upstream-LICENSE.txt) |
| Nethereum.ABI | 7.0.0 | LICENSE.md | [Nethereum.ABI-LICENSE.md](LICENSES/Nethereum.ABI-LICENSE.md) |
| Nethereum.Accounts | 7.0.0 | LICENSE.md | [Nethereum.Accounts-LICENSE.md](LICENSES/Nethereum.Accounts-LICENSE.md) |
| Nethereum.BlockchainProcessing | 7.0.0 | LICENSE.md | [Nethereum.BlockchainProcessing-LICENSE.md](LICENSES/Nethereum.BlockchainProcessing-LICENSE.md) |
| Nethereum.Contracts | 7.0.0 | LICENSE.md | [Nethereum.Contracts-LICENSE.md](LICENSES/Nethereum.Contracts-LICENSE.md) |
| Nethereum.HdWallet | 7.0.0 | LICENSE.md | [Nethereum.HdWallet-LICENSE.md](LICENSES/Nethereum.HdWallet-LICENSE.md) |
| Nethereum.Hex | 7.0.0 | LICENSE.md | [Nethereum.Hex-LICENSE.md](LICENSES/Nethereum.Hex-LICENSE.md) |
| Nethereum.JsonRpc.Client | 7.0.0 | LICENSE.md | [Nethereum.JsonRpc.Client-LICENSE.md](LICENSES/Nethereum.JsonRpc.Client-LICENSE.md) |
| Nethereum.JsonRpc.RpcClient | 7.0.0 | LICENSE.md | [Nethereum.JsonRpc.RpcClient-LICENSE.md](LICENSES/Nethereum.JsonRpc.RpcClient-LICENSE.md) |
| Nethereum.KeyStore | 7.0.0 | LICENSE.md | [Nethereum.KeyStore-LICENSE.md](LICENSES/Nethereum.KeyStore-LICENSE.md) |
| Nethereum.Merkle.Patricia | 7.0.0 | LICENSE.md | [Nethereum.Merkle.Patricia-LICENSE.md](LICENSES/Nethereum.Merkle.Patricia-LICENSE.md) |
| Nethereum.Model | 7.0.0 | LICENSE.md | [Nethereum.Model-LICENSE.md](LICENSES/Nethereum.Model-LICENSE.md) |
| Nethereum.RLP | 7.0.0 | LICENSE.md | [Nethereum.RLP-LICENSE.md](LICENSES/Nethereum.RLP-LICENSE.md) |
| Nethereum.RPC | 7.0.0 | LICENSE.md | [Nethereum.RPC-LICENSE.md](LICENSES/Nethereum.RPC-LICENSE.md) |
| Nethereum.Signer | 7.0.0 | LICENSE.md | [Nethereum.Signer-LICENSE.md](LICENSES/Nethereum.Signer-LICENSE.md) |
| Nethereum.Signer.EIP712 | 7.0.0 | LICENSE.md | [Nethereum.Signer.EIP712-LICENSE.md](LICENSES/Nethereum.Signer.EIP712-LICENSE.md) |
| Nethereum.Util | 7.0.0 | LICENSE.md | [Nethereum.Util-LICENSE.md](LICENSES/Nethereum.Util-LICENSE.md) |
| Nethereum.Util.Rest | 7.0.0 | LICENSE.md | [Nethereum.Util.Rest-LICENSE.md](LICENSES/Nethereum.Util.Rest-LICENSE.md) |
| Nethereum.Web3 | 7.0.0 | LICENSE.md | [Nethereum.Web3-LICENSE.md](LICENSES/Nethereum.Web3-LICENSE.md) |
| Newtonsoft.Json | 13.0.3 | MIT | [Newtonsoft.Json-LICENSE.md](LICENSES/Newtonsoft.Json-LICENSE.md) |
| Portable.BouncyCastle | 1.9.0 | See upstream license | [Portable.BouncyCastle-upstream-LICENSE.html](LICENSES/Portable.BouncyCastle-upstream-LICENSE.html) |
| Solana.Unity.Rpc | 2.6.1.3 | MIT | [Solana.Unity.Rpc-upstream-LICENSE.txt](LICENSES/Solana.Unity.Rpc-upstream-LICENSE.txt) |
| Solana.Unity.Wallet | 2.6.1.3 | MIT | [Solana.Unity.Wallet-upstream-LICENSE.txt](LICENSES/Solana.Unity.Wallet-upstream-LICENSE.txt) |
| System.Buffers | 4.5.1 | See upstream license | [System.Buffers-LICENSE.TXT](LICENSES/System.Buffers-LICENSE.TXT), [System.Buffers-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Buffers-THIRD-PARTY-NOTICES.TXT) |
| System.Collections.Immutable | 6.0.0 | MIT | [System.Collections.Immutable-LICENSE.TXT](LICENSES/System.Collections.Immutable-LICENSE.TXT), [System.Collections.Immutable-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Collections.Immutable-THIRD-PARTY-NOTICES.TXT) |
| System.IO | 4.3.0 | See upstream license | [System.IO-dotnet_library_license.txt](LICENSES/System.IO-dotnet_library_license.txt), [System.IO-ThirdPartyNotices.txt](LICENSES/System.IO-ThirdPartyNotices.txt) |
| System.IO.Pipelines | 9.0.2 | MIT | [System.IO.Pipelines-LICENSE.TXT](LICENSES/System.IO.Pipelines-LICENSE.TXT), [System.IO.Pipelines-THIRD-PARTY-NOTICES.TXT](LICENSES/System.IO.Pipelines-THIRD-PARTY-NOTICES.TXT) |
| System.Memory | 4.5.5 | See upstream license | [System.Memory-LICENSE.TXT](LICENSES/System.Memory-LICENSE.TXT), [System.Memory-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Memory-THIRD-PARTY-NOTICES.TXT) |
| System.Numerics.Vectors | 4.5.0 | See upstream license | [System.Numerics.Vectors-LICENSE.TXT](LICENSES/System.Numerics.Vectors-LICENSE.TXT), [System.Numerics.Vectors-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Numerics.Vectors-THIRD-PARTY-NOTICES.TXT) |
| System.Runtime | 4.3.0 | See upstream license | [System.Runtime-dotnet_library_license.txt](LICENSES/System.Runtime-dotnet_library_license.txt), [System.Runtime-ThirdPartyNotices.txt](LICENSES/System.Runtime-ThirdPartyNotices.txt) |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | MIT | [System.Runtime.CompilerServices.Unsafe-LICENSE.TXT](LICENSES/System.Runtime.CompilerServices.Unsafe-LICENSE.TXT), [System.Runtime.CompilerServices.Unsafe-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Runtime.CompilerServices.Unsafe-THIRD-PARTY-NOTICES.TXT) |
| System.Security.Cryptography.Algorithms | 4.3.1 | See upstream license | [System.Security.Cryptography.Algorithms-dotnet_library_license.txt](LICENSES/System.Security.Cryptography.Algorithms-dotnet_library_license.txt), [System.Security.Cryptography.Algorithms-ThirdPartyNotices.txt](LICENSES/System.Security.Cryptography.Algorithms-ThirdPartyNotices.txt) |
| System.Security.Cryptography.Encoding | 4.3.0 | See upstream license | [System.Security.Cryptography.Encoding-dotnet_library_license.txt](LICENSES/System.Security.Cryptography.Encoding-dotnet_library_license.txt), [System.Security.Cryptography.Encoding-ThirdPartyNotices.txt](LICENSES/System.Security.Cryptography.Encoding-ThirdPartyNotices.txt) |
| System.Security.Cryptography.Primitives | 4.3.0 | See upstream license | [System.Security.Cryptography.Primitives-dotnet_library_license.txt](LICENSES/System.Security.Cryptography.Primitives-dotnet_library_license.txt), [System.Security.Cryptography.Primitives-ThirdPartyNotices.txt](LICENSES/System.Security.Cryptography.Primitives-ThirdPartyNotices.txt) |
| System.Text.Encodings.Web | 9.0.2 | MIT | [System.Text.Encodings.Web-LICENSE.TXT](LICENSES/System.Text.Encodings.Web-LICENSE.TXT), [System.Text.Encodings.Web-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Text.Encodings.Web-THIRD-PARTY-NOTICES.TXT) |
| System.Text.Json | 9.0.2 | MIT | [System.Text.Json-LICENSE.TXT](LICENSES/System.Text.Json-LICENSE.TXT), [System.Text.Json-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Text.Json-THIRD-PARTY-NOTICES.TXT) |
| System.Threading.Tasks.Extensions | 4.5.4 | See upstream license | [System.Threading.Tasks.Extensions-LICENSE.TXT](LICENSES/System.Threading.Tasks.Extensions-LICENSE.TXT), [System.Threading.Tasks.Extensions-THIRD-PARTY-NOTICES.TXT](LICENSES/System.Threading.Tasks.Extensions-THIRD-PARTY-NOTICES.TXT) |
| System.ValueTuple | 4.5.0 | See upstream license | [System.ValueTuple-LICENSE.TXT](LICENSES/System.ValueTuple-LICENSE.TXT), [System.ValueTuple-THIRD-PARTY-NOTICES.TXT](LICENSES/System.ValueTuple-THIRD-PARTY-NOTICES.TXT) |
| websocket-sharp-latest | 1.0.2 | MIT | [websocket-sharp-latest-upstream-LICENSE.txt](LICENSES/websocket-sharp-latest-upstream-LICENSE.txt) |
| Nethereum.EVM | 7.0.0 | LICENSE.md | [Nethereum.EVM-LICENSE.md](LICENSES/Nethereum.EVM-LICENSE.md) |
