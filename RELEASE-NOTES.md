# Capico 靓号 v1.0.0

Windows x64 · ETH / BSC / Solana · Capico（X：@NanaWiti）

## 本版功能

- 前后缀与 54 种模板、附加模板及三种停止条件。
- EVM CPU、可选 NVIDIA CUDA；SOL CPU / OpenCL。
- GPU 自动检测、批次自动优化与无兼容设备时的 CPU 回退。
- 本地加密保存靓号私钥，独立的钱包恢复与命中列表。
- 公开地址余额查询，无需币安账户或 API 凭据。

## 下载文件

- 普通版：`CapicoVanity-Win64-<构建时间>.exe`，配套 ZIP 与 SHA-256 文件。
- CUDA 版：`CapicoVanity-Win64-CUDA-<构建时间>.exe`，配套 ZIP 与 SHA-256 文件。
- 对应源码：`CapicoVanity-v1.0.0-Source.zip`，附源码清单和 SHA-256 文件。

需要 .NET Framework 4.7.2 或更高版本。首次运行会检测环境；缺少时可打开微软官方下载页，程序不会自行安装运行时。CPU 模式无需安装 CUDA 或 OpenCL；GPU 模式需要兼容显卡和驱动。

## 已验证与边界

已在本机验证 CPU 回退、三链公开测试向量恢复、模板和停止条件、私钥加密保存、SOL OpenCL 实卡生成、可选 CUDA 生成及独立地址复核，以及启动缓存的篡改拒绝。测试使用 NVIDIA RTX 3050 Laptop GPU；其他显卡和驱动组合未逐一验证。具体速度与可用引擎以本机检测和实测为准。

本机验证与内部检查不构成第三方安全认证。预估时间不是命中保证。

## 数据兼容

产品名称为 Capico 靓号，X 账号 ID 为 @NanaWiti。为兼容旧版，分享版继续使用 `%LOCALAPPDATA%\NanaVanity\Data`。改名不自动搬移或删除旧钱包。DPAPI 加密文件依赖原 Windows 用户配置；换电脑或重装前需自行完成可恢复的加密备份。
