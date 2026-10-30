# v0.5 验收摘要

源码：`967f309a6be402612514714f46f3743421267799`。Windows 与 macOS 构建嵌入同一源码、p0.5.0 配置和100例黄金集哈希。当前是 **Windows技术检查通过、Mac实机待验的候选交付**。

| 检查 | 实际结果 |
|---|---|
| Unity EditMode03 | 554/554，0失败、跳过、未定 |
| Unity PlayMode04 | 24/24，0失败、跳过、未定 |
| 独立规则对照 | 1024计分、512概率、128完整商店轨迹；附录C98项均有映射 |
| Windows完整ZIP新目录解压 | 实际运行，无缺DLL/资源；包内版本页与源码身份一致 |
| 正常操作 | seed20261037，161条全部吻合；第9关正常失败，未伪称24关正常通关 |
| Debug完整流程 | 单一测试初态后178真实命令，24关、8次高压、23个商店，终态RunWon |
| 手册/存档 | 13行、20次纯读取、schema3检查点提交与重载、幂等、坏历史回退均通过 |
| 视觉 | 实际720p/1080p/1440p，包括10骰、K8、10控制器、5道具；独立查看截图 |
| Windows压力 | 180.006992秒、10746完整帧，LateUpdate间隔P95=16.6913ms≤16.7；最大24.872ms，不声称每帧均达标 |
| Mac构建 | Succeeded，0错误、71内置/URP shader警告；6个Mach-O均含arm64+x86_64 |
| Mac运行/封装 | 未执行；需M5实际运行与ditto最终重包，Intel运行另列未验 |
| 远程发行附件恢复 | 用户批准Release替代LFS；同版3附件上传，完整重新下载SHA/大小/CRC全匹配，下载的Windows包新目录启动与checkpoint恢复、正常退出0；Mac原生启动待验 |

Windows实际设备为Ryzen9 5900X、RTX3080、约64GB内存，操作系统和完整指标由Player报告记录。采样负载、帧率器、观测方法及16.7ms门槛均未修改。首轮16.77ms失败完整保留，优化针对重复存档解析，未删历史、未删慢帧。

主要证据：`evidence/v05/player/player-20261030-054053-169bea91/`、`evidence/v05/review/`、`evidence/v05/unity/`。原始Editor启动日志含账户参数，不入Git；发布的是测试报告、BuildReport和必要警告清单。

Mac下一步：解压 `DicePrototype-p0.5.0-MacVerificationKit.zip`，在M5终端输入 `bash ` 后拖入 `VerifyMac.command` 并回车。脚本保留输入原包，在Mac重新封装、全新解压、原生arm64运行，并用Windows真实存档和逐步规则状态对照。回传生成的 `MacVerification-Evidence.zip` 和最终Mac ZIP；人工鼠标/触控板、退出续玩表单也需填写。说明见工具包内“执行说明.md”。

补充：Windows本地新解压包在新进程恢复旧检查点、提交324分、状态/幂等一致、主菜单实际渲染，正常关闭exit0。一次额外延长回放step121发生文件替换I/O错误，失败和旧档/候选完整保留，恢复复核见review；不把该失败运行写成全流程通过。

发行附件实际证据：`evidence/v05/delivery/github-release/`。发布标签 `p0.5.0-rc.1`，游戏源码与包内字节未变；短启动不重复认定完整压力结果。
