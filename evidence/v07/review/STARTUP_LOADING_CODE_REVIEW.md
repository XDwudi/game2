# S02 异步启动读取与生命周期独立代码复核

2026-11-01；`/root/v07_shop_ui`；art0.7.0 / p0.6.0 / schema4，Unity 6000.6.7f1。只读复核spec/root实现的 `Runtime/Art07StartupLoad.cs`、`UI/GameApp.Art07Loading.cs` 及其GameApp/Display/Focus/Continue生命周期接入。此agent曾实现新局纸单，因此本报告不自签S02新局预览；仅审独立作者的启动读取模块。

**结论：静态读取隔离、同路径生命周期、200ms提示及主线程采用结果的设计未发现新的阻塞性缺陷；结合已运行功能测试可限域通过。** 最新150%最终画面由art独立图审，见 `LOADING_UI_REWORK_REVIEW.md`，不在此重复签署或扩大为整版/平台通过。

- Begin只将独立配置JSON、路径和私有AtomicSaveStore交给worker；任务不持有GameApp/场景对象/PlayerPrefs。真实Load沿原active/backup/legacy策略执行，未改Core或AtomicSaveStore规则。
- 同一绝对规范路径以串行尾任务协调，Windows大小写不敏感；公开SaveLocation继续保留原Path.Combine字符串合同。旧场景销毁仅Abandon禁止采用，已有同步IO可完成；新场景等同路径前序任务结束再真正读，不与旧worker并发写恢复文件。字典在当前尾结束后删除，没有主线程Join。
- 200ms计时从Begin开始，包含同路径真实排队时间；完成分支优先于提示分支。快速完成不人为延迟到200ms；pending无Session/规则动作，Update仍可维护显示恢复。Ready之后主线程建立Session并采用状态，Continue使用已载结果不二次读档、不重新掷骰或抽C53。
- 测试barrier只存在UNITY_EDITOR且要求明确临时隔离SaveOverride，Player无该入口。Destroy不采用旧结果；错误走原保留文件/恢复说明渠道。普通存档与偏好由测试原有setup/teardown保护，原readiness上限不变。

实际证据：072142 1080三项3/3，072245 720加载相关三项3/3（整批9/9）。本agent逐张实看其16张旧截图，pending与排队的小型“正在读取存档…”正确；ready/fast/backup/worker错误图当时仍处在菜单/模态淡入首帧，部分只有桌布或通知，**不能据此验最终画面**，且旧样本为100%而非150%。该证据缺口已交root修订截图等待与真实字号前置，art随后独立实看072646/072807两尺寸最新16张150%终态，详见上述独立图审及其bindings。

旧功能测试的真实worker阻塞是显式隔离测试屏障，不冒充实际硬盘慢读；快速路径、200ms前无提示、Continue不再读取、原文件逐字节不变、backup/legacy与销毁重载均有真实断言。旧批原文件/SHA、源快照与程序集开始结束一致性已在 `artifacts/cache/v07-settlement-backdrop-review/actual-evidence-audit.json` 校验。

保留历史问题：071431因SaveLocation规范化改变既有字符串合同而失败，后续只规范租约key、Store仍保留传入路径。Editor23240进入Play前发生SpriteAtlas原生分配崩溃，没有新的测试Awake/NUnit结果；重启Editor10504后上述3项通过不能证明崩溃根因已解决，也不能将崩溃判成loader逻辑失败。最终Player、macOS、实际存储性能及整版验收未由此报告覆盖。

本次代码SHA/旧证据关联见 `artifacts/cache/v07-qualified-review/review-bindings.json`；此审查未运行Unity、未改生产/标准。root负责STATE与最终汇总。
