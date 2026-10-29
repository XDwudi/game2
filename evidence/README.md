# P0 实际验证证据

当前交付结论见 `../docs/STATE.md`，独立最终评审见 `review/`。历史失败保留；同一个目录或脚本写了 passed，不等于整体验收通过。

## 如何阅读

- `unity/`：Unity 6000.6.7f1真实EditMode/PlayMode JSON与NUnit XML、场景装配、逐次Windows构建。最近规则/存档/帧调度为EditMode08 341项；界面为PlayMode12 20项。对照逐用例名称、源码PDB和报告版本，不能只看汇总数。
- `runs/normal-win.json`：正常种子20261032的97命令六关复现。实际Player每步核对规则、显示与磁盘，31次动画落地另有记录。此种子经过搜索，不是玩家胜率。
- `runs/player/`：每次独立Windows Player的原始JSONL、截图、存档和逐帧CSV。Debug压力与正常挑战分开保存；本轮最大场景1080p连续至少180秒，含真实自动保存和持续操作。
- `runs/pacing/`：20秒空闲显示路径/帧调度试验及独立生命周期检查。它们不替代正式压力验收。Pause/focus注入明确不是OS切窗。
- `runs/perf/`：对历史原始CSV的可复跑诊断。按相位推断的操作不是方法Profiler耗时；周期内的等待与工作位置变化不能混写成执行时间。
- `review/`：独立规格、源代码、PDB、计分oracle、真实文件IO、图像与数据复算。评审者身份和是否亲自运行Unity在各报告中明确区分。
- `art/`：真实Blender重开/导出、六面及两套动画、图标/点阵/音效的检查与源件哈希。离线图不替代游戏截图。
- `environment/sanitized/`：压缩后的Editor日志，移除启动凭据与账户标识。清理脚本和原/发布SHA清单可追溯；原始Editor日志只留本机、不提交Git。失败与诊断未因脱敏变成通过。
- `delivery/`：最终运行文件/压缩包SHA与打包校验。完整包位于仓库本机 `Builds/`，不把Unity构建二进制放进Git历史。

## 必须保留的失败脉络

最初的黄金载入、动画Player缺Animator、黑图与采集器容量问题均有失败和后续修复记录。Player04/05分别以23.5467/18.3322ms超出原帧间隔门槛。Player06虽返回原探针exit0，但Present/Complete约18.75ms，协调者没有据此放行；后续针对渲染尾部工作返工。新结果必须与旧结果及来源一起阅读，不能删除不利数据。

性能指标包括CPU、GPU、GC、UI及多种帧时点；CPU Main Thread可包含等待，FTM Present调用和渲染完成都不等同物理屏幕scanout。手机和真人体验未验收；自动通关不代替5–8人、每人至少2局的原型试玩。
