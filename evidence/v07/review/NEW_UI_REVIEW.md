# v0.7 Toast / 骰操作提示真实画面复核

结论：有缺陷；限域视觉观察已完成，不是整版 ACCEPTED。实看32张原图（26 Toast、6骰操作），未修改 Assets、Editor、规则、测试或验收标准。全部为 WindowsEditor / Unity6000.6.7f1 / D3D11 / RTX3080 /1280×720；本次没有1080p画面，也没有Mac结论。

复核者 `/root/v07_art`：Toast由其他实施者完成，本次给独立实图观察；空心勾/抓手的初版由本代理实施，因此只能计实施者实机复查，不能更名为独立最终验收。root补的CanvasRenderer修复已体现在042447实际运行中。

## 实际缺陷

| ID | 规格 / 场景 | 实图与现象 | 结论 / 修复方向 |
|---|---|---|---|
| NUI01 | S10钱包/利息及150%可读性；Shop合法调试金币1000010 | `Toast042240/007-toast-shop-125-normal.png`、`011-toast-shop-150-normal.png`、`019-toast-shop-125-reduced.png`、`023-toast-shop-150-reduced.png`：125%“币”换到下一行，150%“金币”整段换行，压住“每10金币利息+1 / 计息本金上限50”；实际约屏幕x28–190,y134–180。100%两模式未重叠。 | 有缺陷，已通知root。钱包数值/货币标签分栏或合法短格式加精确入口，不以缩小字号或删利息解决；需重新实测两个分辨率三字号。不是Toast文字自身缺陷。 |
| NUI02 | ART_SPEC:346普通操作/资源错误就地解释，只有阻止继续或检查点数据问题用模态 | `ToastPause042240/001-toast-long-error-existing-details.png`：长的“输入不合法”诊断自动成为“操作未完成·完整说明”模态。全文三行清楚、关闭按钮可见，但类型本身是一般输入错误。该长文由UI测试注入，不是本次实玩真实规则错误路径。当前DrawToasts07在任何超过两行文字都直接ShowNoticeDetails07。 | 规格符合性待返工/核对。完整文本可读性已体现；不能据此把普通长错误自动模态判为符合346。应保留就地摘要/明确展开入口，阻止继续或存档问题才自动模态。这里只读报告，未改生产。 |

## Toast逐场景观察

所有下表的 normal/reduced 各100/125/150原PNG均已分别打开实看；不是仅看联系表。路径前缀 `artifacts/cache/v07-new-ui-review/Toast042240/`，编号即原文件编号。

| 场景 | normal编号 | reduced编号 | 实际制作观察 |
|---|---|---|---|
| MaximumPool |000、004、008|012、016、020|纸色珊瑚边、完整1–2行文字可读；100%显示两条，125/150显示一条并保留队列。位于空槽下部，部分空槽“空栏”临时让位；没有盖实际骰、待锁/确认/提交按钮，也未出现截字。|
| MaximumTable |001、005、009|013、017、021|8锁骰+2未锁骰时纸签移到骰池两侧空白。100%一条，125/150两条；长“调序必须保留全部已锁骰子…”两行完整。点阵、六点抓手、槽号、色标/临时/玻璃、动作均可见。|
| Debug32 |002、006、010|014、018、022|一条纸签位于计分面板右上，遮住只读预计总分。未盖六倍率按钮、32入口、分页按钮或实际骰。按root说明列为既知诊断32溢出工程fallback，不能推广为普通对局允许遮总分。|
| Shop |003、007、011|015、019、023|两条纸签底部并排，购买/出售失败全文可读，与LeaveShop保持空隙，刷新/页签/价格未盖。125/150同时暴露钱包换行NUI01，不能按Toast局部通过推断整个Shop通过。|

减少动态截图与普通模式的信息和布局一致；其桌布无高频颗粒，未丢掉纸签或错误全文。截图只能证明这些稳定帧，不单独证明2秒寿命和暂停时钟；后者由原NUnit相应用例给执行证据。

暂停恢复图 `ToastPause042240/000-toast-resumed-after-pause.png`：同一“没有待锁骰子”完整恢复，未盖锁骰与动作。长说明图文字/标题/关闭无重叠，规范边界另见NUI02。

## 空心勾与抓手实施者复查

六图均已原尺寸打开：`Affordance042447/pending-eight-font{100,125,150}.png` 和 `locked-eight-grips-font{100,125,150}.png`。

- 三待选图中，每颗待选骰右侧空心金勾与“01–08 待锁”均能辨认；勾在骰体外，与蓝菱形/红圆色标不混淆，玻璃/临时文字保持可读。金色虚线不穿过可见顶面点数，第二排无上排标签遮挡。
- 100%锁槽为单排八格，六点抓手位于各骰Hit左缘、槽框之外的狭窄空隙内；点阵可见，但约1px的小点较125/150紧凑两排的2px点更细。未盖骰点、序号或颜色标签。此低尺寸易发现性仍应人工试玩观察，不以截图/射线测试代替体验。
- 125/150两排四格中，六点抓手清楚，位于骰体左侧且没有碰邻槽。所有八个锁标、序号、蓝/红与玻璃/临时文字都能读出，提交/重掷保留清楚的主次颜色。
- 截图未覆盖拖拽过程中间帧、退出/失焦120ms和重排140ms全过程；原042447的实际拖放测试仅作对应功能证据，不声称独立动画质量通过。原042240缺CanvasRenderer失败仍保留。

## 证据来源与绑定

- `evidence/v07/unity/nunit-20261101-042240.xml`：整批8项6过2败；本次两个Toast用例均实际通过。Runtime MVID `11613c47-2a59-469e-976e-57b3f8ecad8b`，PlayMode MVID `cb47aa3f-d215-4d1c-9c3e-2aab40a1249f`。不能将本批改写为8/8。
- `evidence/v07/unity/nunit-20261101-042447.xml`：修复后两个目标用例2/2。Runtime MVID `0905cfbe-5333-403f-8339-7b517390169e`，PlayMode MVID `c198f22d-084e-4632-9e02-0b065bb038e9`。
- Toast精确源目录：`C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_f2c8e07bf06e431cbad58629eb966d5e/extra-capture`；暂停/长文在 `DiceCraftTests_2236fcfef0cd4b93b0f7c9353719172c/extra-capture`。
- Affordance精确源目录：`C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_efc9c24482e34a8898bd1a74e0e73ef1/art07-dice-affordance`。
- 以上PNG与JSON已逐字节归档 `artifacts/cache/v07-new-ui-review/`，原始文件保持；全部SHA在 `NEW_UI_REVIEW.bindings.json`。26张Toast的PNG SHA均与各自捕获sidecar一致，state/save before-after也逐项一致。六骰图绑定实际NUnit输出路径与对应source binding，不虚构不存在的捕获sidecar。

下一步：root修复NUI01并确认/修复NUI02；Guide候选另等真实图；1080p与Player仍需验证。docs/STATE由root持有写锁，本报告交root合并状态，不自行改写总验收结论。
