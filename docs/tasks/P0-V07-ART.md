# P0-004 / v0.7 美术与表现实施契约

状态 IMPLEMENTING。2026-10-31，root 统筹；用户明确要求按三份附件开发，并授权多代理协作及必要 Unity/Blender 工具安装。

输入为原样 `docs/specs/v07/ART_SPEC.md`、`ART_DESIGN.docx`、`dice_art_v0_7_manifest.json`。版本 art0.7.0；规则 p0.6.0、schema4 保持。前版冻结源08ef933，交付证据归档HEAD 1fbc3cc；前版显示和性能失败、Mac未验保持历史事实。

交付：统一青绿/奶油/红蓝主题、22屏及关键状态、71独立内容图标与5附魔公共角标、可组合3D陶瓷/玻璃骰、独立收据表现播放器、六档Combo、25声音变体与音乐、辅助设置、资源源文件与复现脚本、Windows/macOS候选与实测记录。范围不含规则平衡调整、移动端或换UI框架。

验收标准原样引用规格第34章 ART01–22 及附录D UI01–32、FX01–13；共67项。每项场景、阈值按原稿，映射与证据由独立审查记录于 evidence/v07/review。方法包含静态资源/配置检查、160规则黄金及完整回归、真实Unity/Player操作、同收据三速度与减少动态对照、固定60fps录像、最大容量/字体/分辨率、200次回放池稳定及180秒性能。Windows本机Ryzen5900X/RTX3080；Mac M5用户实机执行，不由Windows推定通过。艺术方向与体验验收责任人为用户，技术审查为v07_spec_audit。

制作顺序：先主题和公共组件，四卡C02/C41/C45/C52及母骰样板；普通手/G147完整垂直切片实际集成与审查后扩内容；最终全部页面与压力场景。不得以图标存在或生成图替代实机通过。当前无新版本通过声明。

写入所有权：root负责现有Runtime/UI、Editor、场景、项目设置、构建及集成；v07_art负责ArtSource/Art07、Assets/Art/Art07（Audio除外）、tools/art_v07（audio除外），独占这些Blender源；v07_presentation负责新Runtime/Presentation、新Tests/EditMode/Presentation、Audio资源和tools/art_v07/audio；v07_spec_audit生产只读，仅evidence/v07/input和review。Unity编辑器唯一写入者root，禁止并行编辑同一场景、字体和全局设置。

接口：规则已成功保存 → detached before/after与CommandReceipt → PresentationReceiptAdapter.Build → 独立PresentationClock；只读事件没有Session写入引用，不调用RNG或再次计分。跳过/恢复展示最终状态。UI资源按稳定C/I/O/U ID绑定，动态文案与数字由TMP绘制。

保留原有未跟踪发行ZIP与失败证据；不覆盖旧版发布。状态按实际进度更新，未测/失败不得提升为ACCEPTED。
