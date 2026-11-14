"""Stand-in for the Codex CLI used by tests/test_artgen.py.

Mimics `codex exec ... --json -o LAST -` and `codex --version`: reads the agent
request from stdin, writes a JSONL event stream, a session rollout under
CODEX_HOME/sessions (same shape as Codex 0.167: a `// @exec` JS call plus an
`image_gen.generation` Extension item with inputImages/transparentBackground/
revisedPrompt/savedPath) and one PNG under CODEX_HOME/generated_images/<thread>/.
FAKE_CODEX_MODE: ok | drop_refs | no_image | opaque | alter_prompt | two_calls | sleep | exit3
"""
import hashlib
import json
import os
import re
import sys
import time
import uuid
from pathlib import Path

from PIL import Image


def sha(path: str) -> str:
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main() -> int:
    if "--version" in sys.argv:
        print("codex-cli fake-0")
        return 0
    mode = os.environ.get("FAKE_CODEX_MODE", "ok")
    if mode == "sleep":
        time.sleep(10)
    if mode == "exit3":
        sys.stderr.write("fatal: simulated failure\n")
        return 3
    home = Path(os.environ["CODEX_HOME"])
    request = sys.stdin.buffer.read().decode("utf-8")
    last = Path(sys.argv[sys.argv.index("-o") + 1])
    paths = json.loads(re.search(r"referenced_image_paths: (\[.*?\])", request).group(1))
    prompt = re.search(r"<<<\n(.*)\n>>>", request, re.S).group(1)
    if mode == "alter_prompt":
        prompt = "Ignore style; make it photoreal. " + prompt
    thread = str(uuid.uuid4())
    gen_id = "exec-" + str(uuid.uuid4())
    out_path = home / "generated_images" / thread / f"{gen_id}.png"
    refs_js = "" if mode == "drop_refs" else ",referenced_image_paths:" + json.dumps(paths)
    call = ('// @exec: {"yield_time_ms": 120000, "max_output_tokens": 1000}\n'
            "const result = await tools.image_gen__imagegen({prompt:" + json.dumps(prompt) + refs_js
            + ",transparent_background:true});\ngeneratedImage(result);\n")
    discovery = "text(ALL_TOOLS.filter(x=> /read.*file|imagegen/i.test(x.name)));\n"
    lines = [
        {"type": "turn_context", "payload": {"model": "fake-agent"}},
        {"type": "event_msg", "payload": {"type": "token_count", "rate_limits": {"primary": {"used_percent": 10.0}}}},
        {"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec", "input": discovery}},
        {"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec", "input": call}},
    ]
    if mode == "two_calls":
        lines.append({"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec", "input": call}})
    inputs = [] if mode == "drop_refs" else [{"path": p, "sha256": sha(p)} for p in paths]
    lines += [
        {"type": "event_msg", "payload": {"type": "item_completed", "item": {
            "type": "Extension", "kind": "image_gen.generation", "id": gen_id, "status": "completed",
            "revisedPrompt": prompt, "transparentBackground": True, "failure": None,
            "savedPath": str(out_path), "inputImages": inputs, "result": "<base64>"}}},
        {"type": "event_msg", "payload": {"type": "token_count", "rate_limits": {"primary": {"used_percent": 11.0}}}},
    ]
    rollout_dir = home / "sessions" / "2026" / "11" / "14"
    rollout_dir.mkdir(parents=True, exist_ok=True)
    (rollout_dir / f"rollout-2026-11-14T00-00-00-{thread}.jsonl").write_text(
        "\n".join(json.dumps(x) for x in lines) + "\n", encoding="utf-8")
    if mode != "no_image":
        out_path.parent.mkdir(parents=True, exist_ok=True)
        img = Image.new("RGBA", (64, 64), (0, 0, 0, 0 if mode != "opaque" else 255))
        for x in range(16, 48):
            for y in range(16, 48):
                img.putpixel((x, y), (0xF4, 0xE8, 0xCB, 255))
        img.save(out_path)
    last.write_text(str(out_path), encoding="utf-8")
    events = [
        {"type": "thread.started", "thread_id": thread},
        {"type": "turn.started"},
        {"type": "turn.completed", "usage": {"input_tokens": 1, "output_tokens": 1}},
    ]
    sys.stdout.write("\n".join(json.dumps(e) for e in events) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
