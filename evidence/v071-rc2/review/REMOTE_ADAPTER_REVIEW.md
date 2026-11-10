# RC2 下载版 Windows 适配器独立复核

结论：未见本次适配引入的阻断项，可以在真实新 manifest/下载/服务器收据齐备后执行只读预检，再由 root 执行下载版诊断。**此报告未运行真实预检、解压、Player、网络或 Registry，不把离线检查签成真实下载版通过。**

复核人 `/root/project_audit`，作者为 `art_assets`。新 adapter SHA `bcd767c00aa160c38976cd0268450585644d3f68ba43092a6340fd6e99979343`。直接比较新旧 AST，七个函数完全相同：require、check_input_hashes、v071_preflight、validate_anchor_receipt、validate_server_asset、validate_zip_members、visible_popen。只变 main 的 RC2 路由/元数据，以及 receipt 增加显式tag条件；原 v3 runner SHA仍 `859333e3d91837d136d17d091cddb3449f53b6b9ec7a5dd07d429daa324f07f0`。

RC2 tag 由 argparse限定，传入真实 publisher.release_inputs；manifest、upload/download/server receipt、远端下载文件均从同tag API取路径。与当前已审 publisher 的关键字参数兼容。新receipt必须显式rc2，拒绝缺字段的旧rc1。sourceCommit/tagCommit/manifestSHA、server release/asset ID与digest、草稿未公开状态、对应URL都保留三向核对。草稿 browser alias 只作实测API事实，不当Git tag；正式 tag 仍精确校验。该工具要求 draft，因此应在 publish 之前做下载版运行。

本轮默认不传旧binary-only anchor；接口为兼容性保留且仍要求独立真实receipt，不能自动借用b116/41aface。原BuildIdentity/GUID/boot.config/Runtime入口、完整ZIP每成员SHA/CRC/mode、安全路径、全新解压前后成员树、原构建树、输入源及工具SHA守卫都保留。最终调用原runner.execute，普通存档/游戏偏好守卫、可见Player、自有单进程句柄/超时清理、原20秒观察与错误分类不变；资源诊断仍不替代180秒性能或原生显示验收。

独立实际重跑作者原 `negative_smoke.py`：**11/11、56负边界**，无源码改变。仅通过临时覆盖 `tempfile.mkdtemp` 的dir，把合成fixture输出限制到审查者audit目录；测试源原字节执行，Fake进程没有实际启动。报告路径/SHA、八项输入源首尾稳定、AST清单见 `REMOTE_ADAPTER_REVIEW.bindings.json`，SHA `47ad760b63df7e0b784cab3a9ee124ce7869d87e66e94ee32eec7bf13472b006`。

真实执行仍须用新rc2收据、未存在的新目录；保持原旧证据不覆盖。实际结果只有在exit、报告完成、idle完成与全部守卫同时符合时，才能写 `downloaded_extraction_diagnostic_completed_not_acceptance`。
