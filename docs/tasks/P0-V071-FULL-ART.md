# v0.71 全量美术与测试发行

状态：REVIEW（全量美术诊断测试候选已交付，人工风格/体验未验，整版未ACCEPTED）。用户最新指令：将全部美术资源按附件要求修改，完成后通过GitHub发行附件提供下载测试。此指令授权沿既定v071方向扩展全部内容、修正公共资源并发布测试候选；覆盖旧任务仅首批4项及等待量产授权的范围限制，不等于人工艺术验收通过。

## 输入与交付

BASE `249d980f2ae3615d573df089403f298a6713ae96`，工作树已有首批交付文档及AI-ART-PIPELINE成果保留。权威输入为 `docs/specs/v071/ART_SPEC.md`、原附件INPUTS、同目录handoff母版和样件；真实内容来自 `DicePrototype/Assets/Game/Data/P0Config.json`。保持Unity6000.6.7f1、Blender5.2.3、p0.6.0/schema4、所有ID/价格/RNG/输入及规则。

交付覆盖全部71个活跃内容（50控制器、7道具、7服务、7强化），A01—A18公共资源、5附魔、状态/类型符号、骰子各强化表现、六档计分和共享触发反馈及所有既有页面。已符合规格的母模型/组件/插画复核后复用，记录理由；其余旧小图标替换为完整主题插画。已删除的C04/C16/C23不复活。规则牌型与骰点保留程序绘制。

保留生成原图、完整提示词、参考/输出SHA、可重跑矢量或脚本源、模型源、引擎运行件及导入配置。生成PNG是栅格原稿，不冒称分层KRA/PSD。通过真实运行制作图册/连续片段。最终交付Windows x64、Mac Universal及Mac验证工具包的GitHub预发布附件，完整远程下载后校验并从新解压目录启动Windows；不覆盖历史发行附件。

## 方法与所有权

- root：任务、共享源/UI与Unity唯一写入者、集成、测试、版本冻结、构建和GitHub发行；新公共确定性资源在 `tools/art_v071/` / `ArtSource/Art071/Full/`。
- art_assets：按活跃ID负责C01/C02/C05/C06与C07—C20（跳过已移除C16），仅 `ArtSource/Art071/IllustrationsFull/<ID>/`，先四张小批；不写生产Assets。
- project_audit：初始全目录只读审计及内容主题清单，后续独立复核；仅 `cache/v071-full/`、分配的证据文件。
- dice_sample：非内容资源差距审计，后续单独授权源路径；不并行写Unity/场景/材质。
- 其余插画批次在审计清单后分配互斥ID，不共享manifest写入。所有图使用同一母版/样件，以单资产内置imagegen调用生成；每4—6项并排复核，明确问题单项修订。无额外API或密钥。

## 准备度与验证

沿用已验证样板及V71-A—E，不新建主观分数门槛。实际已读工程组件、脚本与当前失败证据。Unity CLI连接与图像工具在本轮正式集成前复核，GitHub读取/授权状态先探测。新资源类型先完成代表件的源→实际组件→画面闭环，再扩大。

| ID | 场景、方法与期望 | 证据与责任 |
|---|---|---|
| FULL-A | 逐个71活跃ID与所有公共任务包核对；完整主体/正确语义、透明边缘/比例/导入，无旧图标漏接或已删除ID | 全量清单、原图/源SHA、真实Importer及场景引用；root实施，非作者复核 |
| FULL-B | 4—6件同风格并排、浅纸/深桌面及紧凑/大卡/详情尺寸；无灰底、白边、裁切或明显画法漂移 | 原图及Unity全量图册；独立视觉复核，人工最终风格 |
| FULL-C | A01—A18、附魔/强化、触发/六档计分与所有页面；1080、720/150%、最大正常容量；保持清楚可读及源可编辑 | 实际截图/连续片段与受影响测试；root+独立复核 |
| FULL-D | 真实购买/报价/锁序/详情/触发/跳过/减少动态；规则配置/Golden/schema不变，普通存档隔离 | 新冻结完整Unity回归及Windows回放/180秒压力；原门槛不降 |
| FULL-E | 同源双构建/3包、许可、源绑定；GitHub预发布附件完整远程下载SHA/CRC，新目录Windows启动 | 构建/运行/包证明/远程receipt/公开链接；root发布、独立复核 |

历史性能、原生独占、Mixer、Mac及人工验收缺口保留，依据本轮实际结果更新，不以新素材关闭。若仍有历史平台失败，在诊断测试候选中明确标出；用户本轮已授权下载测试发行，无需再次索取同一发布许可。新增美术回归或关键功能故障必须修复。任务最终只签各自技术/制作范围，不伪造人工ACCEPTED。

## 当前交付证据（2026-11-09 UTC）

实际成品源41aface80bba6c1e008bd3d35344210a28003553。原条件和阈值保持，独立技术结论不代签人工美术/体验验收。

| 条件 | 本轮结果与证据 |
|---|---|
| FULL-A | 71项全部接入；127清单/132导入PNG，Bootstrap旧Art07 PNG依赖0；源、导入及副本SHA已核，ASSET_MAP记录完整路径 |
| FULL-B | Unity全量图册与5种尺寸/底色实例完成，跨作者覆盖全部71项；生成原图/修订与来源保留，人工风格待评 |
| FULL-C | 公共资源、五附魔、强化、六档Combo、页面与720/1080/150%专项完成；13.0013326秒140帧操作片段与最终独立视觉审查在cache/v071-full |
| FULL-D | 完整Edit1018/1018、Play182/182；Windows普通145步/Debug175步、UI18、动画50、0Unity错误，存档守卫与独立恢复通过。180秒P95 16.8078ms、GPU覆盖91.91%，性能FAILED；原生独占确认失败，保留REWORK |
| FULL-E | 同源Windows/Mac Universal构建与三个完整ZIP封装、独立成员/CRC/SHA核验完成；三个GitHub附件完整远端下载SHA/CRC一致，Windows下载包新目录诊断exit0并经独立复核；05:40:34 UTC已公开p0.7.1-rc.1，匿名最终3链接均HTTP200 |

回归、构建/Player、封包独立报告分别位于evidence/v071-full/review/FULL_REGRESSION_41AFACE_REVIEW.md、BUILD_PLAYER_41AFACE_REVIEW.md、PACKAGE_41AFACE_REVIEW.md。历史249d980仅首批包不作为本轮交付。Mac/Metal/签名公证、AudioMixer与人工体验仍未验。

本次两次源码push被自动审批拒绝，采用仅三个具名成品ZIP发行；实际构建源41aface不变，标签锚定已存在远端b11694f8。GitHub自动Source code不对应本版。Mac验证工具包包含执行验证所需脚本与Windows对照数据，不外发完整游戏Git源码及独立开发证据目录。

最终交付：[GitHub p0.7.1-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.1)，发布与公开访问凭据在evidence/v071-full/delivery/github-release/review-candidate；[远端独立复核](../../evidence/v071-full/review/REMOTE_DELIVERY_41AFACE_REVIEW.md)仅签其真实技术范围。全部制作/Unity/资源文件锁释放，源码冻结。下一步为用户下载风格/体验测试、Mac实机验证与既有性能/独占全屏问题返工。
