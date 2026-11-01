# p0.6.0 规则轨迹在 art0.7.0 中复用

2026-10-31：root 执行 `git diff --quiet 08ef933f2fbc3198a5e357ecebaa2758fbee8643 -- DicePrototype/Assets/Game/Core DicePrototype/Assets/Game/Data/P0Config.json DicePrototype/Assets/Game/Data/GoldenScores.json`，退出0。当前美术版权威规则保持p0.6.0/schema4。

本目录 normal-run-trace.json 和 debug-24-trace.json 是对应 evidence/v06/session 文件的原字节副本，用作新Player的确定性输入；没有改成v0.7实测报告，亦不代表新UI或新构建已通过。新Player必须真实执行这些命令并产生独立状态/存档/规则/显示结果。normal轨迹为145步且第8关明确失败；debug轨迹175步覆盖24关/23商店，不能拿debug通关推断正常平衡。
