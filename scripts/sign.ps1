# 代码签名脚本：对一个文件或一个目录下所有 EXE/DLL 签名（含 RFC3161 时间戳）
# 用法示例：
#   1) 证书在硬件令牌/本地证书存储（按主题匹配）：
#      powershell -File sign.ps1 -Folder "dist\CleanMaster-1.0.0-net8-x64" -Subject "CN=Your Company"
#      powershell -File sign.ps1 -File   "dist\CleanMasterSetup-1.0.0.exe"  -Subject "CN=Your Company"
#   2) PFX 文件（仅旧证书或自签测试证书可用，2023 年起新发证书密钥不可导出）：
#      powershell -File sign.ps1 -Folder "dist\..." -PfxPath "my.pfx" -PfxPassword "xxx"
param(
    [string]$Folder = "",
    [string]$File = "",
    [string]$Subject = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
if ($Folder -eq "" -and $File -eq "") { throw "请指定 -Folder 或 -File" }
if ($Folder -ne "" -and -not (Test-Path $Folder)) { throw "目录不存在: $Folder" }
if ($File -ne "" -and -not (Test-Path $File)) { throw "文件不存在: $File" }

# —— 定位 signtool.exe（Windows SDK 自带）——
$signtool = $null
$cmdSigntool = Get-Command signtool.exe -ErrorAction SilentlyContinue
$signtoolCandidates = @()
if ($cmdSigntool) { $signtoolCandidates += $cmdSigntool.Source }
$signtoolCandidates += (Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "x64" } |
    Sort-Object FullName -Descending | Select-Object -First 1).FullName
foreach ($c in $signtoolCandidates) { if ($c -and (Test-Path $c)) { $signtool = $c; break } }
if (-not $signtool) { throw "未找到 signtool.exe，请安装 Windows SDK（勾选 Signing Tools for Desktop Apps）" }
Write-Host "signtool: $signtool"

# —— 时间戳服务器（RFC3161，必须加，否则证书过期后签名失效）——
$ts = "http://timestamp.digicert.com"

if ($File -ne "") {
    $targets = @(Get-Item $File)
    $verifyTarget = $File
}
else {
    $targets = Get-ChildItem $Folder -Recurse -Include *.exe, *.dll
    if ($targets.Count -eq 0) { throw "目录中没有 EXE/DLL" }
    $verifyTarget = Join-Path $Folder "CleanMaster.exe"
}

$failed = @()
foreach ($f in $targets) {
    Write-Host "签名 $($f.Name) ..."
    $sargs = @("sign", "/fd", "SHA256", "/tr", $ts, "/td", "SHA256")
    if ($PfxPath -ne "") { $sargs += @("/f", $PfxPath, "/p", $PfxPassword) }
    elseif ($Subject -ne "") { $sargs += @("/n", $Subject) }
    else { $sargs += "/a" }
    $sargs += $f.FullName
    & $signtool @sargs | Out-Null
    if ($LASTEXITCODE -ne 0) { $failed += $f.FullName }
}

if ($failed.Count -gt 0) {
    Write-Host "以下文件签名失败:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" }
    throw "存在签名失败文件，禁止发布"
}

if (Test-Path $verifyTarget) {
    Write-Host "验证签名..."
    & $signtool verify /pa /q $verifyTarget
    if ($LASTEXITCODE -ne 0) { throw "签名验证失败" }
}
Write-Host "全部签名完成并通过验证 ✓ ($($targets.Count) 个文件)"
