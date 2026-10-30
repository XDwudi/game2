# P0-002 发行附件交付补充契约

日期：2026-10-30。目标：将现有 Windows、macOS 与 M5 验收工具三个完整 ZIP 共同上传到用户仓库的一个 GitHub 预发布版本，提供可直接下载的链接。

## 已确认变更

本轮用户明确授权：“可以改用 GitHub 发行附件，把 Windows、macOS 和 M5 验收工具包一起上传，完成后把下载链接发我。”

因此本次以 GitHub Release 附件替代 P0 v0.5 原规格的 LFS 交付渠道。原规格文件不改写，历史 LFS 配额失败证据保留。V05-GIT/P15 中的远程字节完整性和新目录运行要求继续保留；不再要求为此恢复或购买 LFS 额度。Mac 实机、跨平台存档与人工体验要求不因渠道变更而免除。

## 输入、边界与所有权

- 原游戏源码 `967f309a6be402612514714f46f3743421267799`；输入为 `Releases/p0.5.0/release-manifest.json` 绑定的三个 ZIP，不能重新打包或修改其字节。
- 仓库 `https://github.com/XDwudi/game2`；使用同一预发布版本 `p0.5.0-rc.1`。先草稿上传，完成三包下载校验后发布。
- root：唯一 GitHub 发布/附件写入者，负责下载、Windows短启动、manifest与文档。独立审查 `v05_spec_audit` 只写 `evidence/v05/review/`。
- 不调整游戏、测试门槛、签名或账户付费配置；不覆盖已有发布或同名不同字节的附件。

## 验收与证据

| ID | 方法与期望 | 证据 | 责任 |
|---|---|---|---|
| RA01 | 三个本地包与冻结manifest的大小/SHA一致，ZIP CRC全部通过 | release-assets-local-preflight.json | 独立审查 |
| RA02 | 同一Release下恰有三个目标ZIP，state=uploaded，服务器大小/digest匹配 | GitHub API附件记录 | root执行，独立核对 |
| RA03 | 从远程实际重新下载到此前不存在的目录文件，大小/SHA/CRC逐项一致 | 每附件download.json、下载实体 | root执行，独立读取实体 |
| RA04 | 从下载所得Windows ZIP全新解压，实际新进程渲染与恢复旧检查点、状态/计分/幂等一致，正常退出 | 路径/PID/退出码/Player有限探针 | root执行，独立审查；Mac运行仍待验 |
| RA05 | 发布为prerelease，三条浏览器下载链接实际可下载；正文明确Mac实机待验 | 发布后API与匿名下载证据 | root执行，独立审查 |

状态随实际结果写入STATE和release manifest。附件交付通过只关闭本次上传与下载任务；不代签整个双平台游戏版本ACCEPTED。
