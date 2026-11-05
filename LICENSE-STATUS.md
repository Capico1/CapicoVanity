# License and provenance

The CapicoVanity combined distribution is licensed under GNU GPL version 3; see [LICENSE](LICENSE). Third-party components retain their own copyright and license notices.

- Base application: [Boom-Caoq/JmsTools](https://github.com/Boom-Caoq/JmsTools). On 2026-11-05, maintainer Capico (@NanaWiti) confirmed that authorization has been obtained for the planned publication. This records the maintainer's confirmation; no independent review of the authorization document is claimed. Preserve the original project's attribution.
- SOL OpenCL kernel: WincerChan/SolVanityCL, GPLv3. The full license and modified source are included in ThirdParty/SolVanityCL.
- Optional CUDA backend: WooMai/ProVanity, MIT. Its license and pinned binary provenance are retained in ThirdParty/ProVanity. The default package and clean source archive omit the prebuilt DLL; an explicit CUDA build checks its fixed SHA-256. The optional prebuilt binary is not independently audited.
- NuGet dependencies and build tools: versions, archive hashes, source references and license notices are recorded in packaging/License-inventory.json, packaging/Dependencies.lock.json and LICENSES. NativeWebSocket retains both original Apache notices and its fork's declared MIT notice. Retained upstream notices do not establish that every binary has been independently reproduced from source.

The source archive contains the application sources, modified OpenCL kernel, build scripts and third-party notices. Keep the corresponding source available with the distributed version. Branding and maintenance by Capico do not replace original or third-party authorship.
