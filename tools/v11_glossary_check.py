"""Check player text against the v1.1 glossary without modifying game sources."""

from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import re
from typing import Iterator


ROOT = Path(__file__).resolve().parents[1]
CONFIG = "DicePrototype/Assets/Game/Data/P0Config.json"
RULES = ROOT / "tools/v11_text/glossary_rules.json"
ALLOWLIST = ROOT / "tools/v11_text/glossary_allowlist.json"
CJK = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\U00020000-\U0003134f]")


@dataclass(frozen=True)
class PlayerText:
    file: str
    location: str
    text: str
    line: int | None = None


def load_rules(path: Path = RULES) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not data.get("rules") or not data.get("configFields"):
        raise ValueError("Rules and config field catalog must not be empty")
    ids = [rule["id"] for rule in data["rules"]]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate glossary rule id")
    for rule in data["rules"]:
        if ("term" in rule) == ("regex" in rule):
            raise ValueError(f"{rule['id']}: provide exactly one of term or regex")
        for required in ("category", "replacement", "source", "rationale"):
            if not rule.get(required):
                raise ValueError(f"{rule['id']}: missing {required}")
        re.compile(rule.get("regex", re.escape(rule.get("term", ""))))
    return data


def load_allowlist(path: Path = ALLOWLIST, rule_ids: set[str] | None = None) -> list[dict]:
    entries = json.loads(path.read_text(encoding="utf-8"))["entries"]
    for entry in entries:
        # Exact text and location keep exceptions from hiding unrelated future text.
        for key in ("rule", "file", "location", "text", "reason"):
            if not isinstance(entry.get(key), str) or not entry[key].strip():
                raise ValueError(f"Allowlist entry requires non-empty {key}")
        if rule_ids is not None and entry["rule"] not in rule_ids:
            raise ValueError(f"Unknown allowlist rule: {entry['rule']}")
    return entries


def config_texts(config: dict, field_catalog: dict, file: str = CONFIG) -> Iterator[PlayerText]:
    """Read only the explicit display contract, never IDs or arbitrary nested strings."""
    for category, fields in field_catalog.items():
        for index, entry in enumerate(config.get(category, [])):
            if not isinstance(entry, dict):
                continue
            identity = entry.get("id", entry.get("stageId", entry.get("count", index)))
            for field in fields:
                value = entry.get(field)
                if isinstance(value, str):
                    yield PlayerText(file, f"{category}[{identity}].{field}", value)


def _escaped(source: str, start: int) -> tuple[str, int]:
    escapes = {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a",
               "b": "\b", "f": "\f", "v": "\v", '"': '"', "'": "'", "\\": "\\"}
    if start + 1 >= len(source):
        return "\\", start + 1
    char = source[start + 1]
    if char in "uUx":
        count = {"u": 4, "U": 8, "x": 4}[char]
        match = re.match(r"[0-9a-fA-F]{1," + str(count) + "}", source[start + 2:])
        if match and (char == "x" or len(match[0]) == count):
            value = int(match[0], 16)
            if value <= 0x10ffff:
                return chr(value), start + 2 + len(match[0])
    return escapes.get(char, "\\" + char), start + 2


def _unicode_scalars(text: str) -> str:
    # C# accepts supplementary characters as either one \U escape or a UTF-16 pair.
    text = re.sub(r"[\ud800-\udbff][\udc00-\udfff]",
                  lambda match: chr(0x10000 + ((ord(match[0][0]) - 0xd800) << 10)
                                    + ord(match[0][1]) - 0xdc00), text)
    # Retain isolated UTF-16 code units as visible escapes so reports remain UTF-8.
    return re.sub(r"[\ud800-\udfff]", lambda match: f"\\u{ord(match[0]):04X}", text)


class CSharpStrings:
    """Small lexical scanner for comments, strings and balanced interpolation holes.

    It does not evaluate expressions or follow variable references. Nested string
    literals in interpolation expressions are returned separately; expression code
    is never treated as player text. This is intentionally not a C# semantic parser.
    """

    def __init__(self, source: str):
        self.source = source
        self.found: list[tuple[int, str]] = []

    def scan(self) -> list[tuple[int, str]]:
        self._code(0)
        return sorted(self.found)

    def _code(self, pos: int, closing_braces: int = 0) -> int:
        source = self.source
        depth = 0
        while pos < len(source):
            if closing_braces and depth == 0 and source.startswith("}" * closing_braces, pos):
                return pos + closing_braces
            if source.startswith("//", pos):
                end = source.find("\n", pos + 2)
                pos = len(source) if end < 0 else end + 1
                continue
            if source.startswith("/*", pos):
                end = source.find("*/", pos + 2)
                pos = len(source) if end < 0 else end + 2
                continue
            if source[pos] == "'":
                pos += 1
                while pos < len(source) and source[pos] != "'":
                    pos = _escaped(source, pos)[1] if source[pos] == "\\" else pos + 1
                pos += 1
                continue
            match = re.match(r'(\$*@?|@\$?)"', source[pos:])
            if match:
                pos = self._string(pos, match[1])
                continue
            if closing_braces:
                if source[pos] == "{":
                    depth += 1
                elif source[pos] == "}" and depth:
                    depth -= 1
            pos += 1
        return pos

    def _string(self, start: int, prefix: str) -> int:
        source = self.source
        quote_pos = start + len(prefix)
        quote_count = len(source[quote_pos:]) - len(source[quote_pos:].lstrip('"'))
        raw = quote_count >= 3
        quotes = quote_count if raw else 1
        verbatim = "@" in prefix
        interpolation = prefix.count("$")
        pos = quote_pos + quotes
        parts = []
        while pos < len(source):
            if raw and source.startswith('"' * quotes, pos):
                pos += quotes
                break
            if not raw and source[pos] == '"':
                if verbatim and source.startswith('""', pos):
                    parts.append('"')
                    pos += 2
                    continue
                pos += 1
                break
            if interpolation and source[pos] == "{":
                if not raw and source.startswith("{{", pos):
                    parts.append("{")
                    pos += 2
                    continue
                braces = interpolation if raw else 1
                if source.startswith("{" * braces, pos):
                    # Keep a separator so words across a dynamic value cannot merge.
                    parts.append("{…}")
                    pos = self._code(pos + braces, braces)
                    continue
            if interpolation and not raw and source.startswith("}}", pos):
                parts.append("}")
                pos += 2
            elif not raw and not verbatim and source[pos] == "\\":
                value, pos = _escaped(source, pos)
                parts.append(value)
            else:
                parts.append(source[pos])
                pos += 1
        self.found.append((start, _unicode_scalars("".join(parts))))
        return pos


def csharp_texts(source: str, file: str) -> Iterator[PlayerText]:
    for offset, text in CSharpStrings(source).scan():
        if CJK.search(text):
            line = source.count("\n", 0, offset) + 1
            column = offset - source.rfind("\n", 0, offset)
            yield PlayerText(file, f"{line}:{column}", text, line)


def source_files(root: Path) -> list[Path]:
    paths = [root / CONFIG]
    for folder in ("UI", "Runtime"):
        directory = root / "DicePrototype/Assets/Game" / folder
        candidates = [path for path in directory.rglob("*.cs")
                      if "Validation~" not in path.parts and path.name != "GameApp.Debug.cs"]
        if not candidates:
            raise ValueError(f"No eligible C# files in {directory}")
        paths.extend(candidates)
    return sorted(paths, key=lambda path: path.as_posix())


def find_violations(texts: list[PlayerText], rules: list[dict],
                    allowlist: list[dict]) -> tuple[list[dict], list[dict]]:
    violations, allowed = [], []
    compiled = [(rule, re.compile(rule.get("regex", re.escape(rule.get("term", ""))))) for rule in rules]
    for item in texts:
        for rule, pattern in compiled:
            for match in pattern.finditer(item.text):
                record = {"rule": rule["id"], "category": rule["category"], "file": item.file,
                          "location": item.location, "line": item.line, "text": item.text,
                          "match": match[0], "offset": match.start(), "replacement": rule["replacement"],
                          "source": rule["source"]}
                exception = next((entry for entry in allowlist if entry["rule"] == rule["id"]
                                  and all(entry[key] == record[key] for key in ("file", "location", "text"))), None)
                if exception:
                    allowed.append({**record, "reason": exception["reason"]})
                else:
                    violations.append(record)
    return violations, allowed


def scan(root: Path = ROOT, rules_path: Path = RULES, allowlist_path: Path = ALLOWLIST) -> dict:
    data = load_rules(rules_path)
    allowlist = load_allowlist(allowlist_path, {rule["id"] for rule in data["rules"]})
    texts, hashes = [], {}
    config = None
    for path in source_files(root):
        file = path.relative_to(root).as_posix()
        content = path.read_bytes()
        hashes[file] = hashlib.sha256(content).hexdigest()
        source = content.decode("utf-8-sig")
        if file == CONFIG:
            config = json.loads(source)
            texts.extend(config_texts(config, data["configFields"], file))
        else:
            texts.extend(csharp_texts(source, file))
    violations, allowed = find_violations(texts, data["rules"], allowlist)
    if not any(item.file == CONFIG for item in texts) or not any(item.file != CONFIG for item in texts):
        raise ValueError("Empty config or CJK source scan; cannot report success")
    matched_exceptions = {(entry["rule"], entry["file"], entry["location"], entry["text"]) for entry in allowed}
    stale = [entry for entry in allowlist if (entry["rule"], entry["file"], entry["location"], entry["text"]) not in matched_exceptions]
    return {"configVersion": config.get("configVersion"), "ruleCount": len(data["rules"]),
            "scannedFiles": len(hashes), "scannedStrings": len(texts),
            "configStrings": sum(item.file == CONFIG for item in texts),
            "csharpStrings": sum(item.file != CONFIG for item in texts),
            "violationCount": len(violations), "allowedCount": len(allowed),
            "countsByRule": {rule["id"]: sum(v["rule"] == rule["id"] for v in violations) for rule in data["rules"]},
            "countsByFile": {file: sum(v["file"] == file for v in violations) for file in hashes},
            "stringsByFile": {file: sum(item.file == file for item in texts) for file in hashes},
            "violations": violations, "allowed": allowed, "staleAllowlist": stale,
            "inputSha256": hashes,
            "rulesSha256": hashlib.sha256(rules_path.read_bytes()).hexdigest(),
            "allowlistSha256": hashlib.sha256(allowlist_path.read_bytes()).hexdigest()}


def readable_report(report: dict) -> str:
    lines = [f"术语检查：{report['scannedFiles']} 个文件，{report['scannedStrings']} 条字符串"
             f"（配置 {report['configStrings']}，C# {report['csharpStrings']}）。",
             f"违规 {report['violationCount']} 处；允许项 {report['allowedCount']} 处；"
             f"失效允许项 {len(report['staleAllowlist'])} 条。", "按规则："]
    lines.extend(f"  {key}: {value}" for key, value in report["countsByRule"].items())
    lines.append("按文件：")
    lines.extend(f"  {key}: {value}" for key, value in report["countsByFile"].items())
    lines.append("逐项：")
    for item in report["violations"]:
        excerpt = item["text"].replace("\n", "\\n")
        lines.append(f"  {item['file']}:{item['location']} [{item['rule']}] {item['match']}"
                     f" → {item['replacement']} | {excerpt}")
    return "\n".join(lines) + "\n"


def markdown_baseline(report: dict) -> str:
    def escape(value: str) -> str:
        return value.replace("|", "\\|").replace("\n", "<br>").replace("\r", "")

    lines = ["# W10 用语检查基线", "", "状态：REVIEW。此报告记录尚未改写的界面文字，不代表 TX-04 通过。", "",
             "复现：`E:\\anaconda3\\python.exe tools/v11_glossary_check.py --report-only --baseline docs/specs/v11/glossary-baseline.md`。",
             "机器可读输出：`E:\\anaconda3\\python.exe tools/v11_glossary_check.py --report-only --json`。", "",
             f"配置版本 `{report['configVersion']}`；{report['ruleCount']} 条规则；扫描 {report['scannedFiles']} 个文件、"
             f"{report['scannedStrings']} 条字符串（配置 {report['configStrings']}，C# {report['csharpStrings']}）。"
             f"共 {report['violationCount']} 处命中，允许 {report['allowedCount']} 处，失效允许项 {len(report['staleAllowlist'])} 条。", "",
             "每次正则匹配计 1 处；同一字符串可命中多条规则。同一概念的重叠别名合成一条规则；不同规则可能分别标出术语和开发信息。", "",
             "## 扫描范围与限制", "",
             "- 配置字段清单固定在 `tools/v11_text/glossary_rules.json`：六类名称、短文、细则；服务价格规则；红组称号；附魔、牌型、颜色名称；关卡目标分字符串。开发用 description、ID、枚举、配置版本与调试配置排除。",
             "- C# 检查 UI/ 与 Runtime/ 中含 CJK 的字符串字面量，排除所有 Validation~ 目录及 GameApp.Debug.cs。包括旧界面与验证辅助代码；没有推断调用可达性或是否最终呈现。",
             "- 识别普通、逐字、插值、原始字符串及 Unicode 转义；跳过注释、字符字面量与插值代码。插值内部的字符串另作字面量处理；动态值以 {…} 表示，不将代码标识符误算为玩家文字。",
             "- 不执行 C#、不跟踪变量与拼接：动态生成的编号/版本、纯英文枚举字面量、运行时异常信息仍须 TX-02 回放检查。词义依靠已列上下文，不能替代人工语义审查。",
             "- 普通关、普通栏位、普通挑战、稀有度代码 common/uncommon/rare、其他/其余、生命周期、原值型技术字段均不因无关子串误报。明确引用“其”的代词、牌型升级与骰面改造等有歧义词采用上下文规则；具体依据见每条 rationale。",
             "- allowlist 要求规则、文件、位置、完整原文和理由完全匹配；未使用的条目单独列为失效，不自动扩大豁免。", "",
             "## 按规则计数", "", "| 规则 | 命中数 |", "| --- | ---: |"]
    lines.extend(f"| {key} | {value} |" for key, value in report["countsByRule"].items())
    lines += ["", "## 按文件计数", "", "| 文件 | 扫描字符串 | 命中数 |", "| --- | ---: | ---: |"]
    lines.extend(f"| `{key}` | {report['stringsByFile'][key]} | {value} |" for key, value in report["countsByFile"].items())
    lines += ["", "## 主要例子（每条规则最多 3 条）", "", "| 规则 | 文件与位置 | 命中 | 建议 | 原文 |", "| --- | --- | --- | --- | --- |"]
    seen = Counter()
    for item in report["violations"]:
        if seen[item["rule"]] < 3:
            lines.append(f"| {item['rule']} | `{item['file']}:{item['location']}` | {escape(item['match'])} | "
                         f"{escape(item['replacement'])} | {escape(item['text'])} |")
        seen[item["rule"]] += 1
    lines += ["", "## 允许项与失效项", ""]
    if not report["allowed"] and not report["staleAllowlist"]:
        lines.append("当前没有允许项，也没有失效项；现存违规全部保留在基线中。")
    for item in report["allowed"]:
        lines.append(f"- `{item['rule']}` `{item['file']}:{item['location']}`：{escape(item['reason'])}")
    for item in report["staleAllowlist"]:
        lines.append(f"- 失效：`{item['rule']}` `{item['file']}:{item['location']}`：{escape(item['reason'])}")
    lines += ["", "## 输入指纹", "", f"- 规则 SHA-256：`{report['rulesSha256']}`",
              f"- 允许项 SHA-256：`{report['allowlistSha256']}`", "", "| 文件 | SHA-256 |", "| --- | --- |"]
    lines.extend(f"| `{key}` | `{value}` |" for key, value in report["inputSha256"].items())
    return "\n".join(lines) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Emit a machine-readable report")
    parser.add_argument("--report-only", action="store_true", help="Report violations without exit code 1")
    parser.add_argument("--baseline", type=Path, help="Write a Chinese Markdown baseline")
    args = parser.parse_args(argv)
    try:
        report = scan()
    except (OSError, ValueError) as error:
        print(json.dumps({"error": str(error)}, ensure_ascii=False) if args.json else f"检查失败：{error}")
        return 2
    if args.baseline:
        args.baseline.write_text(markdown_baseline(report), encoding="utf-8", newline="\n")
    print(json.dumps(report, ensure_ascii=False, indent=2) if args.json else readable_report(report), end="\n" if args.json else "")
    return 1 if report["violationCount"] and not args.report_only else 0


if __name__ == "__main__":
    raise SystemExit(main())
