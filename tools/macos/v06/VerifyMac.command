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
    --debug-trace "$SCRIPT_DIR/inputs/debug-24-trace.json" \
    --windows-results "$SCRIPT_DIR/inputs/windows-results" \
    --identity "$SCRIPT_DIR/inputs/BUILD_IDENTITY.json"
  if [ -f "$SCRIPT_DIR/inputs/review-candidate.json" ]; then
    printf '%s\n' '限制版审阅候选：Windows 原生显示仍待解决；此 Windows 结果仅作玩法/UI/轨迹对照。Mac 自身仍执行完整严格验收。'
    set -- "$@" --review-candidate
    if python3 -c 'import json,sys; sys.exit(0 if json.load(open(sys.argv[1], encoding="utf-8")).get("allowFailedWindowsPerformance") is True else 1)' "$SCRIPT_DIR/inputs/review-candidate.json"; then
      printf '%s\n' '诊断候选：Windows 性能 FAILED（P95 超标、焦点/GPU 采样异常），原生 P22 未通过；Mac 未测，整版未 ACCEPTED。Mac 自身仍执行原性能与显示门槛。'
      set -- "$@" --allow-failed-windows-performance
    fi
  fi
fi
exec python3 "$SCRIPT_DIR/verify_mac.py" "$@"
