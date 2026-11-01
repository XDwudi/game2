# 最终冻结与构建准备度预检

2026-11-01，审查者 `/root/v07_shop_ui`；独立窄范围子审查 `/root/v07_shop_ui/delivery_static_audit`。输入为 [P0-V07-ART 契约](<E:/agent_workspace/gpt/game2/docs/tasks/P0-V07-ART.md>)：art0.7.0；玩法 p0.6.0 / 存档 schema4；Windows x64、macOS Universal；Unity 6000.6.7f1。此文是**只读准备度审查，不是冻结完成、测试通过、构建通过或整版验收声明**。

**当前不能直接完成 identity → gate → build → package。** 05:13:11 UTC 的观察仍有 716 项项目路径变更、旧 p0.6 身份、无 v07 gate/双平台产物，root 正在处理 POST 视觉缺陷。交付脚本静态入口可用；须按下列顺序满足真实前置条件。未运行 Unity、Player、测试、打包或网络，未改 production/tests/gates/标准；只做文件读取、Git 只读查询、Python AST 与 PowerShell 语法解析。 部分交付工具由本审查者早期实施，本次另委派未实施这些脚本的子代理作窄范围静态复核；不以本报告自签工具或整版验收。

## 观察绑定与保留边界

[observation.json](<E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-freeze-audit/observation.json>) 记录具体路径、SHA、当前 Git/发现/原始报告情况；[delivery-static.md](<E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-freeze-audit/delivery-static.md>) 记录两个交付脚本的逐行审查。观察发生在 root 继续制作期间，后续必须重新盘点，不能直接拿本文件当最终源快照。

| 观察项 | 实际结果 | 冻结含义 |
|---|---|---|
| 分支 / HEAD | `codex/v07-art-and-ui` / `1fbc3cce9d84237fa98b31d4b73b08e6f5e909ae` | 尚未形成 v07 源提交 |
| Git index | 暂存为空；审计前后 SHA 均 `99d3706b25ba802f7bd38b598479a75414f012d2dae8c25624c6739ba195f7ab` | 本审查没有暂存或改变 index |
| 项目变化 | Assets / Packages / ProjectSettings 共 716 路径；当前可构建文件 1,291 个（含下面临时场景及生成身份） | 变更涵盖 UI、表现、3D桌面、验证器、测试、字体、场景、资源与设置；未作逐行全量行为审查 |
| 规则边界 | Core、P0Config、GoldenScores 对冻结源 `08ef933` 的 Git diff 为空 | 本次仍为美术/表现迭代；这项不是新 Player 规则通过证明 |
| 实际设置差异 | SpritePackerMode 0→5；EnterPlayModeOptions 1→0；bundleVersion 0.6.0→0.7.0 | 当前恢复正常域重载；这些真实设置应随源冻结，不在最后测试之后再改 |
| 最大文件 | 观察到的非旧发行变更/新增文件没有超过 50 MiB | 没有据 Git 大文件限制删减资源的理由 |
| 最后发现文件 | EditMode 996 个唯一名称；PlayMode 121 个唯一名称 | 是当前文件内容，**非最终数量**，最终编译后必须重新发现 |
| 历史完整套件 | 最大 EditMode 全通过为 022445 的 996；完整 Play 023325 为 92/74/18 | 都早于后续修改，不能以专项报告拼成一份最终完整全通过 |
| 最新动态观察 | 051213 为 0 项 Passed；051310 为 17 项、16 过 1 败 | 0 项不能算通过；此时 root 仍在返工，报告原件保持 |
| 目标环境 | ProjectVersion 6000.6.7f1；WindowsStandaloneSupport / MacStandaloneSupport 模块目录存在 | 只证明文件存在；不是新构建成功 |

七个旧 ZIP 仍在原 `Releases/p0.5.0/`、`Releases/p0.6.0/review-candidate/`（含 superseded-kit-doc01）路径，**全部未跟踪、未暂存，合计 806,548,011 B**；本次逐 SHA 与 `v07-build-preflight/observed-01/preflight.json` 完全一致。没有移动、重写或删除。不得 `git add .`；保留原 LFS attributes 不等于把这些 ZIP 提交进 Git。最终仍按已授权 GitHub Release 三附件渠道交付。

`.gitignore` 已增加 `artifacts/tools/`，`artifacts/cache/` 本已忽略；旧交接中“工具缓存没有 ignore”已过时。`.gitattributes` 已增加 `tools/macos/v07/*.command text eol=lf`，当前 VerifyMac.command 无 CRLF；旧交接中的此项建议已落实。**现有 candidate-stage 生成器不会自动纳入 `.gitignore` / `.gitattributes`**，它们被归为 owner review，root 应单独审查并显式加入本次源提交。

## 前置缺口与运行注意事项

| ID | 状态与精确定位 | 必须执行的处理 |
|---|---|---|
| FR01 | 尚未冻结。identity 为 p0.6.0 / 08ef933 且没有 artVersion；`build-gate.json`、v07 EXE、v07 app、Releases/p0.7.0 均不存在 | 完成 POST 和范围内剩余制作/复核后形成真正源提交 S，再生成 p0.7.0/art0.7.0 身份。AudioMixer 仍是 STATE 中已记录的未完项；本审查不把用户“稍后处理”改写成标准豁免。 |
| FR02 | `Assets/InitTestSceneb751d372-e17f-423b-a044-438d36d638c3.unity` + `.meta` 仍在实际构建范围；Temp.meta、Assets/Screenshots 本次没有残留 | 可能是运行中的 TestRunner 场景。**等待测试结束且 Editor idle**，由 root 记录/备份后按既有 AssetDatabase 流程处理。不要在活跃测试中删。最终全套可能再次生成临时场景，结束后再盘点。 |
| FR03 | `TestEvidence.cs:41` 排除 InitTestScene / Screenshots；`v07_delivery.source_paths` 和 `PrototypeBuild.ValidateGate` / `package_v07.source_paths` 纳入实际全部非 `~` 文件 | Git ignore 不足以完成清理。identity 的 git status 看不到 ignored 残留，gate 可把它们纳入；package 最终 exact Git tree 对比仍会拒绝。必须在创建 gate 前消除生成残留，不能改标准排除它们。 |
| FR04 | `preflight.py:82–94,173–195` 的 `files` 只列 changed / untracked，`buildScopeFiles` 只有总数；返回 0 仅检查 auditErrors / 不稳定读取，并不要求 blockers 为空 | 可以直接作审计工具，不能把 exit 0 或 `candidate-stage.nul` 当发布放行。提交后已干净文件不在这份变更清单，不能用它冒充全源录制快照；最终媒体应绑定完整 gate.files、身份和 loaded MVID。 |
| FR05 | `record-v07-screens.ps1:27–32` 要求 idle + 无路径且未改动的 scratch scene；当前正常 Bootstrap 场景不符合；脚本不替用户场景做隐式替换 | root 保存/核对 Bootstrap 后主动准备空 scratch；用新批次名。实际 Screen.width/height 仍由 recorder 严格验证；外窗尺寸设置本身不能当分辨率证据。 |
| FR06 | `record-v07-screens.ps1:28–57` 没有 finally；超时或捕获失败抛出后可能仍在 Play；`Art07ScreenRecorder.Finish/OnDestroy` 才负责恢复偏好 | 属于运行器恢复注意事项，不应无人看守后直接继续下一步。失败时保留输出，root 调用当前 recorder.Cancel()，确认 prefs/report 后退出 Play；等待 idle 再继续。未建议弱化错误处理或篡改失败报告。 |
| FR07 | 截图脚本只存 recorder 自身 SHA / Runtime+Core MVID，未抓整套源码；也不会验证 22 屏/67项全部通过 | 最终截图前后核对完整 gate 文件 SHA，保存实际 loaded MVID 与 gate PDB/MVID一致的记录；检查各屏 coverage/未覆盖清单并独立看图。脚本的 ALL_PRESETS_CAPTURED 只代表完成预置路线。 |
| FR08 | 五程序集 PDB 审计工具会跳过不存在 DLL，也不以 mismatches 设置非零退出码 | 不单凭 dotnet exit 0。后续 gate 会强制 exact 五程序集、非零文档、全 C# 集合、checksum、DLL/PDB匹配、两份测试起止实际程序集一致。当前 C#133 只是观察数，不作为最终门槛。 |

未发现 `tools/v07_delivery.py` / `tools/package_v07.py` 必须修改才能使用的静态代码缺陷。相关 Python 文件 AST 解析成功，截图 PowerShell 解析 0 错误；这些不是实际执行通过。

## root 可执行顺序

以下都是**未执行的操作模板**。每步完成后核对实际结果再进入下一步，保留旧失败。不要并行启动同一工程的 Editor，不在测试或录制期间改 Assets。

### 1. 完成修复与整理源

完成 POST 修复的实际复验与独立复核；按当前契约处理剩余制作缺口。保存 Bootstrap、完成字体/资源最后导入并确认无编译/导入/测试/录制进行。等待测试现场释放后，备份并清理生成 InitTest/Screenshots 残留；不删用户文件或旧 ZIP。

用全新输出名重跑现成只读预检；`--compare-protected` 保留七 ZIP 的原哈希基准：

```powershell
$v07Python = 'C:/Users/f/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $v07Python artifacts/cache/v07-build-preflight/preflight.py --output final-before-freeze-<新时间> --compare-protected observed-01/preflight.json
```

明确阅读 `auditErrors`、`changedDuringRead`、`blockersForFinalBuild`、`excluded-and-review.tsv`。此阶段 identity/gate 尚缺属于顺序中预期，不能跳过其他源残留。审查新的 candidate-stage.nul，补审 `.gitignore` / `.gitattributes` 后显式暂存/提交 S；不用旧 observed-final 清单漏掉新文件，不用 `git add .`。验证 project scope Git clean，七 ZIP 仍原路径/哈希且未暂存。

### 2. 身份、完整发现与完整两套

```powershell
& $v07Python tools/v07_delivery.py identity
```

派生 `Assets/Resources/BuildIdentity.json` 留在既有 ignore 中；不能为避免自指而填假提交。保持 HEAD=S 至 gate 生成完成；此时不要先提交本轮新证据推进 HEAD。实际身份应为 p0.7.0 / art0.7.0 / p0.6.0 / schema4 / 6000.6.7f1 / Mono，并绑定 Config/Golden 原 SHA。让唯一 Editor 完成导入。

root 在已接通 live Editor 调用 `DiceCraft.Editor.TestEvidence.DiscoverAll()`，等待异步 Edit/Play 两文件均更新。随后在同一冻结源与同一实际编译程序集下串行跑**完整、不带名字筛选**的 EditMode 和 PlayMode。可使用现有 TestRunner 入口，等效公开 API 为：

```csharp
var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
    new UnityEditor.TestTools.TestRunner.Api.Filter {
        testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode
    }));
```

待真实完整 Edit 回调结束，再另一次把 mode 改为 PlayMode。GPU/EndOfFrame 测试需要真实渲染 GameView；不要改成无图形执行再把缺失采样当通过。保留 callback 写出的 `nunit-时间.xml` 和同名 `.binding.json`；精确名称集合、总计和全部叶结果必须与发现清单一致，失败/跳过/零项均不通过。过程中若生产、测试、资源或程序集改变，应分析影响并重冻/重验，不能拼专项报告。

### 3. PDB → gate，先于切换 Mac

测试结束且 Editor idle 后再处理 TestRunner 生成残留，确认实际 project scope 与冻结树一致。PDB 输出必须是新路径；已存在检查器来源在 preflight 目录，其源码与既有 v06 原件哈希一致，本次没有重新执行它：

```powershell
$v07PdbOutput = 'E:/agent_workspace/gpt/game2/evidence/v07/review/pdb-final-<新时间>.json'
& 'C:/Program Files/dotnet/dotnet.exe' run --no-build --project artifacts/cache/v07-build-preflight/PdbAudit/PdbAudit.csproj -- 'E:/agent_workspace/gpt/game2' $v07PdbOutput
$v07EditCount = (Get-Content evidence/v07/unity/discovery-EditMode.json -Raw | ConvertFrom-Json).names.Count
$v07PlayCount = (Get-Content evidence/v07/unity/discovery-PlayMode.json -Raw | ConvertFrom-Json).names.Count
& $v07Python tools/v07_delivery.py gate --edit 'evidence/v07/unity/nunit-<最终Edit>.xml' --play 'evidence/v07/unity/nunit-<最终Play>.xml' --pdb 'evidence/v07/review/pdb-final-<同上时间>.json' --expect-edit-count $v07EditCount --expect-play-count $v07PlayCount
```

133、996、121 均不硬编码。gate 强制两次测试起止 source 与当前一致、exact 五程序集 loaded MVID/DLL SHA 与 PDB 审计相等、每个源 checksum 匹配。797/30 只是历史最低下限。生成后保留 gate、两份报告/binding、发现清单、PDB 原字节，不在切 Mac 后用新编译的 DLL 覆盖原证明。

### 4. 最终连续录像与六配置截图

最终媒体要有录制前完整源绑定。建议 gate 完成后、仍处于原 Windows 测试程序集会话时，复制/记录 gate 的完整文件清单与身份 SHA，读取实际 Runtime/Core MVID；与最终 PDB/两套测试一致后录制。录制后再次核对清单、MVID、状态/存档/偏好恢复；源码改动则这批最终证据失效。preflight 的变更列表本身不覆盖提交后全部源文件。

root 在确认当前源场景已保存、无活动测试/录制后，主动创建无路径的空 scratch scene，再执行：

```powershell
& artifacts/cache/record-v07-screens.ps1 -BatchName 'v07-final-screens-<新时间>'
```

六预置为 1920×1080 / 1280×720+150% / 1366×768 / 2560×1440 / 2560×1600 / 3440×1440；其他预置字体100%。100/125/150%全组合专项仍依赖相应真实用例，不能将这六图路线说成所有字号组合。每预置输出独立 save sibling，录制器严格要求真实尺寸、空隔离 Session、输出新目录、普通存档未污染。成功仅为 captured_pending_visual_review，需实际看图及记录未覆盖分支；POST/Instant 连续录像另外使用既有 Art07VisualRecorder，不能与屏录同时启动。

如果脚本失败仍在 Play：保留整批目录；通过唯一 Editor 显式取消 recorder 并退出 Play，等待 idle，核对恢复字段。不要紧接着开始构建或复用同一批次名。

### 5. 串行 Windows → Mac 构建

`PrototypeBuild.BuildActiveProfile()` 只接受 `Windows_x64.asset` 或 `macOS_Universal.asset`，且真实 activeBuildTarget 必须相符；它调用 StrictMode 和完整 gate 检查。两个 profile 都是 Development=1/Mono，Mac Architecture=2（Universal），Windows CopyPDBFiles=0；保留现设置，不在最终测试后改变配置以追求性能通过。

执行前关闭前一工程 Editor 写者并确认退出；不能在 live 测试 Editor 还持有工程时另开 batch build。可用已安装 Unity CLI 的自定义构建入口，传入真实 active profile：

```powershell
$v07Cli = 'C:/Users/f/AppData/Local/Unity/bin/unity.exe'
& $v07Cli build 'E:/agent_workspace/gpt/game2/DicePrototype' --editor-version 6000.6.7f1 --target StandaloneWindows64 --execute-method DiceCraft.Editor.PrototypeBuild.BuildActiveProfile --args '-activeBuildProfile Assets/Settings/BuildProfiles/Windows_x64.asset' --allow-dirty-build --log-file 'E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-build-windows-<新时间>.log' --format json
# 确认前一个进程退出、Windows真实报告成功，再运行Mac。
& $v07Cli build 'E:/agent_workspace/gpt/game2/DicePrototype' --editor-version 6000.6.7f1 --target StandaloneOSX --execute-method DiceCraft.Editor.PrototypeBuild.BuildActiveProfile --args '-activeBuildProfile Assets/Settings/BuildProfiles/macOS_Universal.asset' --allow-dirty-build --log-file 'E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-build-macos-<新时间>.log' --format json
```

这些是根据本地 CLI 技能文档的未执行模板。`--allow-dirty-build` 仅允许仓库外 scope 的旧未跟踪 ZIP/新证据留在工作树，**前提是 project scope 已 clean 且冻结 gate 完整有效**；不绕过 `PrototypeBuild.ValidateGate`，不允许生产脏源。若 root 采用已接通的原生 Editor 启动方案，也必须相同 target、active profile 与 BuildActiveProfile 入口，不能换成只按 `--profile` 的内建构建而绕开本项目 gate。

构建结果以实际 `evidence/v07/unity/build-windows-*.json` / `build-macos-*.json`、Succeeded/errors0、正确 profile/target/Unity、真实 GUID 与相同 identity 为准，不只看 CLI 退出码。输出固定 `Builds/v07/Windows/DicePrototype.exe` / `Builds/v07/macOS/DicePrototype.app`。两次切平台后核对冻结文件未变化；若 profile/ProjectSettings 被改，先诊断，不能只更新 gate 哈希。

Mac 切换可能改变 Library 中 live DLL/PDB；现有 package 特意检查 gate 中**原已冻结的 PDB/测试身份**，不要求此时 live Mac 编译程序集等于旧 Windows 测试程序集。这是正确的顺序设计，不应因此重写已有证明。实际 Mac 所有 Mach-O 必须 Universal，不能只看 profile 数字。

### 6. Player、三包、发行

在 Windows 用 `artifacts/cache/v07-player-execution-plan/run_windows_player.py` 的现有串行方案，填写真实 build report/GUID 和新的 evidence-root；只有显式 `--execute` 才启动。完整默认流程保留真实 native显示、正常/debug24轨迹、独立存档、UI、180秒/P95≤16.7ms、独立writer/reader恢复。诊断或受限开关只能对应真实失败/限制，不能预选来避开标准。Mac M5 仍由用户实际运行，不能把交叉构建当 Mac 通过。

严格完整 Windows 结果满足之后：

```powershell
& $v07Python tools/package_v07.py windows --build-report '<本次真实Windows报告>'
& $v07Python tools/package_v07.py macos --build-report '<本次真实Mac报告>'
& $v07Python tools/package_v07.py kit --windows-results '<本次真实Windows结果目录>'
& $v07Python tools/release_v07_manifest.py --windows-results '<同一目录>' --windows-build '<同一Windows报告>' --macos-build '<同一Mac报告>' --display-recovery '<真实writer-reader共享证据目录>'
```

两个平台 ZIP → M5 kit → manifest；输出只到新 p0.7.0，拒绝覆盖既有文件。该 manifest 仍是 Mac/人工待验候选，不是 ACCEPTED。受限/诊断参数及额外 windows-results 要求详见静态子报告；没有真实失败输入时不得使用。后续按既有 `publish_v07_release.py` draft/upload/download/inspect/publish 路线上传三个附件，逐个远程完整下载核对 SHA/大小/CRC，并从新解压 Windows 实际运行；不要让脚本的远端字节验证替代新目录运行。最终给用户真实链接。

## 交接结论

交付脚本可作为后续执行入口，无需为当前准备度额外改 production/gate；眼下阻塞是尚未完成制作/冻结/全量证明及产物，而不是已有专项通过数不够大。根协调者继续处理 POST 与已记录的其余缺口，随后执行上列流程。七 ZIP 保留、InitTest 清理时机、候选暂存漏项、截图恢复与全源绑定、先 PDB/gate 再切 Mac 是本轮需要明确带入最终执行的注意点。

根协调者可把本报告作为准备度状态加入 STATE；不得写成全版通过。本审查释放所有读审查槽，不持有 Unity 或生产文件锁。
