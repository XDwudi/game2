# 检查点 0befca 完整回归证据独立复核

状态：**EditMode 证据一致性通过；完整 PlayMode 等待完成，整版未通过。** 本文件是新检查点记录，不覆盖 `FULL_REGRESSION_CHECKPOINT_REVIEW.md` 中 86d740 完整 Play 的七项失败，也不把其后局部回归作为完整152项通过。

复核者 `/root/v07_shop_ui`，仅磁盘只读复核，未运行 Unity/Player，未修改生产、测试、门禁或 Git index。输入 art0.7.0 / p0.6.0 / schema4；当前提交 `0befca282bf5cd6da7dda9480423d36007386498`。复核使用的具体文件SHA和执行输出保存在 `artifacts/cache/v07-full-regression-0befca-review`。

## 已核实的 EditMode

真实报告 `nunit-20261101-082614.xml`：999项全部通过，失败、跳过、不确定均0，18.2774621秒；实际起止08:25:56Z—08:26:14Z。全部999个唯一fullname与Unity完整发现清单逐一相同，无遗漏、额外名称或重复项。继续使用原工具的严格999/152全发现集合及零失败/跳过/不确定检查，没有改动任何验收阈值。

报告SHA与 `.binding.json` 相符。开始08:25:56.3185610Z、结束08:26:15.3372447Z的1358项物理源文件清单和SHA顺序一致、无重复路径，并与本次只读复核时的磁盘完全一致；全部1358项已被Git跟踪。生成的BuildIdentity单独核验，生产SCOPES没有工作树修改。

## 程序集及实际加载版本

独立读取 `pdb-checkpoint-0befca2.json` 后，重新散列其指向的当前DLL、PDB及168份C#文件。5程序集集合完整，DLL/PDB标识匹配，168份文档恰好覆盖当前项目C#集合，内容校验零差异。开始与结束时真实加载的5组MVID/DLL SHA均与该PDB证明及当前磁盘一致。Runtime MVID为 `3084d099-dede-45b9-8b42-a0fe30d608d5`。

`checkpoint-resource-identity-0826.json` 内真实UTC为08:25:52.0734496Z，Editor当时 `playing=false`。已检查其实际执行的同步导入、Resources.Load和重新发现调用成功，加载内容与当前BuildIdentity相同，提交0befca、art0.7.0、p0.6.0、schema4、Unity6000.6.7f1及配置/黄金用例哈希均正确。**该命令没有返回帧号或Runtime MVID**，本复核未补造；程序集实际加载证据来自上述测试起止绑定，二者分开叙述。

## 未完成边界

完整PlayMode当前发现152项，root已启动同源全量执行，但本次复核时尚无最终报告；不得标为完整回归通过。后续报告到齐后再检查152项名称、零失败/跳过、源起止及程序集身份，并更新新检查点限域结论。AudioMixer实际资源、Windows Player/性能、macOS实机和人工体验仍不在本次通过范围，不能由EditMode通过推定。静态源一致性也不替代新强化录像和其它最终媒体的独立实图审查。

审计脚本是旧只读审计的cache副本：参数化期望HEAD和identity路径，并按本次真实idle Editor资源读取格式处理，不再假设旧0743的playing/frame/MVID字段存在。原稿、旧输出与失败报告均未修改；差异保存在 `audit-adaptation.patch`。
