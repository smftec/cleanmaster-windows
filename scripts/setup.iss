; 清理优化大师 Windows 安装包脚本（Inno Setup 6）
; 使用方法：
;   1. 先运行 scripts\publish.ps1 生成 dist\CleanMaster-1.0.0-win-x64-selfcontained
;   2. 用 Inno Setup Compiler (ISCC.exe) 编译本脚本：
;        ISCC.exe scripts\setup.iss
;   3. 产物：dist\Output\CleanMasterSetup-1.0.0.exe
; 正式发布前请用 signtool 对安装包与所有 EXE/DLL 签名（PRD 第 44/54 章）。

#define MyAppName "清理优化大师"
#define MyAppNameEn "CleanMaster"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CleanMaster"
#define MyAppExeName "CleanMaster.exe"

[Setup]
AppId={{8A6C1E42-5B7D-4E93-9A2C-CLEANMASTER01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist\Output
OutputBaseFilename=CleanMasterSetup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 面向普通用户，默认单用户可装；勾选 PrivilegesRequiredOverridesAllowed 允许安装时选"为所有用户"
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\CleanMaster.App\Assets\app.ico
WizardUninstallWarning=no

; ===== 代码签名 =====
; 编译前在 Inno Setup 中配置签名工具（菜单 Tools → Configure Sign Tools...）：
;   名称: cm_signtool
;   命令: "<signtool.exe 完整路径>" sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f
; 然后取消下面两行注释，即可对安装包（及卸载器）签名：
; SignTool=cm_signtool
; SignedUninstaller=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
; 自包含发布目录的全部文件
Source: "..\dist\CleanMaster-{#MyAppVersion}-win-x64-selfcontained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："
Name: "autostart"; Description: "开机自动运行（后台托盘）"; GroupDescription: "附加任务："; Flags: unchecked

[Registry]
; 开机自启（HKCU，无需管理员）
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CleanMaster"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载时不删除用户数据（隔离区/历史保存在 %LOCALAPPDATA%\CleanMaster），
; 如需彻底清理可手动删除该目录 —— 这是刻意的安全设计。
