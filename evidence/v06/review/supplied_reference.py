"""P0 v0.6 standalone canonical data and reference arithmetic; not a Unity build."""
from collections import Counter
from fractions import Fraction
from copy import deepcopy

CONTROLLER_ROWS = [('C01', '存钱罐', 4, 'common', 'SubmitAndStageSettlement', '每次有效提交计分后获得1金币；每10金币额外增加1金币关末利息，计息本金仍最多50。'),
 ('C02', '蓝色疯狂', 5, 'rare', 'ScoreMultiply', '非空提交没有有效红面时，整手得分×4。'),
 ('C03', '首当其冲', 2, 'common', 'EffectiveColor', '第一颗提交骰的有效颜色视为红；仅本次计分，不永久改面。'),
 ('C05', '再来三次', 5, 'rare', 'AfterRoll', '有效投掷后剩掷为0且剩1至2颗未锁存活骰时，剩掷+3；每手限1次。'),
 ('C06', '数字多多', 4, 'uncommon', 'ScoreMultiply', '提交的有效点数至少有3种时，整手得分×3。'),
 ('C07', '骰子复制', 3, 'uncommon', 'AfterScore', '本关第一次有效提交恰好2颗且有效点数不同，提交后将第二颗永久数据深拷贝为第一颗。'),
 ('C08', '少数派', 3, 'uncommon', 'ScoreMultiply', '提交数量小于有效提交容量K时，整手得分×2。'),
 ('C09', '简化提交', 6, 'rare', 'CapacityAndScore', '提交容量−1；每次有效提交整手得分×4。'),
 ('C10', '少就是多', 3, 'common', 'PipContribution', '每颗有效1点的提交骰，点数贡献+10，不改牌型。'),
 ('C11', '2233', 4, 'uncommon', 'ScoreMultiply', '提交中至少2颗有效2点及2颗有效3点时，整手得分×6；允许额外骰。'),
 ('C12', '不要奇数', 6, 'rare', 'EffectivePip', '当前奇数+1成为有效偶数；影响牌型和后续数字条件，不永久改面。'),
 ('C13', '就要奇数', 2, 'common', 'PipContribution', '每颗有效奇数的提交骰，点数贡献+3，不改牌型。'),
 ('C14', '只要奇数', 4, 'uncommon', 'ScoreMultiply', '非空提交的有效点数全为奇数时，整手得分×3。'),
 ('C15', '红色扩散', 3, 'uncommon', 'AfterScore', '扫描整个有序提交列；每个有效蓝面若左右相邻面均为有效红，则计分后将该物理面染红，全部目标从同一快照同时确定。'),
 ('C17', '小鬼当家', 3, 'uncommon', 'PipContribution', '每颗有效1、2、3点的提交骰，点数贡献+6。'),
 ('C18', '红蓝相当', 3, 'uncommon', 'ScoreMultiply', '非空提交中有效红面数等于有效蓝面数时，整手得分×3。'),
 ('C19', '小头大用', 2, 'common', 'PipContribution', '某提交骰永久六面原值之和≤13时，该骰点数贡献+5。'),
 ('C20', '66大顺', 2, 'common', 'PipContribution', '占满K栏且第K栏有效点数为6时，该骰点数贡献+6，参与逐骰强化及后续全部倍率。'),
 ('C21', '增加提交', 3, 'uncommon', 'Capacity', '提交栏容量+1；不增加骰子数量或提交次数。'),
 ('C22', '大量提交', 5, 'rare', 'Capacity', '提交栏容量+2；可与C21叠加；不增加骰子数量。'),
 ('C24', '我是状元', 3, 'uncommon', 'RedGrouping', '仅计算红组时，红色有效1按4分组，与其他红4合组；其他计分不变。'),
 ('C25', '红运当头', 6, 'rare', 'RedGrouping', '仅计算红组时，所有有效红骰合成同一组；不把蓝骰转红、不改牌型。'),
 ('C26', '红色主义', 3, 'uncommon', 'AfterScore', '恰好提交1颗骰时，提交后将该骰6个物理面永久染红。'),
 ('C27', '对穿肠', 3, 'common', 'BaseAdd', '每个出现至少2颗的不同有效点数组，使整手基础分+10；四条只算一组，双三条算两组。'),
 ('C28', '对对穿肠', 4, 'uncommon', 'ScoreMultiply', '至少两个不同有效点数各出现2次时，整手得分×4；四个相同不能拆两对。'),
 ('C29', '简单顺子', 4, 'uncommon', 'HandRule', '顺子最低连续不同点数由5改为4；保持顺子当前等级与倍率。'),
 ('C30', '三条开泰', 5, 'rare', 'ScoreMultiply', '提交中任一有效点数至少3颗时，整手得分×4。'),
 ('C31', '四平八稳', 5, 'rare', 'ScoreMultiply', '提交中任一有效点数至少4颗时，整手得分×4；与三条开泰可叠乘。'),
 ('C32', '天地大同', 4, 'uncommon', 'ScoreMultiply', '非空提交只有一种有效点数时，整手得分×3；单骰也满足。'),
 ('C33', '大数开路', 6, 'rare', 'ScoreMultiply', '至少提交2颗，第一颗有效点数≥第二颗有效点数+2时，整手得分×7。'),
 ('C34', '大数致胜', 3, 'uncommon', 'RollProbability', '永久4至6点组的总概率最多增加15个百分点；从1至3点组按比例转移。'),
 ('C35', '小数出奇', 3, 'uncommon', 'RollProbability', '永久1至3点组的总概率最多增加15个百分点；从4至6点组按比例转移。'),
 ('C36', '不断成长', 3, 'uncommon', 'AfterScore', '至少提交2颗且S.Count<K，提交后前两颗本次物理面原值+1，上限6。'),
 ('C37', '缩小射线', 3, 'uncommon', 'AfterScore', '至少提交2颗且S.Count<K，提交后前两颗本次物理面原值−1，下限1。'),
 ('C38', '蓝色扩散', 2, 'common', 'AfterScore', '每次有效提交后，首颗与末颗提交骰本次物理面永久染蓝；单骰目标去重，其他面不变。'),
 ('C39', '蓝色价值', 3, 'common', 'PipContribution', '每颗有效蓝色提交骰，点数贡献+6。'),
 ('C40', '不断增加', 4, 'uncommon', 'ScoreMultiply', '至少提交3颗，前三颗有效点数形成公差大于0的等差数列时，整手得分×3。'),
 ('C41', '百变模仿', 6, 'rare', 'Proxy', '复制关卡开始时右邻绑定目标的完整基础技能，不复制目标附魔、身份或既有卖价；动态数值共享，模仿自身附魔独立。'),
 ('C42', '玻璃大炮', 5, 'rare', 'ScoreAndAfterSubmit', '每次有效提交整手×10；计分与后置效果后，该效果来源实体独立15%概率永久损坏并消失。'),
 ('C43', '牌型提升', 5, 'rare', 'AfterScore', '本次与上一有效提交的结构主牌型相同且非散点时，该牌型在本次计分后+1级；每个来源各+1，正常上限8，散点不升级。'),
 ('C44', '有钱真好', 6, 'rare', 'BaseAdd', '每1金币令整手基础分+2，读取本次提交冻结时的钱包；本次提交新增金币不回算本手。'),
 ('C45', '飘忽不定', 4, 'uncommon', 'PipMultiplyAndAfterSubmit', '当前加成颜色的每颗骰，其逐骰基础贡献×3；初次获得随机红或蓝，每次有效提交后真实目标重新等概率抽取下一颜色，可与原色相同。'),
 ('C46', '稳稳乘数', 5, 'rare', 'ScoreMultiply', '每次有效提交整手得分×3。'),
 ('C47', '控制欲', 6, 'rare', 'ScoreMultiply', '整手得分乘以提交快照中实际持有控制器实例数量；包括自身及免槽卡，不把模仿技能另算一件。'),
 ('C48', '有也不交', 3, 'common', 'BaseAdd', '每空一个提交栏位，整手基础分+10；空栏数为K减去实际提交数。'),
 ('C49', '千年老二', 3, 'uncommon', 'ScoreMultiply', '整手得分乘以有效5点提交骰的数量；没有5时为×0，仍是一次有效提交。'),
 ('C50', '稳稳分数', 3, 'common', 'BaseAdd', '每次有效提交整手基础分+20。'),
 ('C51', '人员流动', 4, 'uncommon', 'ScoreAndAfterSale', '整手得分×n；真实实例n初始1.5，每次真实卖出一件控制器后，仍持有的真实C51各增加0.5；自毁不是卖出。'),
 ('C52', '成长基金', 4, 'uncommon', 'StageFinalize', '基础购买价固定4，初始卖价1；每次成功关卡Finalize时卖价+3，仅一次；不是每手加3，作为固定卖1规则的明确例外。'),
 ('C53', '错乱乘积', 4, 'uncommon', 'RandomScoreMultiply', '提交事务中从1、2、3、4、5、6、7、8、0.5等概率抽取整手倍率；每个真实C53抽一次，其模仿者复用同一结果。')]

CONSUMABLE_ROWS = [('I01', '红色浪潮', 2, 'Selecting', 'ThisHand', '下一次有效提交全部骰子临时视为红色；本手继续投掷不失效。'),
 ('I02', '临时骰子', 2, 'HandReady', 'ThisStage', '本关增加1颗标准临时骰；不增加提交栏，临时同时最多3、活动总数最多10。'),
 ('I03', '遥控骰子', 3, 'HandReadyOrSelecting', 'ThisRoll', '恰好2颗不同未锁骰指定任意1至6点；同时消耗道具和1次普通投掷，全部未锁骰照常掷。'),
 ('I04', '大数多来', 1, 'HandReadyOrSelecting', 'ThisHand', '本手后续投掷中，永久6点面相对权重×1.5，再归一化；同手同名不叠加。'),
 ('I05', '小数多来', 1, 'HandReadyOrSelecting', 'ThisHand', '本手后续投掷中，永久1点面相对权重×1.5，再归一化；同手同名不叠加。'),
 ('I06', '增加提交', 2, 'HandReadyOrSelecting', 'ThisStage', '本关剩余提交次数+1；结束本关时尚未使用的次数正常计入奖励。'),
 ('I07', '增加投掷', 2, 'HandReadyOrSelecting', 'ThisStage', '本关每手初始投掷次数+1，当前手剩掷立即+1；不重置C05。')]

SERVICE_ROWS = [('O01', '道具增加', 2, '2+2×此前购买次数，最多3次', '道具槽从2增加到最多5；受折扣，最低1。'),
 ('O02', '道具打折', 3, '3−已有折扣层数，最低1', '本Run局内道具与局外服务购买价−1，最多3层；不影响控制器、附魔、刷新、骰强及研习。'),
 ('O03', '加入骰子', 3, '候选骰3至6，折后最低1', '每店预生成2颗候选，购买只增加永久骰，正常最多8；不增加K。'),
 ('O04', '删除骰子', 1, '1', '删除一颗永久骰，不能删除最后一颗，K不变。'),
 ('O05', '增加控制', 3, '3+2×此前购买次数，最多3次', '每店控制器报价数量永久+1，从3到最多6；当前店只生成新位置的一件报价，不补其他已售商品。'),
 ('O06', '增加道具', 2, '2+2×此前购买次数，最多3次', '每店局内道具报价数量永久+1，从2到最多5；当前店只新增一件报价，不增加背包槽。'),
 ('O07', '免费刷新', 6, '6，整个Run仅可购买1次', '每店第一次控制器刷新免费，仍消耗第0次刷新序号；之后价格5、7、9；本店已刷新过则从下一店开始享受。')]

UPGRADE_ROWS = [('U01', '光滑骰面', 1, 'Face', '目标面纹理设为光滑：对面相对权重×0.80，其余5面各×1.04，最后归一化。'),
 ('U02', '粗糙骰面', 1, 'Face', '目标面纹理设为粗糙：对面相对权重×1.20，其余5面各×0.96，最后归一化。'),
 ('U03', '骰子染红', 1, 'Face', '目标物理面永久红色，保持其他属性。'),
 ('U04', '骰子染蓝', 1, 'Face', '目标物理面永久蓝色，保持其他属性。'),
 ('U05', '骰子脆化', 1, 'Die', '整骰玻璃，提交时该骰贡献×2；每次实际投掷先独立判定15%永久损坏。'),
 ('U06', '黄金电镀', 2, 'Face', '目标面增加金层；该面朝上且提交时，该骰贡献×2，红蓝底色不变。'),
 ('U07', '随机变形', 1, 'Face', '物理面原值等概率随机变为1至6中另外5种之一，颜色及其他属性不变。')]

HAND_ROWS = [('pair', '一对', 200, 75, 2, 10, 2),
 ('two_pair', '两对', 300, 100, 2, 20, 4),
 ('trips', '三条', 400, 125, 2, 30, 3),
 ('full_house', '三条带一对', 600, 200, 2, 50, 5),
 ('three_pair', '三对子', 700, 225, 3, 60, 6),
 ('quads', '四条', 800, 250, 2, 70, 4),
 ('two_trips', '双三条', 1000, 300, 3, 80, 6),
 ('five_kind', '豹子 五个相同', 1200, 400, 2, 90, 5),
 ('four_pair', '四对子', 1200, 400, 4, 100, 8),
 ('six_kind', '六豹子', 1800, 600, 3, 110, 6),
 ('seven_kind', '七豹子', 2700, 900, 4, 120, 7),
 ('eight_kind', '八豹子', 4000, 1200, 5, 130, 8),
 ('straight', '顺子', 600, 200, 2, 40, 5)]

COLOR_ROWS = [('all_blue', '全蓝', 300, 50, 2), ('all_red', '全红', 200, 50, 2)]

REMOVED_IDS = ['C04', 'C16', 'C23']

RED = [100, 100, 175, 250, 350, 500, 700, 1000, 1400]

TARGETS = [180,
 400,
 900,
 1800,
 3500,
 6500,
 11000,
 18000,
 28000,
 42000,
 62000,
 88000,
 125000,
 175000,
 240000,
 320000,
 420000,
 540000,
 690000,
 870000,
 1080000,
 1320000,
 1560000,
 1800000]

OPPOSITES = {'F01': 'F06', 'F06': 'F01', 'F02': 'F05', 'F05': 'F02', 'F03': 'F04', 'F04': 'F03'}

RARITY_WEIGHT = {'common': 6, 'uncommon': 3, 'rare': 1}

NON_SUBMIT = ['C05', 'C21', 'C22', 'C34', 'C35', 'C52']

ENCHANTS = [('none', '无附魔', 50, 0),
 ('slotless', '不占栏位', 18, 3),
 ('base10', '基础分增加', 10, 2),
 ('mult2', '倍率增加', 4, 5),
 ('submit1', '提交增加', 9, 3),
 ('roll1', '投掷增加', 9, 2)]



"""Pure v0.6 reference. Random outcomes are explicit inputs, never drawn in preview."""

VALID_IDS={r[0] for r in CONTROLLER_ROWS}
VALID_ENCHANTS={r[0] for r in ENCHANTS}

def normalize_instances(controllers):
    out=[]
    for i,c in enumerate(controllers):
        x={'definitionId':c} if isinstance(c,str) else deepcopy(c)
        x.setdefault('instanceId',f'ctl_{i+1:02d}')
        x.setdefault('enchantmentId','none');x.setdefault('state',{})
        x.setdefault('alive',True)
        assert x['definitionId'] in VALID_IDS and x['enchantmentId'] in VALID_ENCHANTS
        out.append(x)
    assert len({x['instanceId'] for x in out})==len(out)
    return out

def resolve_sources(controllers,bindings=None):
    """bindings None means Shop/Preview adjacency. A stage supplies immutable instance-ID bindings."""
    items=normalize_instances(controllers);by_id={x['instanceId']:x for x in items}
    if bindings is None:
        bindings={x['instanceId']:(items[i+1]['instanceId'] if i+1<len(items) else None)
                  for i,x in enumerate(items) if x['definitionId']=='C41'}
    result=[]
    for i,emitter in enumerate(items):
        if not emitter['alive']:continue
        target=emitter;seen=set()
        while target and target['definitionId']=='C41':
            assert target['instanceId'] not in seen, 'Cyclic mimic binding'
            seen.add(target['instanceId'])
            target=by_id.get(bindings.get(target['instanceId']))
            if target is not None and not target['alive']:target=None
        result.append(dict(emitterId=emitter['instanceId'],emitterDefinitionId=emitter['definitionId'],
            abilityId=target['definitionId'] if target else None,
            stateOwnerId=target['instanceId'] if target else None,
            state=deepcopy(target['state']) if target else {},
            enchantmentId=emitter['enchantmentId'],orderIndex=i))
    return result

def inventory_valid(controllers,debug=False):
    items=[x for x in normalize_instances(controllers) if x['alive']]
    if debug:return len(items)<=32
    return (len(items)<=10 and len({x['definitionId'] for x in items})==len(items)
            and sum(x['enchantmentId']!='slotless' for x in items)<=4
            and all(x['enchantmentId'] in enchant_pool(x['definitionId']) for x in items))

def effective_capacity(controllers,bindings=None):
    c=Counter(x['abilityId'] for x in resolve_sources(controllers,bindings))
    return max(1,min(8,5+c['C21']+2*c['C22']-c['C09']))

def stage_submission_budget(controllers):
    return 3+sum(x['alive'] and x['enchantmentId']=='submit1' for x in normalize_instances(controllers))

def hand_roll_budget(controllers,stage_item_bonus=0):
    return 3+stage_item_bonus+sum(x['alive'] and x['enchantmentId']=='roll1' for x in normalize_instances(controllers))

def evaluate(dice,rolls_remaining=0,controllers=(),levels=None,color_levels=None,capacity=5,red_wave=False,
             submission_ordinal=0,wallet=0,previous_primary=None,random_units2=None,bindings=None,debug=False):
    """Complete score and frozen after-score plan. Input does not consume actions or RNG.
    dice: current p,c plus optional glass/gold/sum6. color_levels is separate from levels.
    random_units2 maps each real C53 instanceId to one of [2,4,6,8,10,12,14,16,1].
    Controller state fields read here: activeColor (C45), turnoverUnits2 (C51).
    Runtime invalid-input, inventory legality and transaction ownership checks are external.
    """
    maxn=32 if debug else 8;maxlevel=100 if debug else 8
    assert 1<=len(dice)<=capacity<=maxn and rolls_remaining>=0 and wallet>=0
    assert all(1<=x['p']<=6 and x['c'] in ('red','blue') for x in dice)
    levels=levels or {};color_levels=color_levels or {};random_units2=random_units2 or {}
    assert set(levels)<={r[0] for r in HAND_ROWS} and set(color_levels)<={r[0] for r in COLOR_ROWS}
    assert all(0<=v<=maxlevel for v in list(levels.values())+list(color_levels.values()))
    items=normalize_instances(controllers);sources=resolve_sources(items,bindings)
    count=Counter(s['abilityId'] for s in sources)
    d=[dict(x,e=x['p']+(x['p']%2 if count['C12'] else 0)) for x in dice]
    if count['C03']:d[0]['c']='red'
    if red_wave:
        for x in d:x['c']='red'
    n=len(d);numbers=Counter(x['e'] for x in d);cc=sorted(numbers.values(),reverse=True)
    mx=cc[0];pairs=sum(v>=2 for v in numbers.values());trip_groups=sum(v>=3 for v in numbers.values())
    reds=sum(x['c']=='red' for x in d)
    run4=any(all(v+j in numbers for j in range(4)) for v in numbers)
    run5=any(all(v+j in numbers for j in range(5)) for v in numbers)
    straight=run4 if count['C29'] else run5
    matches={'pair':mx>=2,'two_pair':pairs>=2,'trips':mx>=3,
      'full_house':len(cc)>1 and cc[0]>=3 and cc[1]>=2,'three_pair':pairs>=3,'quads':mx>=4,
      'two_trips':trip_groups>=2,'four_pair':pairs>=4,'five_kind':mx>=5,'six_kind':mx>=6,
      'seven_kind':mx>=7,'eight_kind':mx>=8}
    vals={id:b+s*levels.get(id,0) for id,na,b,s,p,r,m in HAND_ROWS}
    ranks={id:r for id,na,b,s,p,r,m in HAND_ROWS}
    candidates=[id for id,v in matches.items() if v]
    chosen=max(candidates,key=lambda id:(vals[id],ranks[id])) if candidates else None
    group=vals[chosen] if chosen else 0;straight_mult=vals['straight'] if straight else 0
    hand=max(100,group+straight_mult)
    structural=candidates+(['straight'] if straight else [])
    primary=max(structural,key=lambda id:ranks[id]) if structural else 'scatter'
    color='all_red' if reds==n else 'all_blue' if reds==0 else None
    colorvals={id:b+s*color_levels.get(id,0) for id,na,b,s,p in COLOR_ROWS}
    color_mult=colorvals[color] if color else 100
    redtargets=[i for i in range(1,n-1) if d[i]['c']=='blue' and d[i-1]['c']=='red' and d[i+1]['c']=='red']
    is_sequence=n>=3 and d[1]['e']>d[0]['e'] and d[2]['e']-d[1]['e']==d[1]['e']-d[0]['e']
    predicates={
      'C01':True,'C02':reds==0,'C03':True,'C05':False,'C06':len(numbers)>=3,
      'C07':submission_ordinal==0 and n==2 and d[0]['e']!=d[1]['e'],'C08':n<capacity,'C09':True,
      'C10':numbers[1]>0,'C11':numbers[2]>=2 and numbers[3]>=2,'C12':any(x['p']%2 for x in dice),
      'C13':any(x['e']%2 for x in d),'C14':all(x['e']%2 for x in d),'C15':bool(redtargets),
      'C17':any(x['e']<=3 for x in d),'C18':reds==n-reds,'C19':any(x.get('sum6',21)<=13 for x in d),
      'C20':n==capacity and d[-1]['e']==6,'C21':False,'C22':False,
      'C24':any(x['c']=='red' and x['e']==1 for x in d),'C25':reds>0,'C26':n==1,
      'C27':pairs>0,'C28':pairs>=2,'C29':run4,'C30':mx>=3,'C31':mx>=4,'C32':len(numbers)==1,
      'C33':n>=2 and d[0]['e']>=d[1]['e']+2,'C34':False,'C35':False,
      'C36':n>=2 and n<capacity,'C37':n>=2 and n<capacity,'C38':True,'C39':reds<n,'C40':is_sequence,
      'C42':True,'C43':primary!='scatter' and primary==previous_primary,'C44':wallet>0,
      'C45':True,'C46':True,'C47':True,'C48':n<capacity,'C49':True,'C50':True,'C51':True,
      'C52':False,'C53':True}
    triggered=[s for s in sources if predicates.get(s['abilityId'],False)]
    active=Counter(s['abilityId'] for s in triggered)
    per_die=[]
    for i,x in enumerate(d):
        e=x['e'];v=e
        if e==1:v+=10*count['C10']
        if e%2:v+=3*count['C13']
        if e<=3:v+=6*count['C17']
        if x.get('sum6',21)<=13:v+=5*count['C19']
        if active['C20'] and i==n-1:v+=6*active['C20']
        if x['c']=='blue':v+=6*count['C39']
        for s in sources:
            if s['abilityId']=='C45':
                assert s['state'].get('activeColor') in ('red','blue'), 'C45 needs stored activeColor'
                if s['state']['activeColor']==x['c']:v*=3
        if x.get('glass'):v*=2
        if x.get('gold'):v*=2
        per_die.append(v)
    redgroups=Counter((0 if count['C25'] else 4 if count['C24'] and x['e']==1 else x['e'])
                      for x in d if x['c']=='red')
    k=max(redgroups.values(),default=0);rv=max((v for v,z in redgroups.items() if z==k),default=0)
    red_mult=RED[min(k,8)]
    multipliers={'C02':4,'C06':3,'C08':2,'C09':4,'C11':6,'C14':3,'C18':3,'C28':4,
                 'C30':4,'C31':4,'C32':3,'C33':7,'C40':3,'C42':10,'C46':3}
    x=Fraction(1);factor_lines=[]
    for s in triggered:
        id=s['abilityId'];m=None
        if id in multipliers:m=Fraction(multipliers[id])
        elif id=='C47':m=Fraction(len(sources))
        elif id=='C49':m=Fraction(numbers[5])
        elif id=='C51':
            units=s['state'].get('turnoverUnits2',3);assert units>=3
            m=Fraction(units,2)
        elif id=='C53':
            units=random_units2.get(s['stateOwnerId']);assert units in [2,4,6,8,10,12,14,16,1], 'Missing C53 committed outcome'
            m=Fraction(units,2)
        if m is not None:
            x*=m;factor_lines.append(dict(emitterId=s['emitterId'],abilityId=id,numerator=str(m.numerator),denominator=str(m.denominator)))
    ench_base=sum(s['enchantmentId']=='base10' for s in triggered)
    ench_mult=sum(s['enchantmentId']=='mult2' for s in triggered)
    pair_add=10*pairs*active['C27'];wallet_add=2*wallet*active['C44']
    empty_add=10*(capacity-n)*active['C48'];steady_add=20*active['C50']
    p=sum(per_die);unused=3*rolls_remaining
    b=p+unused+pair_add+wallet_add+empty_add+steady_add+10*ench_base
    numerator=b*hand*color_mult*red_mult*x.numerator*2**ench_mult
    denominator=1000000*x.denominator
    upgrade_request=active['C43'];upgrade_actual=min(upgrade_request,maxlevel-levels.get(primary,0)) if upgrade_request else 0
    owners=lambda ability: [z['instanceId'] for z in items if z['alive'] and z['definitionId']==ability]
    return dict(effectivePips=[z['e'] for z in d],effectiveColors=[z['c'] for z in d],pipContributions=per_die,pipSum=p,
        unusedRollBonus=unused,pairKinds=pairs,pairBaseAdd=pair_add,walletBaseAdd=str(wallet_add),emptySlotBaseAdd=empty_add,
        steadyBaseAdd=steady_add,enchantBaseAdd=10*ench_base,baseTotal=str(b),matchedGroupIds=candidates,chosenGroupId=chosen,
        primaryHandId=primary,groupMult100=group,straightMult100=straight_mult,totalHandMult100=hand,
        colorId=color,colorMult100=color_mult,redGroupingMode='all_red' if count['C25'] else 'one_as_four' if count['C24'] else 'same_value',
        redValue=rv,redCount=k,redRewardCapped=bool(k>8),redMult100=red_mult,
        controllerMultiplierNumerator=str(x.numerator),controllerMultiplierDenominator=str(x.denominator),
        triggeredSourceIds=[s['emitterId'] for s in triggered],triggeredAbilityIds=[s['abilityId'] for s in triggered],
        factorLines=factor_lines,enchantBaseCount=ench_base,enchantMultiplierCount=ench_mult,
        scoreNumerator=str(numerator),scoreDenominator=str(denominator),score=str(numerator//denominator),
        afterScorePlan=dict(earnedGold=active['C01'],copyFirstToSecond=bool(active['C07']),
          redSpreadIndices=redtargets if active['C15'] else [],dyeAllFacesRed=bool(active['C26']),
          growthNetDelta=active['C36']-active['C37'],growthIndices=[0,1] if active['C36'] or active['C37'] else [],
          blueSpreadIndices=sorted({0,n-1}) if active['C38'] else [],
          upgradeHandId=primary if upgrade_request else None,upgradeRequested=upgrade_request,upgradeApplied=upgrade_actual,
          nextPreviousPrimary=primary,nextColorOwnerIds=owners('C45'),randomFactorOwnerIds=owners('C53'),
          cannonRiskEmitterIds=[s['emitterId'] for s in sources if s['abilityId']=='C42']))

def raw_probabilities(faces,buffs=()):
    raw=[]
    for face in faces:
        id,pip,texture=face[:3];w=face[3] if len(face)>3 else 1
        assert w>0
        for tid,tp,ttex,*rest in faces:
            if ttex=='smooth':w*=80 if OPPOSITES[tid]==id else 104
            elif ttex=='rough':w*=120 if OPPOSITES[tid]==id else 96
        for b in sorted(buffs):w*=150 if pip==b else 100
        raw.append(w)
    total=sum(raw);return [Fraction(w,total) for w in raw]

def probs(faces,buffs=(),high_count=0,low_count=0):
    assert len(faces)==6 and high_count>=0 and low_count>=0
    ps=raw_probabilities(faces,buffs);delta=high_count-low_count
    if delta:
        high=delta>0;target=[i for i,f in enumerate(faces) if (f[1]>=4)==high];donor=[i for i in range(6) if i not in target]
        if target and donor:
            mass=sum(ps[i] for i in donor);transfer=min(Fraction(15*abs(delta),100),mass)
            out=list(ps)
            for i in donor:out[i]-=transfer*ps[i]/mass
            vals=sorted({faces[i][1] for i in target})
            for pip in vals:
                ids=[i for i in target if faces[i][1]==pip];bucket=sum(ps[i] for i in ids)
                for i in ids:out[i]+=transfer/len(vals)*ps[i]/bucket
            ps=out
    scaled=[p*1000000 for p in ps];units=[v.numerator//v.denominator for v in scaled]
    order=sorted(range(6),key=lambda i:(-(scaled[i]-units[i]),faces[i][0]))
    for i in order[:1000000-sum(units)]:units[i]+=1
    return units

def die_offer_price(pips,colors):
    return min(6,3+(sum(pips)>=24)+(sum(pips)<=13)+(max(Counter(pips).values())>=3)+(max(Counter(colors).values())>=5))

def enchant_pool(controller_id,owned_enchant=None):
    assert controller_id in VALID_IDS
    weights=({'none':100,'slotless':50,'submit1':25,'roll1':25} if controller_id in NON_SUBMIT
             else {id:w for id,n,w,p in ENCHANTS})
    if owned_enchant is None:return weights
    return {k:v for k,v in weights.items() if k!='none' and k!=owned_enchant}

def normalize_new_enchant(controller_id,raw_enchant):
    assert raw_enchant in enchant_pool(controller_id), 'Invalid ID-specific enchantment'
    return raw_enchant

def controller_sell_price(instance):
    x=normalize_instances([instance])[0]
    accrual=x['state'].get('fundAccrual',0) if x['definitionId'] in ('C41','C52') else 0
    assert accrual>=0 and accrual%3==0
    return 1+accrual

def refresh_price(refresh_count,free_first=False):
    assert refresh_count>=0
    return 0 if free_first and refresh_count==0 else 3+2*refresh_count

def settlement_preview(gold,remaining_submissions,controllers=(),risk=False,bindings=None):
    assert gold>=0 and remaining_submissions>=0
    sources=resolve_sources(controllers,bindings)
    pigs=sum(s['abilityId']=='C01' for s in sources)
    interest=(1+pigs)*(min(gold,50)//10);reward=8 if risk else 5
    fund_emitters=[s['emitterId'] for s in sources if s['abilityId']=='C52']
    return dict(interest=interest,baseReward=reward,unusedSubmissionGold=remaining_submissions,
                totalGold=gold+interest+reward+remaining_submissions,fundAccrualEmitterIds=fund_emitters,fundAccrualPerEmitter=3)

def c05_roll_grants(controllers,remaining_after_roll,unlocked_survivors,used_emitter_ids=(),bindings=None):
    if remaining_after_roll!=0 or not 1<=unlocked_survivors<=2:return []
    return [s['emitterId'] for s in resolve_sources(controllers,bindings)
            if s['abilityId']=='C05' and s['emitterId'] not in used_emitter_ids]

