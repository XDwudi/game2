骰子构筑 v0.71 · 全量美术测试候选

发行p0.7.1，美术art0.7.1，规则p0.6.0，存档schema4。
本次实际构建源41aface80bba6c1e008bd3d35344210a28003553，Unity6000.6.7f1 / Mono。完整EditMode1018/1018、PlayMode182/182通过；Windows与macOS Universal均构建成功。版本与实际限制分别见BUILD_IDENTITY.json和REVIEW_CANDIDATE.json；这不是整版或人工风格验收通过声明。

全部71项内容插画（50控制器、7道具、7服务、7强化）与公共符号、附魔、特效、纸面、卡背、字标、桌布已统一接入。临时骰小钟/轻影、真实附魔反馈、高档Combo环/短弧、强化商店和详情纸面同步更新。骰子沿用经技术复核的陶瓷母模型。插画负责主题表现；精确骰点、名称、价格和得分仍由引擎按真实规则显示。

Windows完整解压后运行DicePrototype.exe，保留全部Data与DLL。Windows构建GUID e36a7047e1f44fe9b16387cbf61ab639。原生独占全屏确认仍未通过，试玩使用默认窗口模式；本次独立窗口模式完成普通145步、Debug24关/175步、18项UI及50项动画检查，0Unity错误，跨进程显示恢复通过；普通存档与成品文件守卫一致。压力采样180.0162698秒/10102帧，P95 16.8078ms高于16.7ms；GPU有效计时覆盖91.91%低于95%，性能仍FAILED。实际限制同时见随包REVIEW_CANDIDATE.json。诊断参考不能替代严格原生显示验收。

Mac保留完整DicePrototype.app。构建GUID a8eee2a94a304703b6fb1e54b3877b87；Universal包包含arm64/x86_64，未在真实Mac运行、签名或公证。Windows交叉构建不等于Mac实机通过。Mac验证工具包已含Mac游戏，按包内执行说明运行bash VerifyMac.command。

开始挑战后，先投掷，点选待锁骰，确认锁定，再提交已锁骰。默认快捷键：R投掷、L确认锁定、Enter提交、Backspace清除待选、H牌型手册、Esc设置/关闭弹窗。已锁骰可拖动调序，右键查看骰子详情。实际修改过的键位以设置页为准。

设置支持正常/快速/即时速度、减少动态、文字大小与颜色辅助。请反馈具体页面、种子、分辨率、文字比例、控制器顺序及附魔、复现步骤和截图。人工美术、动画节奏、声音与体验仍待确认。AudioMixer总线尚未接入，当前使用分类音量回退。

本次GitHub仅发布三个具名成品ZIP。实际游戏源码提交保留本地；发行标签锚定已有远端提交，GitHub自动生成的Source code下载不是本版构建源码。Mac验证工具包包含验证所需脚本和Windows对照数据。请使用Windows、macOS Universal或MacVerificationKit具名附件。
