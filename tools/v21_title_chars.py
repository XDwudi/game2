"""Reproduce the W21 font inputs from the current game, its font sources and OFLs."""

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / "DicePrototype/Assets/Game"
FONTS = GAME / "Fonts"
sys.path.insert(0, str(ROOT / "tools"))
from v11_glossary_check import CSharpStrings, config_texts, load_rules


# Titles assembled from several helper calls, including the approved W15/W16 labels.
# Names of individual cards, bosses, rewards, upgrades and hands come from P0Config.
TITLE_FRAGMENTS = """
商店 第 关前 关完成 本次挑战结束 退出游戏 已提交变化 只读 已提交表现记录 控制器排列与状态
卡牌排列与状态 暂停与设置 分数来源 只读快照 指定升级 本店还剩次 显示设置 版本与构建 新建挑战
开始新的一局 遥控骰子 选择两颗未锁骰 牌型手册 设置 画面风格 首领战利品 选择战利品 击败 整局结果
骰子构筑 快捷键 点击后按一个新按键 同点红组 独立相乘 普通关 高压关 高压挑战 首领关
升级与服务 骰子与骰面 我的骰子 我的构筑 在售 第页 一档 二档 三档 终局 减益已解除 还可以领件
已领完 章 金币 目标 玻璃 金面 全蓝 蓝五面 蓝面多 红蓝各半 红面多 红五面 全红 大点 小点
三个 六个 同款 点数和 第张 重掷 还能掷 回车 小键盘 空格 菜单键 锁定颗 最多锁 提交 下一关
开始挑战 开始第关 进入商店 查看整局结果 再开一局 继续对局 返回对局 重试保存 应用 保留
放弃当前进度并开始 结束本关 结算 达标后可结束
"""
FALLBACK_SYMBOLS = "²·‹›←↑→↓≈≤≥◆◇●✓×−…"
ROLE_CALL = re.compile(
    r'(?:PrimaryButton11|Section14|TextRole\.|"DialogTitle"|"ShopTitle"|'
    r'"StageLabel11"|"StageGoal11"|"StageCardTitle11"|"BossModifierName"|"EndTitle"|"Cleared")'
)


def character_sets(game=GAME):
    config = json.loads((game / "Data/P0Config.json").read_text(encoding="utf-8"))
    ascii_chars = {chr(code) for code in range(32, 127)}
    title = ascii_chars | set(TITLE_FRAGMENTS + FALLBACK_SYMBOLS)
    bold = set()
    for item in config_texts(config, load_rules()["configFields"]):
        bold.update(item.text)
        if item.location.endswith((".displayName", ".title")):
            title.update(item.text)
    # The regular atlas includes glyphs used by earlier saves, key bindings and views.
    for code in re.findall(r"^\s*m_Unicode: (\d+)\s*$",
                           (game / "Fonts/DiceChinese.asset").read_text(encoding="utf-8"), re.M):
        bold.add(chr(int(code)))
    for folder in ("UI", "Runtime", "Core"):
        for source in sorted((game / folder).rglob("*.cs")):
            if {"Verification", "Validation~"} & set(source.parts):
                continue
            body = source.read_text(encoding="utf-8-sig")
            for pos, literal in CSharpStrings(body).scan():
                bold.update(literal)
                start = body.rfind("\n", 0, pos) + 1
                end = body.find("\n", pos)
                if folder == "UI" and ROLE_CALL.search(body, start, end if end >= 0 else len(body)):
                    title.update(literal)
    def printable(chars):
        return "".join(sorted(c for c in chars if ord(c) >= 32 and (c == " " or not c.isspace())))
    return printable(title), printable(bold | title)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def font_entry(font, license_file, source, package_name, usage):
    with TTFont(font, lazy=True) as face:
        names = face["name"]
        return {
            "name": names.getDebugName(4), "version": names.getDebugName(5),
            "copyright": names.getDebugName(0), "license": "SIL Open Font License 1.1",
            "source": source, "usage": usage, "fontFile": font.relative_to(ROOT).as_posix(),
            "fontSha256": sha(font), "licenseFile": license_file.relative_to(ROOT).as_posix(),
            "licenseSha256": sha(license_file), "packageFile": package_name,
        }


def recorded_source(directory):
    record = json.loads((directory / "SOURCE.json").read_text(encoding="utf-8-sig"))
    files = [directory / item["name"] for item in record["local_files"]]
    for item, path in zip(record["local_files"], files):
        if sha(path) != item["sha256"]:
            raise ValueError(f"Source differs from SOURCE.json: {path}")
    return files[0], files[1], record["source"]


def manifest():
    regular = font_entry(*recorded_source(FONTS), "NotoSansCJKsc-OFL.txt", "正文与动态回落")
    bold = font_entry(FONTS / "NotoSansSC-Bold.otf", FONTS / "LICENSE.txt",
                      "https://github.com/notofonts/noto-cjk", "NotoSansSC-Bold-OFL.txt",
                      "基础粗体与标题缺字回落")
    bold_record = json.loads((ROOT / "ArtSource/Art071/Fonts/SOURCE.json").read_text(encoding="utf-8"))
    if bold["fontSha256"] != bold_record["originalSha256"]:
        raise ValueError("Bold source differs from Art071 provenance")
    entries = [regular, bold]
    for theme, name, usage in (
        ("sancai", "MaShanZheng", "唐三彩标题"), ("comic", "ZCOOLKuaiLe", "爆彩漫画标题"),
        ("deco", "ZCOOLXiaoWei", "黑金标题"), ("glass", "NotoSerifSC", "彩绘玻璃标题"),
    ):
        entries.append(font_entry(*recorded_source(FONTS / "Themes" / theme),
                                  name + "-OFL.txt", usage))
    # W26 selects this notice if the actual player build still contains the TMP sample.
    tmp_fonts = GAME.parent / "TextMesh Pro/Fonts"
    optional = font_entry(tmp_fonts / "LiberationSans.ttf", tmp_fonts / "LiberationSans - OFL.txt",
                          "https://github.com/liberationfonts/liberation-fonts",
                          "LiberationSans-OFL.txt", "TextMeshPro 示例字体")
    return {"schema": 1, "fonts": entries, "ifIncludedInBuild": [optional]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="write outputs (default: check)")
    args = parser.parse_args()
    title, bold = character_sets()
    outputs = {
        FONTS / "Themes/title_chars.txt": title + "\n",
        FONTS / "Themes/bold_chars.txt": bold + "\n",
        FONTS / "fonts_manifest.json": json.dumps(manifest(), indent=2, ensure_ascii=False) + "\n",
    }
    for path, expected in outputs.items():
        if args.write:
            path.write_bytes(expected.encode("utf-8"))
        elif not path.exists() or path.read_text(encoding="utf-8") != expected:
            parser.exit(1, f"Stale W21 font input: {path}\n")
    print(f"W21 font inputs: {len(title)} title and {len(bold)} bold characters")


if __name__ == "__main__":
    main()
