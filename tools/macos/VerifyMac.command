#!/bin/bash
set -eu
SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" && pwd)"
if ! command -v python3 >/dev/null 2>&1; then
  printf '%s\n' '需要 Python 3.9 或更新版本。脚本不会自动安装；安装后重新运行同一命令。' >&2
  exit 2
fi
if [ "$#" -eq 0 ]; then
  if [ ! -f "$SCRIPT_DIR/inputs/macos.zip" ]; then
    printf '%s\n' '未找到同目录 inputs/macos.zip。请使用完整验证工具包，或执行 bash VerifyMac.command --help 查看参数。' >&2
    exit 2
  fi
  set -- --package "$SCRIPT_DIR/inputs/macos.zip" \
    --trace "$SCRIPT_DIR/inputs/normal-run-trace.json" \
    --windows-results "$SCRIPT_DIR/inputs/windows-results" \
    --identity "$SCRIPT_DIR/inputs/BUILD_IDENTITY.json"
fi
exec python3 "$SCRIPT_DIR/verify_mac.py" "$@"
