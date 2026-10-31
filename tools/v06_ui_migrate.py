"""One-shot source adaptation from the frozen v0.5 UI; not a build-time dependency."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]/'DicePrototype/Assets/Game'
def edit(path, changes):
    p=root/path; s=p.read_text(encoding='utf-8-sig')
    for old,new in changes:
        if old not in s: raise ValueError(f'{path}: missing {old[:90]}')
        s=s.replace(old,new)
    p.write_text(s,encoding='utf-8')
edit('Runtime/GameApp.cs',[
('Config=JsonUtility.FromJson<GameConfig>(configAsset.text);','Config=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(configAsset.text));'),
('            Refresh();\n        }\n        public void Refresh()', '            InitializeDisplay();\n            Refresh();\n        }\n        public void Refresh()'),
('UiKit.Clear(main); table.Hide(); visibleDice.Clear(); dieCenters.Clear();','UiKit.Clear(main); table.Hide(); visibleDice.Clear(); dieCenters.Clear(); controllerRects.Clear();'),
('P0 v0.5','P0 v0.6'),
('UiKit.Button(main,"BuildVersion"','UiKit.Button(main,"MenuSettings","设置",1370,1010,180,44,Pause);\n            UiKit.Button(main,"BuildVersion"'),
('            if(cmd.kind==CommandKind.ConfirmLocks)','            if(IsControllerDragging)return new CommandResult{accepted=false,error="先完成或取消控制器拖拽。"};\n            if(cmd.kind==CommandKind.ConfirmLocks)'),
('            PresentCommitted(cmd,result);','            if(result.noChange)return result;\n            PresentCommitted(cmd,result);'),
('附魔触发：×1.2 共 ','附魔触发：×2 共 '),
('int normalTarget=tutorial?50:def.normalTarget;','string normalTarget=tutorial?"50":def.normalTarget;'),
('ViewState.stage.target.ToString("N0")','Big(ViewState.stage.target)'),
('s.stage.score.ToString("N0")','Big(s.stage.score)'),
('b.score.ToString("N0")','Big(b.score)'),
('r.score.ToString("N0")','Big(r.score)'),
('r.target.ToString("N0")','Big(r.target)'),
('UiKit.Button(main,"StartStage",','UiKit.Button(main,"PreviewControllers","排列控制器 · 查看状态",90,905,1670,58,ShowControllerOrder);\n            UiKit.Button(main,"StartStage",'),
('            for(int i=0;i<unlocked.Count;i++)\n            {\n                var die=unlocked[i];','            poolPage=Math.Min(poolPage,Math.Max(0,(unlocked.Count-1)/10));\n            if(unlocked.Count>10)UiKit.Button(main,"PoolPage","骰池页 "+(poolPage+1)+" / "+((unlocked.Count+9)/10)+" ▸",1000,205,385,36,()=>{poolPage=(poolPage+1)%((unlocked.Count+9)/10);Refresh();});\n            for(int i=0;i<Math.Min(10,unlocked.Count-poolPage*10);i++)\n            {\n                var die=unlocked[i+poolPage*10];'),
('float slotW=1030f/s.stage.capacity;\n            for(int i=0;i<s.stage.capacity;i++)','int slotsOnPage=Math.Min(8,s.stage.capacity);slotPage=Math.Min(slotPage,Math.Max(0,(s.stage.capacity-1)/8));\n            if(s.stage.capacity>8)UiKit.Button(main,"SlotPage","提交页 "+(slotPage+1)+" / "+((s.stage.capacity+7)/8)+" ▸",1065,575,320,34,()=>{slotPage=(slotPage+1)%((s.stage.capacity+7)/8);Refresh();});\n            float slotW=1030f/slotsOnPage;\n            for(int visible=0;visible<Math.Min(8,s.stage.capacity-slotPage*8);visible++)'),
('float x=355+i*slotW;','int i=visible+slotPage*8;float x=355+visible*slotW;'),
('Vector2 ui=new Vector2(e.position.x/Screen.width*1920,(1-e.position.y/Screen.height)*1080);','Vector2 ui=ScreenToDesign(e.position);'),
('            var k=Keyboard.current;if(k==null)return;','            TickDisplay();\n            var k=Keyboard.current;if(k==null)return;\n            if(IsControllerDragging){if(k.escapeKey.wasPressedThisFrame)CancelControllerDrag();return;}\n            if(DisplayPreferences!=null&&DisplayPreferences.IsPending){if(k.escapeKey.wasPressedThisFrame)DisplayPreferences.Revert();return;}'),
('        private string NameOf(string id){if(string.IsNullOrEmpty(id))return "散点";', '        private static string Big(string value)=>System.Numerics.BigInteger.Parse(value??"0",System.Globalization.CultureInfo.InvariantCulture).ToString("N0",System.Globalization.CultureInfo.InvariantCulture);\n        private string NameOf(string id){if(string.IsNullOrEmpty(id)||id=="none"||id=="scatter")return "散点";if(id=="all_blue"||id=="all_red")return Config.Color(id).displayName;'),
('private string Description(string id){','private string Description(string id){if(id=="all_blue"||id=="all_red"){var c=Config.Color(id);return "全部提交骰为有效"+(id=="all_red"?"红":"蓝")+"色即成立，无最少数量。独立乘区，每级 +"+Factor(c.incrementPerLevel100)+"。";}'),
('if(paused&&IsBusy)SkipVisual();','if(paused){CancelControllerDrag();if(IsBusy)SkipVisual();}'),
('(tutorial||s.isDebug?"debug":"normal")','(tutorial||s.isDebug||s.debugWorkspace?"debug":"normal")'),
('UiKit.Text(main,"RunKind",tutorial?','UiKit.Text(main,"RunKind",tutorial?'),
('ViewState.isDebug?"DEBUG · 测试挑战"','ViewState.debugWorkspace?"DEBUG · 独立试验工作区"'),
('ScoreBreakdown b=Session.PreviewScore();','var preview=Session.PreviewScoreRange();ScoreBreakdown b=preview?.minimum;'),
('(b.colorMult100>0?" + 同色":"")','(b.colorId!=null?" · "+NameOf(b.colorId):"")'),
('$"同点 {Factor(b.groupMult100)} + 顺子 {Factor(b.straightMult100)} + 颜色 {Factor(b.colorMult100)}"','$"牌型 同点 {Factor(b.groupMult100)} + 顺子 {Factor(b.straightMult100)}"'),
('"牌型总倍率  ×"+Factor(b.totalHandMult100)','"牌型 ×"+Factor(b.totalHandMult100)+" · 颜色 ×"+Factor(b.colorMult100)'),
('"全手基础  +"+b.pairBaseAdd+"（对子） +"+b.enchantBaseAdd+"（附魔）"','"全手基础 "+Big(b.baseTotal)+"（含钱包/空栏/附魔）"'),
('"控制器 ×"+b.controllerMultiplier+" · 附魔 ×1.2 ^ "+b.enchantMultiplierCount','"控制器 ×"+b.controllerMultiplierNumerator+"/"+b.controllerMultiplierDenominator+" · 附魔 2^"+b.enchantMultiplierCount'),
('UiKit.Text(p,"FinalScore",Big(b.score),24,459,412,80,57,UiKit.Gold);','UiKit.Text(p,"FinalScore",preview.isRandom?Big(b.score)+" — "+Big(preview.maximum.score):Big(b.score),24,459,412,85,preview.isRandom?30:45,UiKit.Gold);\n            if(preview.isRandom)UiKit.Text(p,"RandomPreview","随机范围 · 确认提交才抽样",24,530,412,28,19,UiKit.Muted);'),
('()=>ShowBreakdown(Session.PreviewSnapshot(),b)','()=>ShowBreakdown(Session.PreviewSnapshot(),b)'),
])
edit('Runtime/DiceTable.cs',[
('        public bool IsRolling => animate;','        public RectTransform designRoot;\n        public bool IsRolling => animate;'),
('Vector2 screen=new Vector2(uiCenter.x/1920f*Screen.width,(1-uiCenter.y/1080f)*Screen.height);','Vector2 screen=designRoot!=null?RectTransformUtility.WorldToScreenPoint(null,designRoot.TransformPoint(new Vector3(uiCenter.x,-uiCenter.y,0))):new Vector2(uiCenter.x/1920f*Screen.width,(1-uiCenter.y/1080f)*Screen.height);')])
edit('UI/GameApp.Modals.cs',[
('private void CloseModal(){modal=false;','private void CloseModal(){if(DisplayPreferences!=null&&DisplayPreferences.IsPending){DisplayPreferences.Revert();return;}CancelControllerDrag();displayPanel=false;debugPanel=false;controllerOrderPanel=false;modal=false;'),
('            var p=OpenModal("暂停与设置");','            displayPanel=false;debugPanel=false;controllerOrderPanel=false;\n            var p=OpenModal("暂停与设置");'),
('40,115,1500,85,29','40,100,1500,65,25'),
('40,235,720,64','40,180,720,58'),('820,235,720,64','820,180,720,58'),
('40,350,1500,45,28','40,265,1500,40,25'),('40,420,1500,28','40,315,1500,28'),
('UiKit.Button(p,"Resume","继续挑战",40,575,720,72','UiKit.Button(p,"Resume","返回游戏",40,625,720,60'),
('UiKit.Button(p,"MainMenu","保存并返回主菜单",820,575,720,72','UiKit.Button(p,"MainMenu","保存并返回主菜单",820,625,720,60'),
('UiKit.Button(p,"PauseHandbook","牌型手册 [H]",40,495,1500,54','UiKit.Button(p,"DisplaySettings","显示与分辨率",40,380,720,58,ShowDisplaySettings);\n            UiKit.Button(p,"PauseHandbook","牌型与颜色手册 [H]",820,380,720,58'),
('            UiKit.Text(p,"SavePath"','            DebugAccess(p);\n            UiKit.Text(p,"SavePath"'),
('string before=step.before.ToString("0.####"),after=step.after.ToString("0.####");','string before=step.before,after=step.after;'),
('{step.operand:0.####}','{step.operand}'),
('step.kind=="red_group"?"同点红组"','step.kind=="color"?"独立颜色乘区":step.kind=="red_group"?"同点红组"'),
('$"({b.pipSum} + {b.unusedRollBonus} + {b.pairBaseAdd} + {b.enchantBaseAdd}) × {Factor(b.totalHandMult100)} × {Factor(b.redMult100)} × {b.controllerMultiplier} × (6/5)^{b.enchantMultiplierCount}\\n全部相乘后向下取整一次 = {b.score:N0}"','$"基础 {b.baseTotal} × 牌型 {Factor(b.totalHandMult100)} × 颜色 {Factor(b.colorMult100)} × 红组 {Factor(b.redMult100)} × 控制器 {b.controllerMultiplierNumerator}/{b.controllerMultiplierDenominator} × 2^{b.enchantMultiplierCount}\\n全部相乘后向下取整一次 = {Big(b.score)}"'),
('JsonUtility.FromJson<GameConfig>(configAsset.text)','JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(configAsset.text))'),
('practiceConfig.stages[0].normalTarget=50;','practiceConfig.stages[0].normalTarget="50";'),
('ViewState.controllers.Any(x=>x.contentId=="C12")','ViewSources.Any(x=>x.abilityId=="C12")'),
])
edit('UI/GameApp.Shop.cs',[
('每件商品限购一次，不补货。出售控制器或未使用道具固定获得 1 金币。','控制器可单独刷新；其他商品保留。普通出售1金币，基金售价包含本实例累积。'),
('1840,40,23,UiKit.Muted);','1250,40,21,UiKit.Muted);\n            if(shopTab==0)UiKit.Button(main,"RefreshControllers","刷新控制器 · "+Session.GetRefreshPrice()+" 金币",1360,236,520,46,()=>{var r=Send(new SessionCommand{kind=CommandKind.RefreshControllers});if(r.accepted)PlaySound("refresh");},ViewState.gold>=Session.GetRefreshPrice());'),
('shopTab==2?o.category=="hand_upgrade"','shopTab==2?o.category=="hand_upgrade"||o.category=="color_upgrade"'),
('按正常升级价 +2','按正常升级价 +1'),
('int price=Session.GetOfferNetPrice(offer);','long price=Session.GetOfferNetPrice(offer);'),
('float y=878;','float y=878;controllerRects.Clear();'),
('int slots=Math.Max(4,ViewState.controllers.Count);','int slots=Math.Min(10,Math.Max(4,ViewState.controllers.Count));'),
('EnchantBadge(b.transform,item.enchantmentId,5,5,22);','EnchantBadge(b.transform,item.enchantmentId,5,5,22);AttachControllerDrag(b,item);'),
('float itemW=690f/ViewState.consumableSlots;\n            for(int i=0;i<ViewState.consumableSlots;i++)','int itemCount=Math.Min(5,ViewState.consumableSlots);inventoryPage=Math.Min(inventoryPage,Math.Max(0,(ViewState.consumableSlots-1)/5));float itemW=690f/itemCount;\n            UiKit.Button(main,"AllControllerStates","排列 / 全部状态",875,y-23,260,34,ShowControllerOrder);\n            if(ViewState.consumableSlots>5)UiKit.Button(main,"InventoryPage","道具页 "+(inventoryPage+1)+" ▸",1630,y-24,240,34,()=>{inventoryPage=(inventoryPage+1)%((ViewState.consumableSlots+4)/5);Refresh();});\n            for(int visible=0;visible<Math.Min(5,ViewState.consumableSlots-inventoryPage*5);visible++)'),
('float x=1190+i*itemW;','int i=visible+inventoryPage*5;float x=1190+visible*itemW;'),
('string id=legal[i];int level=ViewState.handLevels.First(x=>x.handId==id).level,price=Session.GetStudyPrice(id);var h=Config.Hand(id);','string id=legal[i];bool color=id=="all_blue"||id=="all_red";int level=(color?ViewState.colorLevels:ViewState.handLevels).First(x=>x.handId==id).level,price=Session.GetStudyPrice(id);int baseMult=color?Config.Color(id).baseMult100:Config.Hand(id).baseMult100,increment=color?Config.Color(id).incrementPerLevel100:Config.Hand(id).incrementPerLevel100;'),
('{h.displayName}','{NameOf(id)}'),('h.baseMult100+h.incrementPerLevel100','baseMult+increment'),
('影响奇数控制器、2233、顺子和红组判定','影响奇数控制器、2233、顺子和红组判定'),
('顺子和颜色类升级','牌型升级'),
])
edit('UI/GameApp.Debug.cs',[
('public bool IsDebugWorkspace => Session?.State?.debugWorkspace==true;','public bool IsDebugWorkspace => ViewState?.debugWorkspace==true;'),
('return 850+Math.Max((Config.controllers.Length+3)/4,(debugDraft.controllers.Count+2)/3)*72;','return 900+((Config.controllers.Length+3)/4)*64+((debugDraft.controllers.Count+2)/3)*62;'),
('if(debugTab==2)return 1200;','if(debugTab==2)return 1600;'),
])
print('v0.6 UI adaptation complete')
