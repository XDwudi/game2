# AI 游戏美术工具与技能研究

研究日期：2026-11-08。项目输入：HEAD `246646b0e4fb87595727b0fbf48559f1947230ba`；[v0.71 美术规格](../specs/v071/ART_SPEC.md)、[实际素材映射](../specs/v071/ASSET_MAP.md)、[质量门禁](../QUALITY_GATES.md)及研究时 [STATE](../STATE.md) 顶部记录。结论是工具选择建议，不修改美术方向、预算或验收标准。在线页面是当日读取结果，不能当作固定版本的已安装软件。

## 本项目应采用什么

推荐当前组合：**内置 imagegen 负责参考图引导的概念与主题插画；Krita 负责需要的图层整理与局部修图；现有 Blender 5.2.3 母版与脚本负责精确 3D；Unity 共用组件、ArtStyleLab 和真实 Player 负责最终表现验证。** 风格统一的中心仍是用户 v0.71 母版、已确认样件与真实游戏截图。新增项目技能应把这条链连起来，不另造一套美术方向。

项目已经有这条路线的基础：[首批插画 manifest](../../ArtSource/Art071/Illustrations/manifest.json)保留原图、提示词、参考图与 SHA；[骰子回导报告](../../ArtSource/Art071/Dice/Review/roundtrip.json)记录 Blender 5.2.3、300 三角骰体及六面映射。STATE 记录四张新插画已接入、67 项仍为旧图标，完整版本与人工风格验收未完成。生成更多图片暂不能解决剩余的验收缺口。

当前最有价值的改进是：每个资源任务携带同一组视觉输入；只改变主体；每批 4—6 张前先完成一个代表资源；将原图、编辑源、导出和 Unity 引用关联；同时检查单图、同类并排图与游戏内缩小后的实际效果。这里的数量来自当前规格，不是通用 AI 产量要求。

## 已核对的技能与工具

| 候选 | 实际核对与适用方式 | 本轮取舍及能力边界 |
| --- | --- | --- |
| OpenAI 内置 `imagegen` | 官方技能区分内置生成与需要 API key 的 CLI；支持参考图与编辑工作流。[技能](https://github.com/openai/skills/blob/main/skills/.system/imagegen/SKILL.md) | **复用现有能力。** 当前会话确实暴露 `image_gen.imagegen`；本子任务未发起生成。精确 image2 能力以本次主任务官方核对及实际调用证据为准。技能文件本身不会安装后端。 |
| Unity 官方 skills | 官方建议 Codex 使用 Unity plugin；可服务 uGUI、TMP、SpriteAtlas 与 Editor 工作流。[来源](https://github.com/Unity-Technologies/skills) | **按需引用已提供技能。** 不另装同功能社区集合；实际 Editor 连通性仍须任务开始时验证，不能由技能目录推定。v0.71 是高清印刷插画，不应误触发整屏 pixel-perfect 改造。 |
| `create-game-assets` | 已阅读真实 SKILL，覆盖视觉方向、资源族、图像 QA、游戏内检查及溯源。[来源](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/create-game-assets/SKILL.md) | **借鉴方法，暂不复制执行代码或全包安装。** 项目已有更严格的人工标杆权与独立验收；该通用技能的像素色数限制、nearest-neighbor 预览示例不应照搬给 v0.71。其全仓 Unity 基线为 6.3，而项目为 6.6。[目录与版本](https://github.com/gamedev-skills/awesome-gamedev-agent-skills) |
| Krita AI Diffusion | 可在选区内生成、局部重绘，以参考、线稿、深度等输入控制结果；可连接本地或云端后端。[仓库](https://github.com/Acly/krita-ai-diffusion) | **下一阶段首选补充。** 适合批量插画经常出现构图漂移、边缘或局部对象反复返工时。当前只核对文档，未证明插件、模型或服务已接通。原生 Krita 源文件补验也不能由插件安装替代。 |
| ComfyUI + ControlNet / LoRA | ControlNet用线稿、边缘、深度等约束结构；LoRA是附加到兼容基础模型的风格/主体调整。[ControlNet](https://docs.comfy.org/tutorials/controlnet/controlnet)、[LoRA](https://github.com/Comfy-Org/workflow_templates/blob/main/site/knowledge/concepts/lora.md) | **需求触发后小试。** 固定工作流、模型 SHA、节点版本与参数有助复现。仅锁 seed 不保证跨模型/硬件逐像素重现，也不保证风格。项目标杆尚待人工验收，不宜马上训练风格 LoRA。 |
| Comfy 官方 MCP / Skills | 官方 MCP 支持云端和本地工作流；技能内容与连接能力分别提供。[MCP](https://docs.comfy.org/agent-tools/mcp)、[Skills](https://docs.comfy.org/agent-tools/skills) | **未来自动化候选，未接通。** 当前 `comfy-skills` 主包面向 Claude Code；旧 `skills/` 已冻结；CLI 专属 skills 随 `comfy-cli` 发布。不能把 Claude 插件安装命令直接宣称为 Codex 集成成功。[仓库现状](https://github.com/Comfy-Org/comfy-skills) |
| Blender Python / 现有生成脚本 | 项目已用 `tools/art_v071/build_dice.py` 与 `.blend` 母版保存精确几何及面序。 | **继续使用。** 骰子几何简单且规则强约束，确定性建模比图生 3D 更合适；概念图只约束外观。现有引擎与导出版本保持锁定，不能因研究新工具顺手升级。 |
| MCP for Blender | 社区工程已改名为 `ahujasid/mcp-for-blender`，可以控制 Blender，明确不是 Blender Foundation 官方产品。[来源](https://github.com/ahujasid/mcp-for-blender) | **可选，不是当前必需。** 需要频繁交互调材质/灯光且现有脚本不方便时再评估。先验证与 Blender 5.2.3 的小场景往返、日志及可撤回操作，再写正式源。 |
| Meshy 图生 3D | 官方 API 提供图生模型、贴图及重网格相关参数。[文档](https://docs.meshy.ai/en/api/image-to-3d) | **当前延后。** 更适合将来复杂装饰物的初稿；不应替换现有骰体、精确六面点数或生产碰撞。需 Blender 清理拓扑/UV/枢轴/比例与 Unity 验证。未核账户、额度或发起付费调用。 |

## 怎样把先进能力用在质量上

**先锁图像角色，再生成。** 同一次请求明确区分整体风格母版、单件插画样件、待编辑图与构图草图。当前项目要求深青毛毡、奶油纸、深墨轮廓、限色大块面、左上主光。参考图不提供规则数据；名称、价格、牌型与骰点仍由实际配置和引擎生成。新主体要从已有内容 ID 取主题，不能以生成结果扩展玩法。

**先使用参考与局部编辑，再增加模型训练。** Krita 控制层将参考风格与结构约束分开；其文档明确 Style/Composition 的分离并非总能清晰生效。因此评估时应让同一主体分别经过“现有 imagegen 参考编辑”和“Krita + 一种结构控制”的流程，比实际缩略图、返工量与可编辑程度，不能只比较大图精细度。[控制层文档](https://docs.interstice.cloud/control-layers/)

**三维先提供精确结构。** 将来需要复杂 2D 视角统一时，可以用已审 Blender 构图输出深度/法线作为结构控制参考；Krita 文档支持这种来源。当前骰子仍沿用已有模型和运行时点阵，不为装饰插画生成的新骰子反向改规则。将来图生 3D 资产也是草稿输入，渲染漂亮不能证明面序、UV 或运行成本正确。[控制层文档](https://docs.interstice.cloud/control-layers/)

**评审区分可自动判定与艺术判断。** 文件哈希、尺寸、alpha 极值、裁切、命名、导入设置、漏项和资源引用可以自动检查；色板角色、轮廓风格、质感、主体是否易懂及成套一致性需要看实际图。抗锯齿、光照和网点会引入大量不同像素色，不适合用“只有五种 RGB 值”当本项目硬门槛。发现任何坏点不能用总分平均抵消。AI 视觉评分可用于定位差异，不替代用户美术标杆验收。

**按用途保留不同资产形态。** 生成原图保留原字节；需要分层修图时保留真实 `.kra` 层，而非给扁平 PNG 换后缀；九宫格和点阵继续使用精确源。导出后检查纸色与深底上的透明边缘、正确尺寸与画面留白，再在真实小尺寸下看线条和网点。自动工具输出报告只能作为证据的一部分。

## 额外工具需要什么条件

| 下一步候选 | 开始前需要核实 | 最小验证及是否扩大 |
| --- | --- | --- |
| Krita AI Diffusion | 已安装 Krita 精确版本、兼容插件版本、可用 GPU/显存或云额度、允许使用的模型及参考素材。官方目前区分 Krita 5.x/1.x 插件与 6.x/2.x；本地服务还需额外模型磁盘空间。[安装文档](https://docs.interstice.cloud/installation/) | 使用一个已知插画的副本做局部编辑，保存/重开真实 `.kra`，导出 PNG 并导入实验场景；比较主体完整性、边缘与风格，再决定采用。不要直接用正式 67 项做工具接通试验。 |
| Comfy 工作流与官方 MCP | 本地/云路线、凭据、模型及节点兼容版本、工作流保存与输出下载方式。当前会话工具清单未出现 Comfy 接口，这不等于机器未安装 ComfyUI。 | 一个固定工作流完成一次生成、重载和再次执行，保存 JSON、版本、参数、输入/输出 SHA；证明可追溯后再自动化批次。 |
| 风格 LoRA | 有权使用且经人工认可的训练集；固定基础模型、训练配置、评估留出集与算力预算。 | 用未进入训练的主题验证风格和新主体能力，检查是否记住旧主体；改善有证据后才推广。当前没有足够证据支持立即训练。 |
| Blender MCP | 精确版本、连接路径、第三方依赖和遥测设置；与现有 Unity/Blender单写者锁协调。 | 在副本中修改一种材质并保存/重开/导出，再核 Unity 外观。现有脚本已能完成的工作优先复用脚本。 |
| Meshy / 其他图生 3D 服务 | 适用任务、账户套餐、输入权利、输出条款、费用与目标预算。 | 先用独立装饰物而非骰子规则对象，测清理后的最终成本；没有真实优于手工/脚本的证据则不引入。 |

现阶段不需要同时购买图生 3D、搭建训练机和安装多套 MCP。优先补齐现有样板验收与可重复的交付证据；是否增购工具由实际制作瓶颈决定。

## 来源、许可与采用边界

本报告只读取第一方项目文档或原作者仓库，没有下载、安装或执行研究对象的脚本、hooks、安装器，也没有把远端 `SKILL.md` 当作本项目指令。未复制上游源码到 `references/vendor`，因此没有新增上游文件哈希或修改现有 `sources.lock.json`。未来若决定引入原文件，先锁定提交和许可，再纳入来源清单。

| 来源 | 本次可确认的许可层 | 不能据此推定 |
| --- | --- | --- |
| OpenAI imagegen 技能 | 技能目录 [LICENSE.txt](https://github.com/openai/skills/blob/main/skills/.system/imagegen/LICENSE.txt) 为 Apache-2.0；[旧 skills 入口](https://github.com/openai/skills)已声明弃用，转向 [plugins](https://github.com/openai/plugins)。本项目优先当前环境内置技能。 | 技能代码许可证不等于模型权重许可证、服务账户权限或任何输入图/输出图的完整权利证明。 |
| Unity Skills | [Unity Companion License for Unity-dependent projects](https://github.com/Unity-Technologies/skills/blob/main/LICENSE.md)。 | 不是可任意用于所有引擎的 MIT 许可，也不授权外部美术资产。 |
| create-game-assets 所属仓库 | [Apache-2.0，附 NOTICE](https://github.com/gamedev-skills/awesome-gamedev-agent-skills)。 | 不能将其资源/引擎基线或脚本示例未经评估变成本项目标准。 |
| Krita AI Diffusion / ComfyUI | 各仓库声明 [GPL-3.0](https://github.com/Acly/krita-ai-diffusion)、[GPL-3.0](https://github.com/Comfy-Org/ComfyUI)。 | 节点代码、基础模型、ControlNet、LoRA、训练素材及云端条款需要分开记录，不能用工具开源推导所有模型和输出都可自由发布。 |
| Comfy Skills | [MIT](https://github.com/Comfy-Org/comfy-skills)。 | 其云账户、模型与生成输出另受相关条款约束；MIT不代表服务已授权。 |
| MCP for Blender | [MIT](https://github.com/ahujasid/mcp-for-blender)。仓库当前说明默认有匿名用量记录，内容收集需启用；可设置 `DISABLE_TELEMETRY=true` 全部关闭。 | 不能把“内容采集默认关闭”表述为“默认零遥测”。采用前还需审核连接的资产服务条款。 |
| Meshy | [官方当前套餐文档](https://docs.meshy.ai/en/webapp/pricing)区分免费产物的 CC BY 4.0 与付费套餐的私有/商业使用安排。 | 本项目未确认套餐，不预填许可；投入制作时重新核条款与实际生成账户，记录当时凭证和署名要求。 |

美术源中的引用素材、模型权重、训练数据与最终导出分别登记。仅有“AI 生成”标签不足以说明输入来源。报告中的许可证信息是选型记录，不能替代具体资产的来源核查。

## 研究验证状态

- 已执行：读取项目上述权威文件与首批插画、骰子源记录；搜索并打开表中第一方网页；查看当前暴露工具清单。
- 未执行：安装研究工具、验证 Comfy/Krita AI/Meshy 账户、调用研究服务、训练模型、修改 Unity/Blender、替换任何生产资源。
- 有限结论：工具组合与延后理由有来源依据；真实质量提升尚需对应最小试验。当前项目已知实现/验收状态以 STATE 为准，旧 manifest 的历史 `pending` 字段不能覆盖后续实际集成记录。
