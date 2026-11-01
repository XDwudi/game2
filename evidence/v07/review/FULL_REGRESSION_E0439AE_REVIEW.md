# 检查点 e0439ae 完整回归证据独立复核

结论：**同源完整EditMode与PlayMode自动化回归证据一致性通过。** 不代表整版美术/体验、实际Player、性能、Mac或发行通过。复核者 `/root/v07_shop_ui` 仅做磁盘只读核验，未运行Unity/Player、未修改生产/测试/门禁/index。输入art0.7.0 / p0.6.0 / schema4，提交 `e0439ae0b6efdcd12ef2f756e8bf5a91b71d0b1b`。

## 两份完整实际结果

| 真实NUnit报告 | 通过/发现 | 失败/跳过/不确定 | 耗时 |
| --- | ---: | ---: | ---: |
| nunit-20261101-083707.xml，EditMode | 999/999 | 0/0/0 | 17.94784秒 |
| nunit-20261101-085726.xml，PlayMode | 152/152 | 0/0/0 | 1181.457388秒 |

两份报告所有唯一fullname与各自Unity完整发现清单逐一相等，无遗漏、额外项、重复或非通过叶节点。没有修改999/152全发现集合和零失败/跳过/不确定门槛。新Play实际运行08:37:44Z—08:57:26Z；相旧075917的152个名称集合完全不变，原七项失败全部保留并在本轮通过，详见cache中的逐项映射。原失败报告没有覆盖或删除。

## 物理源、程序集与实际加载身份

两份原binding的报告SHA均与XML吻合。Edit开始08:36:48.9688187Z、结束08:37:07.6234989Z，以及Play开始08:37:44.7725417Z、结束08:57:26.8970478Z的四份清单均为1358项，顺序与SHA完全相同、没有重复路径，并与本次复核时的磁盘一致；全部1358项已Git跟踪。08:57:55真实SaveAssets完成后，本人08:59复核生产SCOPES仍干净。

相0befca的生产scope差异精确只有PlayerVerification的4行已审导航：通过真实FullScoreDetails按钮，再执行原ScoreDetail滚动检查；其余源码相同，未改规则/资源/阈值。该变化已在本检查点绑定，不能借用旧检查点未完成的Play。

新 `pdb-post-full-e0439ae-0858.json` 的5程序集集合完整，DLL/PDB标识匹配。独立重新散列当前DLL、PDB和168份C#源；文档集合恰好覆盖当前项目C#，0差异。两报告四个起止时点真实加载的5组MVID/DLL SHA均与该PDB证明和当前磁盘一致。

实际资源读取 `checkpoint-resource-identity-0837.json` 的真实UTC为08:36:44.2567147Z，Editor当时playing=false。同步导入、Resources.Load与完整发现调用成功；资源内容与当前identity相同，e0439ae、art0.7.0、p0.6.0/schema4、Unity6000.6.7f1及配置/黄金用例SHA均正确。本次实际返回的Runtime MVID `70c58cbc-d0c0-4a8a-bea7-3e3f1ff9f383` 与两报告/PDB/当前程序集相符。没有补造帧号。

## 归档和偏好恢复

独立检查 `cache/v07-full-play-e0439ae-085726/archive.json`：150个唯一隔离目录恰好等于NUnit输出引用集合；2920个文件、901张PNG、1,019,133,034字节。逐文件检查归档与原Temp的大小/SHA，全部相符，没有缺失、额外文件或重复路径，XML/binding副本也与原件一致。**本项是证据完整性检查，没有宣称本人实看901张图或据数量签美术通过。** 相关模块的实图报告继续各自限定范围。

独立比对08:35:30.2709142Z基线与08:57:55.2507065Z实际读取：所记录6个整数偏好的had/value、5个按键绑定的had/value，以及volume/hadVolume均完全相同，font100、guideSeen1。后者实际执行SaveAssets，场景不dirty且不在Play。仅对这些已记录字段确认恢复，不扩称所有未知偏好。

## 旧取消与验收边界

0befca完整Play仍为未完成：实际08:31:01.1628705Z公开CancelTestRun对GUID `37f8293c-bbd3-4654-9638-a811d86011b2` 返回cancelled=true，原开始binding和隔离目录已保留；不把取消转成通过，不改变其偏好恢复证据边界。旧86d740的七败也继续保留；本轮同名真实通过作为修复后新证据。

本报告只关闭e0439ae本轮完整自动化回归及绑定核对。真实AudioMixer资源、Windows Player/原生显示/性能、macOS实机、最终媒体与人工体验仍按STATE等待或保留既有失败；不能由Editor测试推定通过。后续Editor退出若保存额外动态资源或其它源改变，应重新核对并按影响重新绑定，不能静默沿用本报告。

旧Edit-only版本已原字节另存cache保留其历史SHA。只读审计脚本继续显式指定HEAD/identity/PDB，正式工具和验收阈值未变；全部输入SHA、输出、旧失败映射与归档/偏好核对见 `artifacts/cache/v07-full-regression-e0439ae-review`。
