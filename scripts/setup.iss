; 清理优化大师 Windows 安装包脚本（Inno Setup 6）
; 本地构建（先运行 scripts\publish.ps1）：
;     ISCC.exe scripts\setup.iss
; CI/自定义版本构建：
;     ISCC.exe /DMyAppVersion=1.2.3 /DSrcDir="C:\path\to\publish" scripts\setup.iss
; 正式发布前请对 CleanMaster.exe、所有 DLL 与安装包本体做代码签名。

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef SrcDir
#define SrcDir "..\dist\CleanMaster-" + MyAppVersion + "-win-x64-selfcontained"
#endif

#ifndef OutBase
#define OutBase "CleanMasterSetup-" + MyAppVersion
#endif

#define MyAppName "清理优化大师"
#define MyAppNameEn "CleanMaster"
#define MyAppPublisher "CleanMaster"
#define MyAppExeName "CleanMaster.exe"

[Setup]
AppId={{47D73EFF-277F-4B0E-B37E-4902F848128A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename={#OutBase}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 默认当前用户安装（免管理员）；需要装给所有用户时可用 /ALLUSERS 命令行参数
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\CleanMaster.App\Assets\app.ico
; 安装时展示 GPL-3.0 许可证
LicenseFile=..\LICENSE

; ===== 代码签名 =====
; 编译前在 Inno Setup 中配置签名工具（菜单 Tools → Configure Sign Tools...）：
;   名称: cm_signtool
;   命令: "<signtool.exe 完整路径>" sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f
; 然后取消下面两行注释，即可对安装包（及卸载器）签名：
; SignTool=cm_signtool
; SignedUninstaller=yes

[Languages]
; 简体中文语言文件随仓库提供（scripts\ChineseSimplified.isl，Inno 6.5+）
Name: "chinese"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SrcDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
; 卸载刻意保留用户数据（隔离区/历史在 %LOCALAPPDATA%\CleanMaster），
; 隔离区文件卸载后仍可重装恢复 —— 这是安全设计。
