"""Retain all P01-P30 conditions and attach review evidence/limits, without changing gates."""
from pathlib import Path
import json

OUT=Path(__file__).resolve().parent
rows=[r for r in json.loads((OUT/'required-acceptance-rows.json').read_text(encoding='utf8')) if r['id'].startswith('P')]
notes={
'P01':('GameApp/AtomicSaveStore/build identity', '等待最终 Windows ZIP 全新路径实际启动及设备记录'),
'P02':('macOS 构建可作包结构检查', '用户 M5 原生 arm64 实机待验；本机不能代签'),
'P03':('V06Setup Universal 配置已读', '等待 Mach-O 主程序/随附库实际双架构检查；Intel运行单列待验'),
'P04':('p0.6.0/schema4 配置已逐字段对照', '等待最终两包 sourceCommit/config/golden SHA 身份一致'),
'P05':('固定 RNG/160黄金/额外差分已在.NET执行', '跨 Windows/Mac 实际同轨迹复现和存档交换待 M5'),
'P06':('24关/23商店核心流程与终态分支已读', '等待最终 Player debug24 逐步轨迹；与普通Run分开'),
'P07':('EndStage/FinalizeStage幂等与未用预算已审', '等待最终真实 Player 达标继续/结束与奖励证据'),
'P08':('HandbookRules结构排序/可达性；UI牌型颜色分区已读', '最终实际UI14+2行、翻页/滚动和字体确认'),
'P09':('预览纯读取独立测试通过；现有20次手册PlayMode源码已读', '等待最终源的Unity/Player重复读取记录'),
'P10':('容量、C29、级别与颜色供给规则已审', '等待最终动态手册UI回归及截图'),
'P11':('全部合法附魔池逐票、实例与分区审查；5徽章源检查通过', '等待最终实际卡面/购买/替换/读档图像'),
'P12':('骰数与K独立；规则与会话容量校验已审', '等待最终PlayMode/Player普通8永久10活动/K8实测'),
'P13':('71图标/5附魔缩略图视觉检查通过', '最终Player三分辨率10骰8栏10卡5物品无重叠/缺字待验'),
'P14':('模态/焦点路由与取消代码已审', 'Windows真实鼠标/键盘及滚轮；Mac触控板由用户待验'),
'P15':('root契约记录用户指定GitHub Release替代已失败LFS路线', '最终commit全新恢复+3ZIP远程下载大小/SHA/CRC；Mac解压运行待用户'),
'P16':('存档分区/原子文件IO独立7场景通过', '最终Windows3分钟性能/退出续玩；Mac性能和跨平台存档待验'),
'P17':('插入/取消/同序纯事务已审；UI06-08静态修复', '最终实际拖拽跨行、失焦、Esc、resize之后操作；非仅按钮回调'),
'P18':('共享owner/RNG/冻结目标独立回归通过', 'UI06-10/11候选拖动反馈和损坏目标标记待修复复核'),
'P19':('刷新3+2r/免费第0次/只换控制器/保存失败重试已审', '最终Player刷新前后其他货架/幂等与余额回归'),
'P20':('O01/O05/O06独立容量和追加逻辑已审', '最终实际商店新增格/售罄保持与重载回归'),
'P21':('DisplaySettings首次所在屏幕偏好和独立持久化已审', 'Windows原生首次启动/确认后重启；Mac由用户待验'),
'P22':('三模式平台映射和实际读回实现已审', 'Windows每模式真实应用；macOS独立待验'),
'P23':('UI06-01已静态修复，真实读回成功后15秒倒计时', '最终原生超时/Esc/失焦/失败/应用中退出重启回退证据'),
'P24':('安全区坐标、窗口resize重建；UI06-02已静态修复', 'UI06-09目标身份读回待修；缺失多屏/Retina/刘海/缩放实机明确待验'),
'P25':('精确Ordinal密码；普通/调试入口隔离已审', '最终Unity真实Enter焦点回归须通过；Mac相同功能仍待用户'),
'P26':('50卡×6附魔命中/复制差分；12纯卡实验附魔进店回归；32上限已审', '最终界面滚动可达全部卡/道具/骰面/等级，不用程序Click不可见按钮证明可达'),
'P27':('独立runId/源普通检查点不变；SaveSlotPolicy实际IO通过', '最终真实入口/退出/重启回归，普通存档字节保持'),
'P28':('动态状态与回执解释已读；C53共享抽样与C42独立风险已实测', 'UI06-11/12损坏绑定标记/自毁提示与声音待修复；实际恢复后反馈确认'),
'P29':('160原样黄金+1024差分，包括C49×0、单骰颜色、新三牌型', '最终实际计分详情准确显示各乘区和唯一floor'),
'P30':('DebugScene白名单DTO/原子应用/规则随机覆盖先抽后强制已审', '最终导出→载入→同动作重放和随机记录；实际显示设置纯读取')}
out=[]
for row in rows:
    code,remaining=notes[row['id']]
    out.append({'id':row['id'],'specLine':row['line'],'title':row['cells'][1],'originalExpected':row['cells'][2],
                'currentReview':code,'remainingFinalEvidence':remaining,'conclusion':'待最终源与真实运行闭环；非ACCEPTED'})
assert len(out)==30
(OUT/'acceptance-coverage-draft.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
text='# v0.6 P01–P30 独立复核矩阵（未冻结）\n\n原始验收文本完整保留在 JSON；本文件不改变阈值或平台范围。实施期间通过的旧源检查不自动覆盖后续改动，最终证据仍需绑定。\n\n| ID | 已核对的实现/局部证据 | 最终待闭环 |\n|---|---|---|\n'
for r in out:text+='| '+r['id']+' | '+r['currentReview']+' | '+r['remainingFinalEvidence']+' |\n'
(OUT/'ACCEPTANCE_COVERAGE_DRAFT.md').write_text(text,encoding='utf8')
print('P01-P30 retained:',len(out))
