# 代码签名脚本：对一个目录下所有 EXE/DLL 签名（含 RFC3161 时间戳）
# 用法示例：
#   1) 证书在硬件令牌/本地证书存储（按主题匹配）：
#      powershell -File sign.ps1 -Folder "dist\CleanMaster-1.0.0-net8-x64" -Subject "CN=Your Company"
#   2) PFX 文件（仅旧证书或自签测试证书可用，2023 年起新发证书密钥不可导出）：
#      powershell -File sign.ps1 -Folder "dist\..." -PfxPath "my.pfx" -PfxPassword "xxx"
param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [string]$Subject = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Folder)) { throw "目录不存在: $Folder" }

# —— 定位 signtool.exe（Windows SDK 自带）——
$signtool = $null
$candidates = @(
    (Get-Command signtool.exe -ErrorAction SilentlyContinue)?.Source,
    (Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64" } |
        Sort-Object FullName -Descending | Select-Object -First 1).FullName
)
foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { $signtool = $c; break } }
if (-not $signtool) { throw "未找到 signtool.exe，请安装 Windows SDK（勾选 Signing Tools for Desktop Apps）" }
Write-Host "signtool: $signtool"

# —— 时间戳服务器（RFC3161，必须加，否则证书过期后签名失效）——
$ts = "http://timestamp.digicert.com"

$files = Get-ChildItem $Folder -Recurse -Include *.exe, *.dll
if ($files.Count -eq 0) { throw "目录中没有 EXE/DLL" }

$failed = @()
foreach ($f in $files) {
    Write-Host "签名 $($f.Name) ..."
    $args = @("sign", "/fd", "SHA256", "/tr", $ts, "/td", "SHA256")
    if ($PfxPath -ne "") { $args += @("/f", $PfxPath, "/p", $PfxPassword) }
    elseif ($Subject -ne "") { $args += @("/n", $Subject) }
    else { $args += "/a" }
    $args += $f.FullName
    & $signtool @args | Out-Null
    if ($LASTEXITCODE -ne 0) { $failed += $f.FullName }
}

if ($failed.Count -gt 0) {
    Write-Host "以下文件签名失败:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" }
    throw "存在签名失败文件，禁止发布"
}

Write-Host "验证签名..."
& $signtool verify /pa /q (Join-Path $Folder "CleanMaster.exe")
if ($LASTEXITCODE -ne 0) { throw "签名验证失败" }
Write-Host "全部签名完成并通过验证 ✓ ($($files.Count) 个文件)"
