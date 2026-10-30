# P0 v0.5 GitHub发行附件独立复核

审查者 `/root/v05_spec_audit`，2026-10-30。只读生产代码、ZIP与root提供的远程证据；仅写本审查及其JSON。root负责发布、上传、下载和全局交付文档。复用已验收游戏源 `967f309a6be402612514714f46f3743421267799`，不重建、不重复完整游戏测试。

## 授权与范围

用户在GitHub LFS额度阻碍之后明确授权改用GitHub发行附件，将Windows、macOS与M5验收工具包一并上传并提供下载链接。本轮仅变更交付渠道；此前真实LFS失败保留，不能写成LFS额度已经恢复。授权不代表Mac实机、双向存档、真人体验或整版本验收完成。

## 本地候选完整性

已独立重新读取三个实际ZIP，SHA-256、大小与manifest逐项一致，完整CRC通过。`release-assets-local-preflight.json`绑定此次读取的manifest和实际结果；三个包未改变：

| 附件 | 字节 | SHA-256 | CRC/成员数 |
|---|---:|---|---|
| DicePrototype-p0.5.0-win-x64.zip | 86,022,146 | `0479e32f10be4bb1f51b65d2c0ca4f8719b3845d895f1da1c61e7545173a512d` | 通过 / 308 |
| DicePrototype-p0.5.0-macos-universal.zip | 126,654,920 | `a5f4c46fb8c7c609a09963e3a963c43566f4ae043b9bbc0cb48442a46da76297` | 通过 / 303 |
| DicePrototype-p0.5.0-MacVerificationKit.zip | 126,722,288 | `7900e055adefaf603ddc27c6efa86fba143856ecf016cad70c205eade2133d24` | 通过 / 14 |

此前包内文件、编译PDB与实际Player身份绑定继续有效，见 `final-candidate02-build-binding.json`、`candidate02-package-independent.json`、`mac-kit-candidate02-independent.json`。Mac仍为Windows交叉构建的Universal转交候选，M5工具包是验证工具，不是Mac通过证据。

## 远程上传、下载与Windows恢复

已独立读取root保存的GitHub Release API元数据：release ID `428809511`，tag `p0.5.0-rc.1`，prerelease=true。三个附件完整存在且均为uploaded，名称、大小和GitHub提供的SHA-256 digest与上表一致：Windows asset ID `691494079`、macOS `691498256`、M5工具包 `691501211`。

root实际从GitHub下载至 `artifacts/cache/v05-release-download/`，三个下载记录均为HTTP200、最终响应主机 `release-assets.githubusercontent.com`。审查者重新读取三个下载文件，独立重算SHA/大小和完整ZIP CRC，全部与冻结候选一致。证据 `release-assets-remote-draft-audit.json` 绑定API元数据、下载记录与实际字节，零差异；这是实际远程附件恢复，不是把本地文件复制后冒充下载。

Windows从该下载ZIP解压到 `artifacts/cache/v05-release-player-01/`，308个成员逐文件SHA均匹配。root记录的实际进程PID34012与EXE路径来自该目录，Runtime MVID `653f6fb3-eab0-41a5-b900-7d8fa8ff4c8d`、buildGuid `182571f77341471d9fd9acd120b05e26`符合已验收构建。新进程读取此前已退出进程的真实checkpoint，继续Submit得到324分、输入/输出完整规则状态哈希一致，写盘重读和重复命令幂等通过，原checkpoint字节未变；CloseMainWindow后实际退出0。

审查者查看实际主菜单截图，正常渲染、中文可读。本次有意在检查点导入与启动画面完成后结束，Probe仍标running、normal replay为0步；只签署远程恢复/新进程checkpoint/渲染启动，**不是第二次完整回放或180秒性能通过**。既有完整Player02证据按包字节未变继续有效。

## 最终公开发布与链接

已核对 `release-publish.json`：同一release ID于2026-10-30T08:49:59Z公开发布，draft=false、prerelease=true；发布前后仍是同三个asset ID，服务器大小/digest未变。发行页为 [P0 v0.5 rc.1](https://github.com/XDwudi/game2/releases/tag/p0.5.0-rc.1)。

- [Windows x64](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-win-x64.zip)
- [macOS Universal候选](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-macos-universal.zip)
- [M5验收工具包](https://github.com/XDwudi/game2/releases/download/p0.5.0-rc.1/DicePrototype-p0.5.0-MacVerificationKit.zip)

root实际执行的 `public-links.json`记录未登录发行页HTTP200，以及三条稳定浏览器下载链接HTTP206；Content-Range分别给出上表完整大小，返回的64字节前缀与已完整验证的下载相同。审查者核对记录与最终API/实际全量下载的asset ID、名称、大小、digest一致，结果 `release-assets-published-audit.json` 零差异。**匿名步骤是64字节前缀和响应头检查，完整SHA/CRC证据来自前一节三个实际完整下载；没有把前缀检查冒充全量下载。**

发布正文和当前manifest明确Mac/M5/Metal/原生封装/跨平台及Intel实机未验，releaseStatus仍为CANDIDATE_MAC_VALIDATION_PENDING，没有把上传或工具包提供当作Mac验收完成。游戏源仍为967f309；tag所指3d307c6是此后源码说明/证据提交，未改变包内游戏身份。全局文档的最终推送提交由root另行记录，不在本审查预填未来操作。

## 验收结论

| 条件 | 结论 | 独立依据 |
|---|---|---|
| RA01 本地包冻结完整性 | 通过 | 三包实际SHA/大小/完整CRC |
| RA02 同一发行版三个目标附件 | 通过 | 3个uploaded资产、唯一名称、GitHub digest/大小一致 |
| RA03 远程完整下载 | 通过 | 重新读取下载实体，三包SHA/大小/完整CRC全部一致 |
| RA04 下载Windows包新目录运行 | 通过，限定scope | 308成员一致、实际新进程、旧checkpoint/324分/状态/幂等、真实渲染、退出0；未重复完整压力 |
| RA05 公开预发布与稳定链接 | 通过 | draft=false/prerelease=true、页面匿名200、三下载链接匿名206及前缀/大小一致、限制说明准确 |

**本次用户授权的发行附件上传、下载链接与远程Windows恢复任务通过。** 此结论替代此前包交付渠道的LFS阻碍，不声称LFS额度恢复，也不代签Mac原生、双向存档、真人体验或整版本ACCEPTED。此前I/O失败、完整Player02性能证据与其他未验项继续保留原审查限定。

本轮review写锁已释放；未修改生产代码、ZIP、发布配置或全局文档，也未创建/操作任何GitHub发布。
