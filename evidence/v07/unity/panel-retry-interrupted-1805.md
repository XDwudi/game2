# 弹窗单项回归被测试运行器中断

2026-10-31 18:05UTC，运行 PLAY_ART07_ModalRedraw 的异步重试期间，Unity控制台报告 `An unexpected error happened while running tests` 与 `Too many instant steps in test execution mode: Error. Current task ExitPlayModeTask.`。CLI状态保持running，没有产生结束NUnit，不能视作通过或正常失败用例结果。

运行期间另一个代理完成了截图器原子更新，可能触发重载；这是候选原因，尚未证明唯一根因。root已取消挂起任务，后续测试需在Assets冻结/自动刷新受控时重跑。本次保留180509开始绑定与控制台观察，上一轮180321真实4项结果仍为3过1失败（尾帧缺TMP材质）。
