# 公共Krita源原生重开补证：工具执行未完成

2026-11-01，root根据v07_art实际CLI记录核对。62公共KRA已存在结构/合成检查；本次补充原生重开不修改资源、生产脚本或验收标准。当前补证**0项通过，62项原生重开仍未完成**，不把没有PNG的exit0计为成功。

第一次隔离offscreen启动中桌布导出进程0xC0000005，其余已记录任务未生成目标PNG；会话停止。使用旧CLI参数及Windows平台的桌布代表对照75秒超时。随后选取当前KRA/PNG SHA均与历史71项成功记录相同的C02作为阳性对照，同样75.047秒超时、无PNG。已知成功样本在当前隔离启动环境也失败，不能把原因归为桌布文件损坏；更底层原因未确定，不再重复尝试。

128个源/运行导出/清单文件前后字节与SHA一致，用户Krita两个配置文件前后相同，自有Krita进程已退出。没有Unity或Blender写操作。历史71项成功记录及其他结构检查仍按原源/环境有效；此记录不降低已有资源质量结论，也不把结构检查冒充原生应用运行。

原始记录保留在 `artifacts/cache/v07-common-krita-roundtrip/`：`attempt-summary.json`、`report.json`、`positive-control-selection.json`、`positive-control-c02/report.json`、两个Markdown报告、各命令与原始stdout/stderr。私有profile目录包含复制的应用配置，**仅留本地缓存，不纳Git/发行附件**。

| 原始记录 | SHA-256 |
|---|---|
| attempt-summary.json | `0eb42185c37fd7bc5dcdf4ee31788fdba8c86e262bb92e8ce611bd0855ac8f08` |
| POSITIVE_CONTROL_REVIEW.md | `43e819267d63ef04469821c6a7bfe06e4f18a3051bb19d918e0d7802406e44c5` |
| positive-control-selection.json | `b77a7fc7a4bd63bd6fe1b9521dc8c6d88acd7b567087435365bed33d7e6a7f8d` |
| positive-control-c02/report.json | `1935a85b6001a555198b4eaec3762dce5cebe8b77f63c6221e0e368c609ca971` |
