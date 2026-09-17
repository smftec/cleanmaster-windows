# 清理优化大师 (CleanMaster) for Windows

> 一款面向普通用户与进阶用户的 **安全清理、空间分析、启动项管理和系统维护工具**。
> 清得明白，优化得安心 —— 可解释、可预览、可恢复，不搞虚假加速。

![技术栈](https://img.shields.io/badge/.NET-8.0--windows-blue) ![UI](https://img.shields.io/badge/WPF-Fluent风格-green) ![平台](https://img.shields.io/badge/Windows-10%2022H2%20/%2011-lightgrey)

---

## ✨ 功能总览

| 模块 | 能力 |
|---|---|
| **首页** | 设备状态模型（良好/建议清理/空间紧张）、四项状态卡片、磁盘概览、空间分析色块、最近活动、智能建议一键清理 |
| **智能扫描** | 一次扫描：系统垃圾 / 浏览器缓存 / 应用缓存 / 回收站；结果按"安全清理 / 浏览器与应用缓存 / 建议检查"分组；逐条预览文件明细；不使用"一键修复全部"式伪交互 |
| **清理空间** | 五个分组：系统垃圾（临时文件/WER 崩溃报告/缩略图/DirectX 着色器缓存/系统日志/更新下载缓存/传递优化）、应用缓存（微信/QQ/Discord/Teams/Office/Steam/Epic/VS Code/NVIDIA/JetBrains/开发工具缓存等 13 类）、浏览器清理（Edge/Chrome/Brave/Opera/Firefox）、隐私痕迹（默认全不勾选）、回收站（SHQueryRecycleBin 统计 + 一键清空） |
| **空间分析** | 多线程磁盘扫描、分类统计（系统/应用/图片/视频/音频/文档/压缩包/其他）、目录占用排行树、大文件查找（>500MB/1GB/5GB 筛选、打开位置/移入回收站/白名单） |
| **启动项** | 注册表 Run（HKCU/HKLM）/ 启动文件夹 / 登录触发计划任务三大来源；与任务管理器一致的 StartupApproved 启停机制；影响评估 + 建议；所有变更可恢复 |
| **应用管理** | 注册表卸载表 + WOW6432Node；按名称/大小/安装日期排序；调用应用自带卸载程序；卸载后确定性残留检测（目录进隔离区、注册表键先导出备份再删） |
| **工具箱** | 清理隔离区（恢复/永久删除/到期自动清理）、文件粉碎（3 次覆写 + 诚实的 SSD 说明）、重复文件（大小分组→首 64KB 哈希→全量 MD5）、清理历史、Windows 官方快捷工具 ×8 |
| **设置** | 通用（开机启动/托盘/主题 浅色·深色·跟随系统）、清理（隔离区开关与天数/自动清理计划）、排除目录与白名单、通知、高级（扫描线程数等）、隐私 |
| **系统集成** | 系统托盘（打开/快速扫描/自动清理开关/退出）、首启三步引导、按需 UAC 提权（仅清传递优化缓存/HKLM 启动项/计划任务时） |

## 🔒 安全设计（本产品的核心）

- **SafetyGuard 统一删除保护**：所有删除必须经 `SafetyGuard.ValidateForClean`；硬编码拒绝表保护 桌面/文档/图片/视频/下载/用户主目录/System32/WinSxS/Installer/Program Files/磁盘根目录/应用自身目录；目标必须位于规则声明的允许根内；白名单最高优先级。
- **风险四级**（PRD 4.1）：L0 安全 / L1 低风险默认勾选；L2 需确认（回收站、更新缓存、隐私项）默认不勾选；L3 永远不自动选中。
- **隔离区可恢复**：非缓存类文件先进隔离区（默认保留 30 天），支持恢复、永久删除、到期自动清理；恢复时自动处理重名冲突。
- **不跨 Junction 递归**：目录遍历与删除均不跟随 Reparse Point（selftest 有专门用例）。
- **可解释**：每条规则都展示"为什么可清理 / 清理后的影响 / 是否可恢复 / 占用进程"。
- **安全自测**：`CleanMaster.exe --selftest` 覆盖 PRD 第 53 章必测安全场景（25 项断言，任何一项失败返回非零退出码，阻止发布）。

## 🚀 构建与运行

环境要求：Windows 10 22H2+ / Windows 11，[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（零第三方 NuGet 依赖）。

```bash
# 构建（也可用根目录 CleanMaster.slnx，需 .NET 10 SDK）
dotnet build src/CleanMaster.App/CleanMaster.App.csproj -c Release

# 运行
src/CleanMaster.App/bin/Release/net8.0-windows/CleanMaster.exe

# 安全自测（CI 可用，退出码 0 = 全部通过）
CleanMaster.exe --selftest
```

## 📦 发布打包

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
```

产出 `dist/` 下两种形态：

- `CleanMaster-1.0.0-net8-x64/` —— 框架依赖版（需要 .NET 8 桌面运行时，体积小）
- `CleanMaster-1.0.0-win-x64-selfcontained/` + `zip` —— 自包含版（开箱即用）

脚本内含发布门禁：打包前自动运行 `--selftest`，任一安全断言失败即中止发布。

安装包：已提供 [scripts/setup.iss](scripts/setup.iss)（Inno Setup 6 脚本，支持桌面快捷方式/开机自启选项、卸载保留用户隔离区数据）。安装 [Inno Setup](https://jrsoftware.org/isinfo.php) 后执行：

```bash
ISCC.exe scripts\setup.iss   # 产物 dist\Output\CleanMasterSetup-1.0.0.exe
```

正式对外发布前：对 EXE/DLL 与安装包做代码签名（signtool）。

## 🔍 运行期可观测性

- 异常与关键失败写入 `%LOCALAPPDATA%\CleanMaster\app.log`
- 清理/卸载/启停历史：`history.json`；上次扫描摘要：`lastscan.json`；磁盘分析摘要：`disk_*.json`
- 全部数据仅存本地，卸载不清除（安全设计，隔离区可恢复）

## 🗂️ 工程结构

```
CleanMaster/
├── CleanMaster.sln
├── scripts/                 # 图标生成、发布打包脚本
└── src/
    ├── CleanMaster.Core/           # 核心引擎（纯逻辑，无 UI）
    │   ├── Models.cs               #   领域模型（RuleResult/CleanItem/CleanSummary...）
    │   ├── Security/               #   SafetyGuard 统一删除保护
    │   ├── Rules/                  #   规则引擎：IScanRule + 27 条内置规则
    │   │   ├── ScannerService      #   扫描编排
    │   │   ├── CleanService        #   清理事务（逐项状态汇报）
    │   │   ├── QuarantineService   #   隔离区
    │   │   └── ElevatedService     #   按需 UAC 提权 Helper
    │   ├── Analysis/               #   磁盘分析 / 大文件 / 重复文件
    │   ├── Startup/                #   启动项枚举与启停（含备份恢复）
    │   ├── Apps/                   #   应用清单 / 卸载 / 残留检测
    │   ├── Store/                  #   设置 / 历史（JSON 持久化于 %LOCALAPPDATA%\CleanMaster）
    │   └── SelfTest/               #   安全自测套件
    └── CleanMaster.App/            # WPF 外壳
        ├── Themes/                 #   浅色/深色画刷 + Fluent 风格控件模板（零第三方库）
        ├── Pages/                  #   首页/智能扫描/清理空间/空间分析/启动项/应用管理/工具箱/设置
        ├── Windows/                #   首启向导/主题化对话框/残留清理对话框
        └── Services/               #   主题/托盘/对话框/自动清理调度/缓存
```

## 🧭 与 PRD 的对应关系

- 技术选型：PRD 建议 WinUI 3；本实现采用 **WPF (.NET 8) + 自绘 Fluent 设计系统** —— 零 NuGet 依赖、单目录绿色发布、Win10/11 通吃，视觉按设计稿完整复刻（Mica 式浅色渐变、圆角卡片、NavigationView 式侧栏、自绘标题栏）。若后续需要 MSIX/Store 分发，可在 Core 不变的情况下替换 UI 层。
- PRD 第 47 章 MVP 清单中 P0 全部实现；P1 的应用缓存、隐私清理、重复文件、卸载残留、自动清理、托盘已实现；在线规则更新（rules.json + 签名校验）与 P2（启动耗时分析、企业版等）为后续迭代项。
- 明确不做（PRD 2.2）：内存加速球、服务阉割、注册表魔改、Defender 排除等伪优化均未实现。

## 📄 开源许可

本项目以 **GPL-3.0** 许可证开源（见 [LICENSE](LICENSE)）：

- 你可以自由使用、修改、再分发本软件，但衍生作品必须同样以 GPL-3.0 开源
- 商业授权/闭源定制请联系作者另行协商（GPL 双许可模式）
- 发布产物中的签名由 [SignPath Foundation](https://signpath.org) 面向开源项目免费提供

### 从源码构建发布

```bash
git clone <本仓库>
cd CleanMaster
dotnet build src/CleanMaster.App/CleanMaster.App.csproj -c Release
# 或直接打 tag 推送，GitHub Actions 会自动：构建 → 安全自测 → 发布 → 出 Release
```
