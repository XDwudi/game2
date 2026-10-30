# P0 v0.5 候选交付

本轮实现与 Windows 技术验收已完成，macOS Universal 候选包同步提供。**整版本仍处 REVIEW：Mac M5 实机、跨平台存档和真人体验尚未完成。** 这是测试原型，尚未代签正式发行。权威输入为 `docs/specs/P0_v0.5.*`，源版本为 `967f309a6be402612514714f46f3743421267799`。

## 下载与运行

[同一GitHub预发布版本 p0.5.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.5.0-rc.1) 已发布三个完整附件。2026-10-30用户明确同意以发行附件替代额度耗尽的LFS，原失败记录保留；现在直接从下列链接下载，无需Git LFS。

- [Windows x64（86.0 MB）](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-win-x64.zip)：完整解压，运行 `DicePrototype.exe`，保留同目录 Data 与 DLL。
- [macOS Universal（126.7 MB）](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-macos-universal.zip)：含arm64与x86_64，是Windows交叉构建候选，Mac原生重包与实机验证待执行。
- [M5验收工具包（126.7 MB）](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-MacVerificationKit.zip)：已包含同版Mac包及真实Windows检查点。复制到Mac并解压，终端输入 `bash `，拖入 `VerifyMac.command` 后回车。需要Python3.9+。按“执行说明.md”完成手工检查并回传 `MacVerification-Evidence.zip` 和最终Mac ZIP。

三个附件已从GitHub重新下载到新文件，大小、SHA-256、ZIP CRC均与冻结原包完全相同。下载所得Windows包已从新目录实际启动，恢复旧检查点、324分提交、状态与幂等检查通过，主菜单渲染正常，退出码0。Mac包的下载完整性通过不等于M5运行通过。

精确 SHA-256、字节大小、平台状态及构建身份见 `Releases/p0.5.0/release-manifest.json`。macOS 签名、公证、Metal与原生封装均按实际未验状态记录；包含 Intel slice 不等于 Intel 运行通过。

## 已实现

- 66大顺在最后一栏为6时给该骰基础分加6，再参与完整乘区，价格6；购骰与提交容量独立。
- 多提多得提交+2/投掷−1；数字多多阈值3；蓝色疯狂×3；24关与增强红骰奖励。
- 控制器增至40个。商店生成实例时固定附魔：无80%、不占栏12%、触发基础分+5为6%、触发整手×1.2为2%。
- 达标后保留剩余提交机会，由玩家选择结束本轮；局内13行牌型手册显示升级次数、倍率与可达性。
- schema3存档、旧Run备份隔离、中文图标/字体、三分辨率布局、独立构建配置与版本页。

## 实际验收

Unity 6000.6.7f1，EditMode **554/554**、PlayMode **24/24**。独立实现对照1024计分、512概率、128商店轨迹，98项效果验收均映射。双平台源码/PDB与包内资源身份已核对。

Windows 从全新解压包运行：正常161命令逐步吻合，第9关正常失败；另一个明示Debug初态完成24关、23商店。该流程测试不代表正常难度已验证。13行手册和20次纯读取、720p/1080p/1440p画面通过技术复核。

在 Ryzen 9 5900X / RTX 3080 / 约64GB设备，最大场景连续180.006992秒，10746完整帧，原门槛P95 **16.6913ms≤16.7ms**；最大24.872ms。首轮16.77ms失败记录完整保留。修复针对重复存档解析，未减少负载、历史或慢帧，未更改采样和门槛。该结论不能推广到M5或其他设备。

详细证据：`Releases/p0.5.0/SMOKE_TEST.md`、`evidence/v05/review/FINAL_TECHNICAL_REVIEW.md`、`evidence/v05/player/player-20261030-054053-169bea91/`。纯源码已从GitHub全新克隆并通过Git对象检查。新进程实际恢复旧检查点、324分提交和幂等检查通过，退出码0；前一检查使用本地ZIP；本次另用真实GitHub下载包重验相同目标并通过。另一次延长正常回放发生文件替换I/O错误，旧active/backup与tmp保留，详见独立恢复复核，不删失败记录。结果在 `evidence/v05/delivery/`。最新状态以 `docs/STATE.md` 与 manifest 为准。

## 尚待完成

M5 原生 arm64/Metal、180秒性能、鼠标/触控板、Mac最终封装及两向跨平台存档需要实际Mac执行；71条Mac shader构建警告包含实际使用的URP路径，须在实机排查。Intel实机未验。40–75分钟局长、难度和乐趣需要真人试玩。本轮已提供具体可运行验收包，不把这些未完成项写成通过。

本次渠道授权和RA01–RA05验收见 `docs/tasks/P0-V05-RELEASE-ASSETS.md`；远程API/下载/启动证据在 `evidence/v05/delivery/github-release/`，独立结论见 `evidence/v05/review/RELEASE_ASSET_REVIEW.md`。
