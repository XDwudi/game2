# P0-004 / v0.7 交付与实机验收工具交接

状态 REVIEW（工具离线检查范围）；游戏构建、Player、Mac、发布及人工美术不是本任务的通过结论。输入 `P0-V07-ART.md`、art0.7.0，规则 p0.6.0/schema4 保持。执行者 v07_shop_ui；主协调者 root 负责实际引擎、双平台构建、发布与 STATE 集成，独立复核待安排。

## 范围与产物

新增 tools/v07_delivery.py、package_v07.py、release_v07_manifest.py、publish_v07_release.py、verify_v07_display_recovery.py 和 tools/macos/v07 全套。旧 v06 工具/证据保留不改。新路径为 Builds/v07/Windows、Builds/v07/macOS、Releases/p0.7.0/，受限候选另存 review-candidate。新 M5 执行说明与 PLAYER_README_V07.txt 随包读取。

发行身份要求 releaseVersion=p0.7.0、artVersion=art0.7.0、contentConfigVersion=p0.6.0、saveSchemaVersion=4。Player 要求 schema p0-player-verification-7、p07 probe；报告、环境、UI 和显示行必须绑定一致美术/探针版本。兼容保留 v06-* 原始文件名及 v06Checks 别名；校验器读取 uiChecks，不根据文件名判断版本。

新增实际 UI 检查必须包含 v07-menu-settings-navigation、v07-shop-five-tabs-fixed-shelf、v07-dice-sheet-cross-and-purity。保留全部原正常轨迹、debug24、独立存档、Debug32、拖拽、原生三模式、15秒/ESC、独立 writer/reader 异常恢复与 1080p 最大容量180秒/P95≤16.7ms 标准。受限/诊断模式仍需显式选项，不能过滤版本错误、缺失非显示检查或把失败写成通过；Mac 永远按完整标准。

## 工具验收契约

| ID | 方法与期望 | 证据/责任 |
|---|---|---|
| DT07-01 | 拒绝旧发行/美术/规则/schema、缺失环境和UI版本、缺失任一新增UI检查 | 新旧离线正负例，执行者；独立审查待验 |
| DT07-02 | 实际发现清单、NUnit完整名称与显式冻结计数相等；无失败/跳过；五程序集/PDB/起止快照完全绑定 | 离线负例通过；真实全量引擎输入由root提供 |
| DT07-03 | 两平台规则轨迹逐字段与真实输入一致，正常终态、debug24/23商店/8风险、保存/渲染一致 | 离线正负例；真实Player/M5待执行 |
| DT07-04 | 拒绝短于180秒、NaN/无效采样、P95超标、缺失原生模式；独立恢复必须不同真实PID与一致确认值 | 离线正负例；真实进程由root/M5执行 |
| DT07-05 | ZIP路径/符号链接安全、全部Mac原生组件Universal、版本/哈希/CRC/完整源绑定 | 离线正负例；真实封包及远程全量恢复待root执行 |
| DT07-06 | M5保留标准库、arch-arm64/Metal、ditto新解压、实际签名隔离状态及双向检查点，人工表初始未验 | 静态核对；真实M5与人工由用户执行 |
| DT07-07 | 发布manifest读取真实测试数与原失败，三附件同源同版；不预签整版ACCEPTED | 离线正负例；root/独立复核实际交付 |

## 运行次序（由主协调者执行）

1. 完成生产源码与独立复核、冻结提交；`python tools/v07_delivery.py identity` 生成本版嵌入身份。该派生文件按仓库既有规则忽略；两平台必须嵌入相同字节。
2. 真实 Unity 完整发现 EditMode/PlayMode、执行全量套件，取得起止 v07-test-binding-1 与独立五程序集PDB证据。实际数量尚未封版，不将估计数量写入验收结论。
3. `python tools/v07_delivery.py gate --edit <真实NUnit路径> --play <真实NUnit路径> --pdb <真实审计路径> --expect-edit-count <完整发现数> --expect-play-count <完整发现数>`。显式计数必须精确一致；797/30仅为已验v0.6回归下限，不是v0.7最终数量。任何源码变化使受影响证据失效。
4. root真实构建到两个新路径，实际Windows完整Player与独立进程显示恢复；使用本版真实结果运行 package_v07.py / release_v07_manifest.py。具体参数见各脚本 --help。输入轨迹在 evidence/v07/session，沿用规则输入须有不变核验和来源记录。
5. 按用户既定GitHub三附件渠道执行 publish_v07_release.py；对远端三个完整下载验证SHA/大小/CRC，并从新目录运行实际Windows。M5包由用户实机运行，回传检查点由Windows实际重放。工具没有执行这些动作。

## 自验与限制

`python -m unittest discover -s tools/macos/v07 -p 'test_*.py' -v` 覆盖114项离线合成工具测试（原100项保留，新增14项版本/计数/标准负例）。`python -m unittest discover -s tests -v` 按仓库要求执行20/20通过。两组原始输出与工具源SHA清单见 `evidence/v07/delivery/tools/20261031-175259-offline-checks.json`。合成fixture的7/3等计数不是游戏测试结果，不能写入发布证据。

未运行Unity、Player、Mac、真实打包、GitHub上传或远程下载；全部实际交付仍由主协调者完成。Mac原生与人工作品验收待真实设备，工具通过不能替代。
