# v0.6 交付源文件审计

读取时点：2026-10-31T14:27:44.532834+00:00 至 2026-10-31T14:27:52.888511+00:00。只写本 JSON/MD 结果，未启动 Unity/Player、构建、暂存、删除、移动或重写被检查文件。

目标范围有 **2060 个未跟踪文件，共 127,620,949 字节**。另有 1 个已跟踪修改、0 个已暂存路径。完整逐文件路径、字节、SHA 和建议见 [source-delivery-audit.json](source-delivery-audit.json)。

最大文件：`evidence/v06/review/review-vectors.json`，**2,925,384 字节**。不因体积删除历史证据。

## 应入 Git 的具体范围

| 路径 | 建议 |
|---|---|
| `docs/PLAYER_README_V06.txt；docs/tasks/P0-V06-MAC-VERIFICATION.md；docs/tasks/P0-V06-SPEC-AUDIT.md` | 保留操作、验收与规格审计说明。 |
| `docs/DELIVERY_V06.md；docs/STATE.md` | root 填写真实状态后提交；本审计不修改，草稿/待验不标最终通过。 |
| `tools/generate_v06_dtos.py；tools/import_v06_rules.py；tools/v06_session_*；tools/v06_delivery.py` | 保留导入、会话复现与校验源；所有确切文件路径见 JSON。 |
| `tools/package_v06.py；tools/release_v06_manifest.py；tools/publish_v06_release.py；tools/verify_v06_display_recovery.py；tools/macos/v06/* 非缓存文件` | 保留交付工具和测试；Mac.command Git属性为eol=lf，实际0个CRLF。 |
| `evidence/v06/art/、rules/、session/、player-support/、review/ 非缓存文件` | 保留规则/参考输入、审查与全部失败、修复、负例证据。 |
| `evidence/v06/review/gate-run*/、gate-consumer-run*/ 非缓存文件` | C#、JSON、故意非法 XML 为门禁负例输入，连同汇总保留；不是生产树或编译缓存。 |
| `evidence/v06/player/、display-diagnostics/、unity/` | 保留实际源绑定、报告、截图、诊断及所有失败；.backup和.rejected-*也保留。 |
| `artifacts/v06/hash-diagnostic/ 全部9文件` | 保留null/默认DTO存档故障的前后状态与canonical文本因果证据。 |

## 全部七个 csproj 应保留

`!evidence/v06/**/*.csproj` 已让七个手写验证工程可入 Git。已实际解析 XML，全部 Compile/HintPath 输入当前存在；没有项目自定义 Target/Exec 或 PackageReference。本次没有重跑构建。

| 工程 | 复现前置条件 |
|---|---|
| `evidence/v06/review/GateConsumer/GateConsumer.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |
| `evidence/v06/review/PdbAudit/PdbAudit.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |
| `evidence/v06/review/harness/ReviewHarness.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |
| `evidence/v06/review/sequences/SequenceReview.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |
| `evidence/v06/rules/harness/RulesHarness.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |
| `evidence/v06/rules/harness/RulesNUnit.csproj` | .NET SDK10 + 已还原的锁定 Unity com.unity.ext.nunit2.1.0；当前HintPath存在，新clone不能直接假设Library存在。不要提交DLL。 |
| `evidence/v06/session/SessionCompile.csproj` | .NET SDK10 + 仓库相对源码；连同邻近源码和实际存在的NuGet.Config保留，bin/obj忽略。 |

后续完整快照已纳入审查者新建的 sequences/SequenceReview.csproj，因此最终为7个（初步盘点为6个）。

本机只读查询到SDK10.0.403（另有7.0.202）。RulesNUnit带哈希PackageCache路径是显式复现依赖；未来路径变动需解析锁定包位置，不以提交Library DLL/PDB替代依赖说明。

## 留缓存、不得强制加入

- 范围内 **1028 个已忽略派生文件，6,137,558 字节**：宿主bin/obj/.dotnet和Python __pycache__。完整确切路径在JSON。未混入当前候选。
- artifacts/cache/v06 当前 **20 文件，2,729,003 字节**：原始build/player/display日志及已保存的Editor派生截图继续保留缓存，不删除或force-add。
- 真实失败的结论、错误栈、状态输入和截图仍保留在evidence或hash-diagnostic；原始启动日志按现有约定留cache，不因本次特征未命中就提交。

## 凭据特征与快照边界

扫描 1753 个文本文件（候选及原始cache日志），328 个二进制文件未作文本/OCR扫描。凭据特征命中 **12**，原始启动标记 **6**。只记文件/行号/类型；公开游戏调试口令不是服务凭据。

**全部凭据参数标记均在已忽略的 artifacts/cache/v06/build-windows01～06.log；待提交候选为0命中。** 不移动、不删除或重新输出原始参数。

| 文件 | 行 | 类型 |
|---|---:|---|
| `artifacts/cache/v06/build-windows01.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows01.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows01.log` | 63 | `credential_flag` |
| `artifacts/cache/v06/build-windows02.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows02.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows02.log` | 63 | `credential_flag` |
| `artifacts/cache/v06/build-windows03.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows03.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows03.log` | 63 | `credential_flag` |
| `artifacts/cache/v06/build-windows04.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows04.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows04.log` | 63 | `credential_flag` |
| `artifacts/cache/v06/build-windows05.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows05.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows05.log` | 63 | `credential_flag` |
| `artifacts/cache/v06/build-windows06.log` | 49 | `raw_startup_marker` |
| `artifacts/cache/v06/build-windows06.log` | 61 | `credential_flag` |
| `artifacts/cache/v06/build-windows06.log` | 63 | `credential_flag` |

读取期间内容变化 0 文件；新增未跟踪 0 文件；退出候选集合 0 文件。确切路径在JSON。后续原生诊断生成文件不在此快照内；特征无命中不是未知凭据形式的通用保证。

本审计不修改门禁、验收标准、历史失败或交付草稿的待验状态。
