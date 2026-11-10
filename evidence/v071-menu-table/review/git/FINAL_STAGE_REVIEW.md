# 最终暂存范围复核

**通过本次源码同步范围检查，没有发现阻断 commit/push 的暂存问题。** 审查人 `/root/dice_sample`；只读 index/Git 对象和对应文件，写入仅本 cache 目录。没有 Git 写入、网络、Unity 或生产改动；不替代已由其他代理完成的实机、制作质量及人工验收。

- 复核基点 HEAD：`32d715b65f0be4240d9f21968e22d61fc4304821`，当前分支 `codex/v0.71`。这是支持资料提交；本次 215 件尚未 commit，不伪造其未来提交 SHA。
- `git diff --cached --name-only -z` 与 `visual-stage-paths.json` 精确相等：**215/215**，无多余/缺少/重复路径。全部 index blob 与工作区原字节一致，审查前后 index 对象不变。
- 暂存内容共 **46,341,487 B**；最大单件为 2,529,798 B 的菜单概念 PNG。范围是菜单/桌面源、运行资源、测试、文档与明确选出的专项证据；没有 cache、artifacts、Builds、Releases、ZIP、DLL/EXE/PDB 或 raw Editor/Player 日志。保留的 `python-tests.txt` 是限定 Python 测试 stdout，不是 Editor 启动日志。
- 对全部暂存文本执行 token 格式、私钥头、含密码/签名 URL、字面量 Authorization/access_token/password 等限定模式扫描，**0 命中**。这不是对所有未知秘密格式的绝对证明；未读凭据库或环境。
- 两张生成 PNG 与 record 的 SHA/字节数/1672×941/RGB 全部一致；两个完整提示词非空并已绑定 SHA。两张参考在 index 中存在且哈希匹配；生产背景 PNG 与其原图逐字节相同。
- 所有暂存 Assets 实体均有对应 `.meta`；背景目录 meta、新 Editor 工具 meta、新测试 meta 均在本次清单。材质 `_TableMap` 的 GUID `ef3c92dddbd20ce4ea073cd048341e89` 精确指向该纹理，`_UseTableMap=1`。既有 Gameplay/Art071Style/ExtraCapture/CardReadability fixture 依赖均已跟踪，未漏上传共享辅助代码。
- 两阶段 evidence manifest 的 **99+55=154** 个复制件都在 index，逐件对暂存 blob 重新核 SHA 与大小，**0 差异**；六个实际通过报告及绑定同在 index。保留早期失败记录，不把最后三轮 1+1+1 扩写为最终源重跑完整矩阵。
- `git -c core.whitespace=cr-at-eol diff --cached --check` 仅报两件 Unity Backgrounds meta 的 **10 处空字段尾空格**，已实读为 userData/assetBundleName/customData 等空值。原字节保留以匹配真实源绑定。默认未指定 cr-at-eol 的命令另把原 CRLF 报为尾空白，不能误称默认命令输出只有10处。

本地 `main` 仍为 `7de9857fe4072852b54f90118e991ff690c3fa98`。本地没有 `refs/tags/p0.7.1-rc.1`，因此本审查不能通过本地 tag 声称已核远端未变；root 已另行只读回读远端 main=`7de9857`、旧 tag=`b11694f`、新分支不存在，这里明确属于 root 的网络证据。没有建议改 main、旧 tag 或旧附件，普通新分支上传由 root 执行。

机器绑定 `FINAL_STAGE_REVIEW.bindings.json` SHA256：`1cc6cb83e994eab8fd93da25dd39ea625a1cef04b1df5203af3a5510e3d7e55b`。包含逐件 index blob OID/SHA/大小、源/meta/manifest 核验、分支状态及复核边界。后续 index 若改变，应据实际差异补核，不能把此结论自动套用于其他提交。
