# S09 / S22 / S02 独立源码复核准备

状态：STATIC_REVIEW_ONLY，实际画面待补。原 art0.7.0 / p0.6.0 / schema4 和验收标准未改。本报告审查他人实现，不是实施者自签；没有启动Editor或写生产文件。

## 静态结论

1. **S09不重复规则命令。** `PresentCommitted`在已成功提交、非duplicate之后arm，`SettlementIdentity07`按run/settlement及已提交commandId标识；冷加载不arm。重绘使用同一表现时钟elapsed，shown集合阻止同一次回执重播。新增局部动画只改变UI位置/缩放/alpha/金币符号，没有金币、Dice、RNG或Save写入。空白、跳过、失焦/应用暂停、返菜单只Complete；“进入商店/查看结果”仍只有一个原Continue命令回调。根显示层读既有settlement和effects，收入仍标已入账、基金仅卖价、临时骰仅显示已移除；通用交易现金纸签在新结算时早退，避免二次到账假象。
2. **旧档元数据不改变存储选择和轮转。** `AtomicSaveStore`仍先active后backup；TryRead新增captureLoadMetadata默认false，只由Load传true。版本字段是只读get属性；Load开始清空，任一成功候选再次清空，Save验证探测不填它。原legacy副本命名/写入条件、rejected副本、File.Replace/backup选择和校验未改。UI要求没有当前State才展示旧版本入口；成功恢复有效backup不会误套旧版本页。“保留备份并开始”只开既有新局确认面板，取消不发NewRun，不修改原文件。旧档页不删除设置或历史。
3. **S02五骰不消费Run RNG。** `DiceFactory.CreateStandard`新建DieInstance并逐FaceState深复制配置，固定5颗与固定姿态，不创建Session/HandState。`ShowStaticPreview07`使用独立`$static-preview:`键、null当前面结果、preview=true，改的是表现Transform；HidePreview同时清理预览键。随机种子按钮用独立crypto只填输入框，正式NewRun仍仅在合法uint或留空确认后调用一次。非法值保留输入，取消只关UI；已有PendingSave分支保持原保存错误流程。
4. **新局生命周期。** 新协程以modalIdentity和generation终止，关闭同步恢复主菜单/桌面并隐藏静态预览；纸、骰孔和五骰位置绑定同一panel矩阵。实际孔内mesh边界、缩放、100/125/150%可读性与退出尾帧不能靠代码确认，等待真实截图/连续帧。

## 当前证据与未结论

- 已读取handoff及集成hunks。当前`GameApp.Art07LegacySave.cs`、`GameApp.Art07NewRun.cs`逐字节与冻结候选相同。
- root在审查期间修订结算identity加入真实commandId，并补OnApplicationPause完成表现；两条增量已另看diff。此修订合理区分同run内Debug重建同关后的新结算与旧收据重播，不能因此自动判实际运行通过。
- 真实EditMode `nunit-20261101-061610.xml`三项3/3：旧active/backup版本保留、成功backup清空诊断、有效active与Save探测不携带旧诊断。原`SAVE_V05_IsolatesBothLegacySchema2FilesAcrossNewRunBackupRotation`全回归仍需按任务检查，不由新增三项替代。
- 原结算Play `061649`两项1过1败保留：Normal样例在IsPlaying断言失败；不是图像通过。新identity修复后的运行结果未在本准备报告中签署。
- 新LEGACY PlayMode的CopyLoadIssue07最初缺剪贴板还原；root已按提示补try/finally，独立复读确认保存并恢复原clipboard，复制内容断言保持。这保护验证环境，不修改产品要求或断言。
- 尚未查看本次新S02/S09/S22实图，不签其画面、动画时长、金币可见性、旧档长文本滚动、全部字号或按钮命中；Editor证据亦不替代Player/Mac。

具体输入与源码SHA见`NEW_SCREENS_STATIC_REVIEW.bindings.json`。此报告不代签本人之前实施的S07抽屉，也不扩大到整体验收。
