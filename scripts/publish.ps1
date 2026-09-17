# 清理优化大师 发布打包脚本
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\CleanMaster.App\CleanMaster.App.csproj"
$outDir = Join-Path $root "dist"

Write-Host "==> 还原并发布 ($Configuration)"
dotnet publish $project -c $Configuration -r win-x64 --self-contained false `
    -p:Version=$Version -o (Join-Path $outDir "CleanMaster-$Version-net8-x64") `
    -v minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "发布失败(框架依赖版)" }

Write-Host "==> 发布自包含版(无需安装 .NET)"
dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:Version=$Version -o (Join-Path $outDir "CleanMaster-$Version-win-x64-selfcontained") `
    -v minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "发布失败(自包含版)" }

Write-Host "==> 运行安全自测 (--selftest)"
$exe = Join-Path $outDir "CleanMaster-$Version-net8-x64\CleanMaster.exe"
& $exe --selftest
if ($LASTEXITCODE -ne 0) { throw "安全自测未通过，禁止发布！(PRD 第 53 章)" }
Write-Host "安全自测全部通过 ✓"

# —— 可选：代码签名（拿到证书后启用）——
# 硬件令牌/云令牌:  $env:CM_SIGN="1"; $env:CM_SIGN_SUBJECT="CN=你的公司或名字"
# PFX(旧证书/测试): $env:CM_SIGN="1"; $env:CM_SIGN_PFX="my.pfx"; $env:CM_SIGN_PWD="xxx"
if ($env:CM_SIGN -eq "1") {
    Write-Host "==> 代码签名"
    foreach ($d in @("CleanMaster-$Version-net8-x64", "CleanMaster-$Version-win-x64-selfcontained")) {
        $p = @{ Folder = Join-Path $outDir $d }
        if ($env:CM_SIGN_SUBJECT) { $p.Subject = $env:CM_SIGN_SUBJECT }
        if ($env:CM_SIGN_PFX)     { $p.PfxPath = $env:CM_SIGN_PFX; $p.PfxPassword = $env:CM_SIGN_PWD }
        & (Join-Path $PSScriptRoot "sign.ps1") @p
        if ($LASTEXITCODE -ne 0) { throw "签名失败，禁止发布" }
    }
}

Write-Host "==> 打包 zip"
$zip = Join-Path $outDir "CleanMaster-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $outDir "CleanMaster-$Version-win-x64-selfcontained\*") `
    -DestinationPath $zip -CompressionLevel Optimal

Write-Host ""
Write-Host "发布完成:"
Get-ChildItem $outDir | Select-Object Name | Format-Table
Write-Host "注意: 正式对外发布前请对 CleanMaster.exe 及所有 DLL 进行代码签名(sigmoid/signtool)，"
Write-Host "      并按 PRD 第 54 章准备官网安装包(Inno Setup/MSIX)与 Microsoft Store 审核。"
