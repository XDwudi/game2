# Player06 / build09 独立技术评审

spec_audit，2026-10-29 UTC。评审范围绑定生产提交 `ba848b231ce3f1b72e53b091cf6f355b76aa88c0`，真实运行目录 `evidence/runs/player/player-20261029-024615-1c6316db`。本审查没有操作Unity；独立执行了报告/原始CSV复算、源码/PDB核对、验收映射与原图目视检查。**功能与制作回归通过；性能仍需返工，不能由原LateUpdate单轴通过推出整体性能通过。**

## 源版本及真实运行

- `.git/refs/heads/main`实际读取为上述提交。build09成功、0error/0warning、195,201,425bytes，Windows x64 / Unity6000.6.7f1、D3D11、Ryzen9 5900X、RTX3080、Windows10 19045；显示桌面144.006Hz，游戏窗口1080p。
- buildGUID `f7f0381585684fde8b60e3a4929dd73a`；Runtime MVID `e9934261-27a7-450a-8a49-f195050d6b4f`，DLL SHA256 `f57c25275053e98de123489ee8fe35e9af467e862ed81f2c0857f5216642a66f`。Core DLL SHA256 `db7798eb8ceec1febb0e020558155a24a533c23fd1229afe21ee9f82ed9c5dac`与先前已审规则版本一致。
- Editor5程序集39份项目源、Player2程序集22份项目源PDB全部匹配；保存于 `unity08-11-pdb-independent.json`、`build09-pdb-independent.json`。随后root新增Refresh末Canvas强制更新不属于本构建；本证据保留原SHA，不能据新工作树差异否定旧运行，也不能让旧运行签署新代码。
- 真实EditMode08 **341/341**、PlayMode11 **20/20**，无失败/跳过，JSON与NUnit逐用例一致；包含16pacer、11原子存档、52黄金、57计分解释及UI故障重试。核查结果 `unity08-11-independent-audit.json`。原始失败历史仍保留。
- `acceptance-build09-independent.json`将原附录C全部70ID映射到真实通过用例，并核查38图标及可编辑源、Blender/FBX来源哈希。映射不是仅凭名称签行为；行为结论同时继承此前独立规则/Session/IO/计分解释复跑及这次真实Unity回归。

## Player06功能和画面

`audit_player06.py`独立23项检查通过：正常种子20261032的97个合法命令与固定trace逐字段匹配，非Debug、每步磁盘/显示状态一致；六关得分208/424/528/884/1250/1908并进入RunWon。此种子经过搜索，不是胜率或真人体验数据。正常存档哈希在独立Debug压力后保持。

31次实际投掷落地的TRS偏差均0、实际朝上面点积满足要求；包括首帧动画、跳过、减少动态效果验证。无Unity error及拒绝截图。独立以原分辨率目视10张新图：normal-selecting1080、分层永久详情720、计分说明720顶部/中部/底部、十骰池1440、满7槽720、正常商店1080、六关结束、压力结束1080。未发现黑图、重影、遮挡、容量丢失、标签/点数或滚动解释回归。截图仍带Development Build/trial水印，本评审是原型技术可读性结论，不代替用户对美术风格的判断。

辅助spike04-lifecycle同构建完成两次20秒空闲对照及9项生命周期检查；独立28项数据复算通过。暂停/焦点是对实际组件注入回调，明确不是OS窗口切换；真实宿主Destroy、同帧Stop+Reinstall、native cap恢复已实际发生。最后恢复初始生产pacer=true/target=-1/vSync0与原窗口/桌面刷新率。详见 `pacing-lifecycle-independent.json`；不能把它说成真实Alt-Tab测试。

## 性能结果与独立判断

1080p，10骰/7槽/10控制器/5道具，180.0054246秒，10,777完整帧，190轮（含预热）Roll/Reorder/待选清理，实际自动保存与UI持续运行。观察缓冲扩容0；CPU/GC/UI覆盖100%，GPU98.6081%，无不可能GPU时间或采样期警告。

| 原始全量指标 | P95 | 最大值 |
| --- | ---: | ---: |
| 原LateUpdate间隔 | 16.6751ms | 21.9660ms |
| Main Thread | 18.7536ms | 24.2620ms |
| FTM FrameStart间隔 | 18.7553ms | 24.2659ms |
| FTM PresentCalled间隔 | 18.7520ms | 24.2804ms |
| FTM FrameComplete间隔 | 18.7508ms | 24.2087ms |
| GPU | 0.23552ms | 0.470016ms |
| 每帧GC分配 | 56,280B | 3,519,248B |
| Canvas.BuildBatch | 0.0066ms | 0.0174ms |

原探针按冻结单轴判据返回 `status=passed/exit0`、performance=`within_frame_budget`，这个结果完整保留；`player06-independent-artifact-audit.json`重算与其一致。首次审查脚本把顶层成功status误写成performance子字段值，修正审查器自己的结构假设后重跑通过；初次日志保留为 `player06-audit-initial-reviewer-status-assumption.log`，没有修改生产/测试预期。

`audit_player06_clocks.py`独立重建全部10,778原始行的三个时钟链、gap/duplicate/倒退分类及分位数，零不一致，三个诊断轴覆盖100%。它们不是物理scanout，也不新增三个同样的16.7硬门槛。这里不接受整体性能的理由是：**新节拍把原LateUpdate变整齐的同时，帧开始/提交/完成的工作分布并未同步平整，原单轴不足以代表目标。** MainThread P95较Player05的17.0318ms增大；旧Player05没有完整FTM三轴，不能声称已经严格证明新旧实际Present变差。所有阶段原始值均需保留。

独立诊断 `player06-timing-diagnosis.json`：MainThread及三FTM轴各有901个>18ms，LateUpdate只有108个。按记录延迟对齐后，MainThread900/901个长帧、FTM899/900个边界可配对长帧关联>10KB分配事件；GC相关约0.62，FTM相对记录有2–4行延迟。这支持刷新/事务后续工作造成阶段差异，但不是TMP、序列化或Canvas方法耗时的直接测量，不能据此给具体函数虚构耗时。

## 最小返工路径评估

root选择保持pacer位置、原LateUpdate和所有FTM采样不变，仅在 `GameApp.Refresh` 完成 `RebuildView` 后调用 `Canvas.ForceUpdateCanvases`。本地锁定ugui包 `TMP_UpdateManager`确实订阅 `Canvas.willRenderCanvases`，处理并清除布局/图形队列；`CanvasUpdateRegistry`也执行布局/图形更新。现有Canvas.BuildBatch的小数值没有覆盖全部TMP队列成本。

这是合理的小候选：真实排队工作提前完成后，原pacer等待可以吸收其中的波动；没有跳过UI、提高帧率、移探针或改阈值。当前wrapper覆盖Menu早退与SaveFailure；没有项目自定义willRenderCanvases/OnPreRenderText→Refresh重入环。风险是TMP内部常驻更新队列每次回调仍执行，以及同帧多次Refresh带来的额外回调，需要20项UI回归、实际三分辨率/动画与同180秒全场景检验。尚未运行该新源，不能宣称已修复。

## 原冻结AC逐项结论

| 条件 | 规格符合性 / 制作质量 | 实际运行与限制 |
| --- | --- | --- |
| AC-CONFIG | 通过 | 原机械配置、38内容/11牌型及非法负例；EditMode08 |
| AC-SCORE | 通过 | 52黄金原18字段、57trace、历史独立3072解释oracle；无中间取整/预览副作用 |
| AC-PROB | 通过 | 5向量、RNG序列、2916纹理组合与单位边界；实际EditMode及独立宿主 |
| AC-STATE | 通过 | 114事务及A/C/D边界、实际键盘/模态/锁定调序/损坏恢复 |
| AC-CONTENT | 通过于本轮原型范围 | 20C/7I/4O/7U真实命中/边界及U目标取消；38对应资源 |
| AC-ECON | 通过 | E01–15、固定供给/价格资格、真实商店UI及97步六关记录 |
| AC-SAVE | 通过 | 11真实JsonUtility/Windows持久化、独立备份损坏直接写回归、实际锁文件多次重试；validator复用无新缺陷 |
| AC-UNITY | 通过于build09 | 341/20非零真实用例、构建启动、冻结包与源码绑定 |
| AC-VISUAL | 技术通过于build09 | 新10张关键原图及全部尺寸/内容检查；美术风格仍由用户判断 |
| AC-BUILD | 通过于build09 | Windows真实运行，正常非Debug六关、磁盘/显示/实际朝向一致 |
| AC-PERF | **REWORK，不接受** | 原单轴pass保留；多阶段与实际工作分布存在未解决的代表性问题，待最小候选同场景复验 |
| AC-SOURCE | 源与追溯通过，最终交接待完成 | 本地ba848b2、可编辑源/导出/资产哈希已核；后续修复、证据归档、压缩包和远端推送由root完成 |
| AC-EXPERIENCE | **未运行** | 5–8人每人≥2局需要真实参与者；AI不代签理解、误锁、购买价值、节奏或乐趣 |

整体：本轮核心玩法正确性、制作和功能运行已有充分证据；性能返工与最终交接尚未结束，因此不能整体验收或声称完成用户的全部交付。iOS/Android本轮有理由不适用，没有移动验证或跨平台通过结论。
