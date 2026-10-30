# P0 v0.5 构建门禁与Player探针审查

审查者 `/root/v05_spec_audit`，2026-10-30。当前为构建前审查；此文件不代表已成功构建、打包或推送。

## 检查器与来源绑定

PrototypeBuild两个Profile共用同一入口，旧Windows菜单已转入该入口。入口校验当前目标平台、配置完整性、固定552 EditMode/24 PlayMode门禁、Assets/Packages/ProjectSettings整个可构建文件集合及每项SHA，排除Unity不导入的`~`目录。新增/删除可构建文件也使集合比较失败。BuildIdentity自身在文件集合内，sourceCommit与gate相同，配置和黄金SHA必须等于实际文件。构建用StrictMode且记录实际BuildReport；失败不能由“构建方法被调用过”转写为成功。

tools/v05_delivery.py验证原生NUnit根结果、叶子数、唯一名、每叶状态、模式和零失败/跳过/未定，再绑定独立DLL/PDB报告。审查中发现它原先仅比较报告里的旧mismatches与DLL/PDB哈希，没有重新检查报告后的源文件变化；这可能在修改已提交但Unity尚未重编时绑定新源与旧报告。root已经补充逐PDB文档与当前源码的SHA1/SHA256重算。该问题以独立负例复核关闭。

审查者直接调用实际生产脚本函数，**12/12检查通过**，见 `delivery-gate-independent-tests.json` / `.log`。包括真实原生XML正向读取，以及根失败、数量不一致、跳过、坏叶子、重复名、错误模式、缺测试、空PDB文档、源checksum失配、DLL变化、PDB身份不符的拒绝检查。变异输入位于 `gate-negative-fixtures/*.fixture.*`，它们是故意错误的检查器输入，不是Unity实测报告。审查者没有覆盖正式gate文件或修改生产检查逻辑。

上述脚本是已独立确认测试证据的冻结工具，不是报告真伪签名服务，也不能自行证明测试发生在每次未来修改之后。当前真实测试与变化范围已在UNITY_TEST_REVIEW中复核；未来变更仍应按同样流程判断影响面。正式gate生成、实际Unity门禁正向/变更拒绝、BuildReport和最终包仍需后续证据。

## 最新编译与已有测试影响面

`unity-prebuild-pdb01.json`再次独立核对5程序集、47项目源文档、DLL-CodeView与PDB GUID：0差异。与PlayMode02时的PDB逐源比较记录在 `post-playmode02-source-delta.json`：只有Editor/PrototypeBuild.cs门禁强化和Runtime/Verification/PlayerVerification.cs opt-in验收探针改变。Core、存档、帧调度、玩家UI及全部EditMode/PlayMode测试源保持一致。因此保留EditMode02/PlayMode02合理；新的探针行为由随后真实Player验证，不把旧PlayMode当作新探针通过。

## 跨平台checkpoint探针

只读检查ProbeRevision7的新增逻辑：仅`--verify-p0`时创建专用目录；从正常第三关或以后首次ConfirmLocks的真实磁盘schema3文件导出checkpoint，独立session执行冻结Submit得到预期；本地重载只为先检验导出自洽。`--verify-import-save`将外平台文件复制到本次隔离目录，校验输入规则hash、接受Submit和分数、结果规则/RNG hash、实际写盘重载、重复commandId幂等及原输入文件SHA不变。规范hash仅剔除run/command身份和历史回执列表，保留所有当前规则字段与三条RNG；重复执行的回执行为另有明确检查。

Mac执行脚本已经传入Windows checkpoint，比较器要求双方实际导出且Mac导入passed。Windows重新导入Mac导出的checkpoint必须另有实际执行结果，不能以同平台导出自测冒充双向兼容。

正常trace目前明确以第9关RunFailed结束，失败路径是合法实测结果。单独标记的Debug24检查24关/8风险/23商店/最终胜利；它不证明正常构筑能通关、玩家胜率或难度适宜。压力夹具维持10骰、8栏、普通4件+免槽6件、5道具，180秒/16.7ms阈值未因本次改动放宽。
