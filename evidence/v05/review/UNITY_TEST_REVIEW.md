# P0 v0.5 原生Unity测试独立复核

审查者：`/root/v05_spec_audit`；2026-10-30。本审查者未启动或操作Unity，独立读取root实际运行后的原始NUnit、CLI结果、受测源和已编译DLL/PDB。以下结论不是再执行一份模拟器测试。

**EditMode02 552/552、PlayMode02 24/24实际通过，0失败、0跳过、0未定。** `unity-edit02-play02-report-audit.json`逐叶子核对两个载体的唯一名称、状态及根总计；所有检查相符。EditMode为2026-10-30 04:57:19–04:57:24 UTC，PlayMode为05:06:17–05:06:35 UTC。原始报告为 `../unity/nunit-20261030-045724.xml` 和 `../unity/nunit-20261030-050635.xml`，对应CLI `editmode-02.json` / `playmode-02.json`。不能将Unity报告的asserts=0元数据误读为无测试；有552/24个实际叶子，源测试内的NUnit断言亦经阅读。

## 源码与运行身份

`unity-edit02-play02-pdb.json`独立读取5个程序集的PE元数据和Portable PDB：DLL CodeView与PDB GUID全部匹配，47个项目源码文档checksum与当前工作树一致，0差异。Core 15文档、Runtime 10、EditMode 11、PlayMode 7、Editor 4。运行身份记录包括DLL/PDB SHA、MVID、最后编译时间，不能只凭文件名说明是当前实现。

Core和EditMode程序集编译时间分别为04:54:07、04:57:01 UTC，早于EditMode02；对应源码仍与独立managed run06绑定一致。Runtime随后为UI/Player修改而重编译，受EditMode覆盖的AtomicSaveStore源最后修改04:55:59、FramePacer未改，PDB当前checksum与这两份既有源一致。11个EditMode测试源及其Core依赖亦未变。因此保留EditMode02结果合理；没有用当前Runtime重新编译时间冒充它早于旧测试。

PlayMode在最后相关Runtime/PlayMode程序集编译之后全量重跑。后续仅Editor门禁工具变更时，不需要为了提高次数而重复游戏测试；任何相关游戏源变更都应沿影响面重新验证。整个当前未提交工作树仍需最终sourceCommit/文件清单冻结，原始基线36025ff不是本轮构建源版本。

## 失败修复没有削减验收

- EditMode01保留550项中19失败：包括Unity把null嵌套历史还原为全默认对象，以及不合法强制颜色夹具。EditMode02包含全部旧550项和两个新增损坏历史测试；旧19项全部仍在且通过。AtomicSaveStore只在无Submit回执且历史恰等于新默认记录时归一化null，其余历史仍严格重算校验。
- 当前14个AtomicSaveStore用例运行真实Unity JsonUtility和文件IO，覆盖实际物理结果/随机流/回执恢复、有效校验和但内容错误的backup回退、旧schema2两个文件隔离保留、组ID null/空串恢复等。已读取测试TearDown保留的实际文件索引并记录哈希，见 `native-persistence-retained-evidence.json`。
- PlayMode01的24项中10失败在PlayMode02全部保留并通过。只读diff确认修复包括合法实例serial/ID/报价、主动EndStage、新骰上限8且不扩栏；教程75分显式断言 `(19+6)×2×1.5`，不是读到任意新结果后直接接受。历史公式展示单空格修正不改变数值要求。
- 4项新增PlayMode实际覆盖手册20次开关状态/存档/RNG不变和真实键盘模态阻断、达标继续/放弃未提交手/唯一奖励、容量/简单顺子/强化/重载后手册刷新、附魔替换净价与实例恢复。它们不能替代Player三分辨率实际阅读检查。

## 字体和构建准备

初次setup报告missingGlyphs异常且静态表缺少“共互令尾斥符”。该现象单独不能证明动态字体运行时显示缺字，但值得在打包前闭环。root将V05Setup采集范围扩到Core展示文本，先查询实际缺字后补齐，再以HasCharacter复查；原生setup现在报告空missingGlyphs。审查者重新解析字体字符表，对配置、GameApp、UI、Core文本的汉字覆盖检查为零缺失，见 `font-character-table-final.json`；初始报告保留为 `font-character-table-initial.json`。

两个实际Build Profile资产为Windows x64和macOS Universal，固定同一Bootstrap，Mono后端，Mac Metal。静态Profile内容与设置可核对，但尚不等于已有成功包或原生Mac运行。

当前技术结论为**原生Unity规则/存档/界面自动回归通过**。构建门禁、真实Player、三分钟压力、跨平台存档、Mac M5原生检查、双包LFS上传与干净检出仍待各自证据；本报告不代签平台或发布验收。
