# PlayMode测试框架在进入时异常

2026-10-31 18:26UTC，PLAY_ART07批次没有产出有效NUnit。TestRunner报NullReferenceException，堆栈PlayModeRunTask.cs:52（转抛controller.RaisedException），继而Too many instant steps / ExitPlayModeTask。没有测试结果，不计通过。完整console响应保存在artifacts/cache/v07-test-framework-error-1826.json。

本轮已经DisallowAutoRefresh且没有LockReloadAssemblies，因此先前猜测并行脚本导入不是足够解释。root取消挂起任务、恢复自动刷新、正常关闭独占Editor40688，使用同版本全新进程重验。未改包/测试门槛，失败证据保留；若重现，将定位更早的原始异常，而非无限重复。
