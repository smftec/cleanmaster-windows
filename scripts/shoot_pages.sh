#!/bin/bash
# 逐页启动应用并截图验收（路径无关）
# 用法: bash scripts/shoot_pages.sh [输出目录]（默认 ./shots）
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
EXE_DIR="$ROOT/src/CleanMaster.App/bin/Debug/net8.0-windows"
OUT_DIR="${1:-$ROOT/shots}"
mkdir -p "$OUT_DIR"

cd "$EXE_DIR"
for page in Apps Toolbox Settings SpaceAnalysis; do
  taskkill //F //IM CleanMaster.exe > /dev/null 2>&1 || true
  sleep 1
  cmd //c "start CleanMaster.exe --page $page"
  sleep 8
  python "$SCRIPT_DIR/shot.py" "$OUT_DIR/$page.png"
done
taskkill //F //IM CleanMaster.exe > /dev/null 2>&1 || true
echo ALL-DONE
