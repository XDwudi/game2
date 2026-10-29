# 当前项目状态

更新：2026-10-28（America/New_York）。P0-001 已完成开发和技术返工，当前处于 **REVIEW（人工体验待验）**。Windows技术范围的独立结论见 `evidence/review/FINAL_REVIEW.md`；不能把技术通过写成真人体验、美术风格或商业发布批准。

## 运行与权威输入

- 本机启动：`Builds/Windows/DiceCraft.exe`；便携包：`Builds/DiceCraft-P0-Windows.zip`。完整解压并保留Data目录和DLL。
- 交付入口：`docs/DELIVERY.md`；操作/存档/构建：`DicePrototype/README.md`；原始证据：`evidence/README.md`。
- 原始MD/Word原样保存在 `docs/specs/`，SHA见source_manifest；MD配置 p0.4.0 / save schema 2 为权威。原聊天只作背景，没有覆盖v0.4。
- 本轮Windows x64，Unity 6000.6.7f1 URP/uGUI/TMP/Input System，Blender 5.2.3 LTS。六关、38内容和所有冻结规则按策划实现。长期PC/iOS/Android意图保留，移动端未验收。

## 最终技术证据

- 代码：`f060098533d26e99e7eba7d0360ecfea78cbe517`；build10，0错误/0警告，buildGuid `ee0c4c6ab89e41008cade950261da73a`，Runtime MVID `25bd34a0-8ad3-40f3-acb3-097ef4cd6fab`。
- EditMode08 **341/341**，NUnit `nunit-20261029-024239.xml`；PlayMode12 **20/20**，NUnit `nunit-20261029-025518.xml`。最后修改只涉及GameApp UI布局；341项覆盖源未变，经独立PDB核对合理复用，UI20项实际重跑。
- 规则含原52黄金×18字段、概率/配置、114事务、11真实存档与16帧调度边界；独立计分解释3072组合、附录C全部70ID及38项内容/源资产映射通过。完整源码/测试/构建绑定见独立评审。
- Player07：`evidence/runs/player/player-20261029-025732-31091505`。非Debug种子20261032、97合法命令六关完成，得分208/424/528/884/1250/1908；逐步规则/显示/磁盘一致，31次真实动画落地通过。种子经过搜索，不是玩家胜率证据。
- 720p/1080p/1440p实际截图覆盖普通流程、10骰/7栏/10控制器/5道具、永久/当前/有效分层、详情、商店与逐步公式。可编辑Blender/FBX、两动作、38SVG/PNG、12点阵、10音效已交接并实测集成。风格和听感仍由用户/真人判断。
- 1080p最大场景连续 **180.0146秒**，10,750完整帧。原LateUpdate帧间隔P95 **16.6807ms** 满足冻结≤16.7ms；平均16.7440ms（约59.72FPS），最大24.5388ms。MainThread P95 16.9783ms；FTM开始/Present/完成P95 16.9825/16.9369/16.9452ms，不能当成物理scanout。
- GPU P95 0.23552ms、覆盖98.52%；UI覆盖100%、P95 0.0066ms；GC P95 56,280B、最大3,519,296B。Unity错误、拒收黑图、采样计时警告、帧记录扩容均0。没有删掉慢帧或把缺失GPU值补零。
- 真实生命周期检查覆盖重复安装/停止、同帧重装、宿主实际销毁和原生帧率恢复。focus/pause为实际组件回调注入，OS切窗未实测。失焦不忙等空转。

## 保留的限制和失败来历

原Player04/05分别P95 23.5467/18.3322ms失败。Player06原探针虽exit0，但Present/Complete约18.75ms，协调者没有放行。最终在每次Refresh完成已排队Canvas/TMP布局，原pacer、采样点、阈值、压力日程均不变；独立比较确认899组旧刷新长短摆动消失。全部旧报告、失败日志与截图保留。

长历史事务仍有少量慢帧和约3.5MB分配峰值，没有声称全部帧≤16.7ms；测试机器为Ryzen 9 5900X/RTX 3080/约64GB内存。其他硬件、物理显示延迟、长期功耗/温升尚未测。后续扩内容或平台须重新验收。

真人5–8名未参与设计者、每人至少2局 **未执行**；已准备 `docs/tasks/P0-PLAYTEST.md`。10–20分钟局长、难度节奏、误锁理解、购买价值与乐趣需实际玩家反馈。AI不代签体验，不自动进入量产或发布。

## Git与交接

origin为用户指定 `https://github.com/XDwudi/game2.git`，作者XDwudi / lxd199541@126.com。源码历史已推送main；最终文档、脱敏证据与包清单继续随交接提交。代码最后变化为f060098，后续文档/证据提交不改变构建。`evidence/delivery/verification-binding.json`绑定规格、配置、NUnit、实际EXE/DLL和运行CSV；包清单与SHA在同目录。

源工程、可编辑美术、配置和测试证据入Git；Unity缓存、完整构建、个人存档、原始含账户参数Editor日志不入Git。发布日志仅做必要脱敏，原/发布SHA保留。构建包包含字体许可。

## 文件锁与下一步

生产文件已冻结，无活动Unity/Blender写入者。本任务启动的Editor和验证Player均已退出；没有停止其他项目的进程。各子agent生产写入锁释放，独立评审只保留证据输出。

下一步由真人使用当前构建完成试玩工作表，再按真实问题决定修复或设计变更。已确认的玩法输入无需重复补填；普通缺陷可沿现有测试、构建和独立复核流程继续。
