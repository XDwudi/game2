# 各风格标题字体候选（W21，待用户批准下载）

状态：提案（2026-11-17）。依据：DEV_PLAN §8 第 2 条（只用开源字体，下载前列出文件名、来源、大小和许可证）。本页只是调研结果：没有下载任何字体文件。

- 正文继续用游戏已带的 Noto Sans CJK SC Regular 与 Noto Sans SC Bold（OFL 1.1）。新字体只用于标题：关卡名、页面标题、大卡的卡名、主菜单标题字。
- 要求：简体至少覆盖 GB2312，含数字与 × + % → · ：（）；许可证 OFL 1.1 或同样允许商业游戏内嵌的许可。
- 大小取自 GitHub API 的文件列表与发行附件；覆盖情况查了上游源文件的字形名、fonts.google.com 的覆盖数据与 Adobe 的 CN 映射表。“未核实”表示没能确认。

## 推荐（每套一个，合计 25,549,312 字节，约 25.5 MB）

| 风格 | 字体 | 文件 | 大小（字节） | 来源 | 许可证 | 覆盖 |
| --- | --- | --- | --- | --- | --- | --- |
| 瑞士现代主义 | 沿用 Noto Sans SC Bold | 已在工程内 | 0 | — | OFL 1.1 | 完整 |
| 唐三彩陶坊 | 马善政楷书 Ma Shan Zheng v2.002 | `MaShanZheng-Regular.ttf` | 5,857,936 | https://raw.githubusercontent.com/google/fonts/main/ofl/mashanzheng/MaShanZheng-Regular.ttf | [OFL 1.1](https://github.com/google/fonts/blob/main/ofl/mashanzheng/OFL.txt) | GB2312 6,763/6,763；没有 → 与 · |
| 爆彩连环漫画 | 站酷快乐体 ZCOOL KuaiLe | `ZCOOLKuaiLe-Regular.ttf` | 1,514,968 | https://raw.githubusercontent.com/google/fonts/main/ofl/zcoolkuaile/ZCOOLKuaiLe-Regular.ttf | [OFL 1.1](https://github.com/google/fonts/blob/main/ofl/zcoolkuaile/OFL.txt) | GB2312 6,763/6,763；没有 → |
| 黑金装饰艺术 | 站酷小薇 ZCOOL XiaoWei | `ZCOOLXiaoWei-Regular.ttf` | 6,313,808 | https://raw.githubusercontent.com/google/fonts/main/ofl/zcoolxiaowei/ZCOOLXiaoWei-Regular.ttf | [OFL 1.1](https://github.com/google/fonts/blob/main/ofl/zcoolxiaowei/OFL.txt) | 简体子集 5,717/5,717；完整 GB2312 未核实；没有 ·；→ 未核实 |
| 彩绘玻璃 | 思源宋体 Noto Serif SC Black（静态子集 OTF） | `NotoSerifSC-Black.otf` | 11,862,600 | https://raw.githubusercontent.com/notofonts/noto-cjk/main/Serif/SubsetOTF/SC/NotoSerifSC-Black.otf | [OFL 1.1](https://github.com/notofonts/noto-cjk/blob/main/Serif/LICENSE) | GB2312 完整，所需符号全有 |

## 备选

| 风格 | 字体 | 文件与大小（字节） | 备注 |
| --- | --- | --- | --- |
| 唐三彩陶坊 | 霞鹜文楷 GB Lite Medium v1.530 | `LXGWWenKaiGBLite-Medium.ttf`，14,164,100（[发行页](https://github.com/lxgw/LxgwWenkaiGB-Lite/releases/tag/v1.530)，OFL 1.1） | 最好读，但是钢笔楷不是毛笔；符号未核实 |
| 爆彩连环漫画 | 站酷庆科黄油体 | `ZCOOLQingKeHuangYou-Regular.ttf`，8,328,684（OFL 1.1） | 最粗，招牌感强；完整覆盖未核实 |
| 爆彩连环漫画 | 得意黑 Smiley Sans v2.0.1 | `smiley-sans-v2.0.1.zip`，5,781,344（OFL 1.1，保留名 Smiley / 得意黑） | 只有斜体；以 zip 发布 |
| 黑金装饰艺术 | 思源宋体 Noto Serif SC SemiBold | `NotoSerifSC-SemiBold.otf`，11,771,476（OFL 1.1） | 覆盖无风险；与彩绘玻璃同一家族 |

## 使用时的做法

- 每个标题字体的 TMP 字体资源都把 Noto Sans SC Bold 设为回落字体，再用 TMP 的缺字报告检查实际的标题字符串（推荐里有三款缺 → 或 ·）。
- 随游戏附上每款字体的 OFL.txt；在 `Assets/Game/Fonts/SOURCE.json` 记录来源、提交号与 SHA-256。上面的地址指向 `main` 分支，下载时改用固定的提交号，以保证与记录的字节一致。
- 站酷小薇笔画细，SDF 采样尺寸要大；实际效果要在实机标题里看过再定。
