<div align="center">

# 🧹 清理优化大师

**安全、透明、可恢复的 Windows 清理与系统优化工具**

[![Release](https://img.shields.io/github/v/release/smftec/cleanmaster-windows?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC)](../../releases/latest)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Windows-10%2022H2%20%2F%2011-0078D4.svg)](#-快速开始)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](#-快速开始)

[简体中文](README.md) | [English](README.en.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md)

**清得明白，优化得安心** —— 可解释、可预览、可恢复，拒绝一切虚假加速。

[⬇️ 下载最新版](../../releases/latest) · [🛠 从源码构建](#-快速开始) · [💬 联系我们](#-联系我们)

</div>

---

## ✨ 界面预览

| 浅色主题 | 深色主题 |
|---|---|
| ![浅色主题](docs/images/home-light.png) | ![深色主题](docs/images/home-dark.png) |

## ⬇️ 下载

到 [Releases](../../releases/latest) 页面获取（SHA256 校验值见附件 `SHA256SUMS.txt`）：

| 文件 | 适合谁 |
|---|---|
| `CleanMasterSetup-x.x.x.exe` | 大多数用户：中文安装向导，支持卸载 |
| `CleanMaster-x.x.x-win-x64.zip` | 免安装绿色版：解压即用，无需 .NET 运行时（自包含） |

## 🧰 功能总览

| 模块 | 能力 |
|---|---|
| **首页** | 设备状态模型（良好 / 建议清理 / 空间紧张）、垃圾与启动项速览、磁盘概览、最近活动、智能建议一键清理 |
| **智能扫描** | 一次覆盖系统垃圾 / 浏览器缓存 / 应用缓存 / 回收站；结果按风险分组、逐条可预览文件明细 |
| **清理空间** | 系统垃圾（临时文件、崩溃报告、缩略图、着色器缓存、更新缓存等）、应用缓存（微信 / QQ / Discord / Teams / Steam / Epic / VS Code / JetBrains 等 13 类）、浏览器清理（Edge / Chrome / Brave / Opera / Firefox）、隐私痕迹（默认不勾选）、回收站 |
| **空间分析** | 多线程全盘扫描、分类统计、文件夹占用排行树、大文件查找（打开位置 / 移入回收站 / 白名单） |
| **启动项** | 注册表 Run / 启动文件夹 / 登录计划任务三大来源；启停机制与任务管理器一致；所有变更可恢复 |
| **应用管理** | 卸载表完整枚举、调用应用自带卸载程序、卸载后确定性残留检测（进隔离区可恢复） |
| **工具箱** | 隔离区管理、文件粉碎、重复文件检测（内容哈希，非文件名匹配）、清理历史、Windows 官方快捷工具 |
| **系统集成** | 深色 / 浅色 / 跟随系统主题、系统托盘、首次使用引导、自动清理计划 |

## 🔒 安全设计（产品核心）

- **统一删除保护**：所有删除必须经 `SafetyGuard` 校验；桌面、文档、图片、下载、System32、WinSxS、Program Files 等在硬编码拒绝表中，任何规则都碰不到
- **风险四级**：安全 / 低风险默认勾选；需确认项（回收站、更新缓存、隐私项）永远不自动选中
- **隔离区可恢复**：非缓存文件先进隔离区（默认保留 30 天），可随时恢复，重名自动处理
- **不跨目录链接**：目录遍历与删除均不跟随 Junction / 符号链接
- **可解释**：每一项都说明"为什么可清理、清理后有什么影响、能否恢复、哪个进程正在占用"
- **自动化安全自测**：`CleanMaster.exe --selftest` 覆盖 25 项安全断言，作为发布门禁集成进 CI

## 🚀 快速开始

环境要求：Windows 10 22H2+ / Windows 11，[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（零第三方 NuGet 依赖）。

```bash
git clone https://github.com/smftec/cleanmaster-windows.git
cd cleanmaster-windows
dotnet build src/CleanMaster.App/CleanMaster.App.csproj -c Release
# 运行
src/CleanMaster.App/bin/Release/net8.0-windows/CleanMaster.exe
# 安全自测
src/CleanMaster.App/bin/Release/net8.0-windows/CleanMaster.exe --selftest
```

推送 `v*` 标签后，GitHub Actions 会自动：构建 → 安全自测 → 发布 → 编译安装包 → 生成校验和 → 创建 Release 草稿。

## 📦 打包发布

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
```

产出：绿色版目录、zip 包、中文安装包（自动检测 [Inno Setup](https://jrsoftware.org/isinfo.php)，安装包脚本见 [scripts/setup.iss](scripts/setup.iss)）、`SHA256SUMS.txt`。

拿到代码签名证书后，加两个环境变量即可输出签名版全套产物：

```powershell
$env:CM_SIGN = "1"; $env:CM_SIGN_SUBJECT = "CN=你的名称"
powershell -File scripts\publish.ps1
```

## 🗺️ Roadmap

- [ ] SignPath Foundation 开源签名（已提交申请路线）
- [ ] 在线清理规则库（签名校验的热更新）
- [ ] 启动耗时实测分析（ETW）
- [ ] Microsoft Store 分发

## 💬 联系我们

- 🌐 官网：<https://52xn.com>
- 📮 邮箱：<dev@52xn.com>
- 💚 微信公众号：微信搜一搜「**AI虚拟助理**」

<div align="center">
  <img src="docs/images/wechat-search.png" alt="微信搜一搜：AI虚拟助理" width="640"/>
</div>

## 📄 开源许可

本项目以 **GPL-3.0** 许可证开源（见 [LICENSE](LICENSE)）：可自由使用、修改、分发，衍生作品需同样开源；商业授权 / 闭源定制请联系 <dev@52xn.com>。

发布产物中的代码签名由 [SignPath Foundation](https://signpath.org) 面向开源项目免费提供（接入中）。
