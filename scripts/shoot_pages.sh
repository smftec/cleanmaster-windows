#!/bin/bash
# 逐页启动应用并截图验收
cd "T:\2、win\1-清理优化大师\CleanMaster\src\CleanMaster.App\bin\Debug\net8.0-windows"
for page in Apps Toolbox Settings SpaceAnalysis; do
  taskkill //F //IM CleanMaster.exe > /dev/null 2>&1
  sleep 1
  cmd //c "start CleanMaster.exe --page $page"
  sleep 8
  python "T:/2、win/1-清理优化大师/CleanMaster/scripts/shot.py" "T:/2、win/1-清理优化大师/shots/$page.png"
done
taskkill //F //IM CleanMaster.exe > /dev/null 2>&1
echo ALL-DONE
