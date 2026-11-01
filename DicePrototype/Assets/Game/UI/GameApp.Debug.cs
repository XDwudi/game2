using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        public bool DebugUnlocked { get; private set; }
        private AtomicSaveStore debugSaves;
        private DebugScene debugDraft;
        private int debugTab,debugNav07,debugRenderedTab07=-1;
        private UnityEngine.UI.ScrollRect debugScroll07;
        private string debugSelectedController,debugSelectedDie,debugMessage="",debugJsonPath;
        private readonly Dictionary<string,string> debugErrors=new Dictionary<string,string>();
        private readonly Dictionary<string,string> debugRaw=new Dictionary<string,string>();
        private TextMeshProUGUI debugStatus;
        private bool debugPanel;
        private int debugPasswordSubmitFrame=-1;
        private int poolPage,slotPage,inventoryPage;
        public bool IsDebugWorkspace => !tutorial&&ViewState?.debugWorkspace==true;
        private static readonly string[] DebugEnchantments={"none","slotless","base10","mult2","submit1","roll1"};

        private void ShowDebugEntry07()
        {
            if(!DebugUnlocked||IsBusy||Session?.HasPendingSave==true)return;
            var p=Art07Modal("组合试验",440,220,1040,640);
            UiKit.Text(p,"Isolation","普通对局保留在原检查点。\n试验副本独立保存，首次应用修改后标记为调试。",24,100,992,130,27,UiKit.Muted);
            UiKit.Button(p,"CopyCurrentToDebug","从当前对局建立实验副本",24,256,992,72,()=>EnterDebugWorkspace(),Session?.State!=null);
            UiKit.Button(p,"BlankDebug","新建空白场景",24,348,992,72,()=>
            {
                debugSaves=debugSaves??new AtomicSaveStore(Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"debug-run.json"),Config,SaveSlotPolicy.Debug);
                Session=GameSession.CreateDebugWorkspace(Config,debugSaves,null);StartRunClock07();tutorial=false;inMenu=false;Refresh();LoadDebugDraft();ShowDebugPanel();
            });
            UiKit.Button(p,"LoadDebugScene","加载测试场景",24,440,992,72,()=>
            {
                if(!EnterDebugWorkspace())return;debugTab=4;ShowDebugPanel();
            });
            UiKit.Text(p,"Version","规则 "+Config.configVersion+" · 独立文件 debug-run.json",24,548,992,64,23,UiKit.Violet);
        }

        public bool UnlockDebug(string password)
        {
            if(!string.Equals(password,"XD is god",StringComparison.Ordinal))return false;
            DebugUnlocked=true;return true;
        }
        private TMP_InputField FreeInput(Transform p,string key,string placeholder,string value,float x,float y,float w,float h,Action<string> change=null,int limit=4096)
        {
            var f=UiKit.Input(p,key,placeholder,value??"",x,y,w,h);
            f.contentType=TMP_InputField.ContentType.Standard;f.characterLimit=limit;
            if(change!=null)f.onValueChanged.AddListener(v=>change(v));return f;
        }
        private void DebugAccess(Transform p)
        {
            UiKit.Text(p,"DebugAccessLabel","调试入口",40,520,290,40,25,UiKit.Muted);
            var field=FreeInput(p,"DebugPassword","输入口令","",320,510,840,56,null,100);
            Action enter=()=>{if(debugPasswordSubmitFrame==Time.frameCount)return;debugPasswordSubmitFrame=Time.frameCount;if(UnlockDebug(field.text)){debugMessage="调试入口已解锁；普通挑战尚未改变。";Pause();}else{field.text="";UiKit.Text(p,"DebugPasswordError","口令不匹配",1190,575,340,34,21,UiKit.Red);}};
            field.onSubmit.AddListener(_=>enter());
            UiKit.Button(p,"UnlockDebug",DebugUnlocked?"打开调试":"确认口令",1190,510,350,56,()=>{if(DebugUnlocked)EnterDebugWorkspace();else enter();});
        }
        public bool EnterDebugWorkspace()
        {
            if(!StartupReady07||!DebugUnlocked||IsBusy||Session.HasPendingSave)return false;
            if(!IsDebugWorkspace)
            {
                debugSaves=debugSaves??new AtomicSaveStore(Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"debug-run.json"),Config,SaveSlotPolicy.Debug);
                Session=GameSession.CreateDebugWorkspace(Config,debugSaves,Session.State);
                StartRunClock07();
                tutorial=false;inMenu=false;Refresh();
            }
            LoadDebugDraft();ShowDebugPanel();return true;
        }
        public void ReturnNormalWorkspace()
        {
            if(!StartupReady07)return;
            if(Session.HasPendingSave){SaveFailure();return;}
            SkipVisual();CloseModal();Session=new GameSession(Config,saves,saves.Load());inMenu=Session.State==null;tutorial=false;
            debugDraft=null;debugPanel=false;poolPage=slotPage=inventoryPage=0;Refresh();
        }
        public bool ResumeDebugWorkspace()
        {
            if(!StartupReady07||!DebugUnlocked||Session.HasPendingSave)return false;
            debugSaves=debugSaves??new AtomicSaveStore(Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"debug-run.json"),Config,SaveSlotPolicy.Debug);
            var restored=debugSaves.Load();if(restored==null||!restored.debugWorkspace){debugMessage=debugSaves.LastLoadNotice??"没有调试存档。";return false;}
            Session=new GameSession(Config,debugSaves,restored);inMenu=false;tutorial=false;Refresh();LoadDebugDraft();ShowDebugPanel();return true;
        }
        private void LoadDebugDraft()
        {
            debugDraft=DebugScene.FromState(Session.State);debugRaw.Clear();debugErrors.Clear();
            debugSelectedController=debugDraft.controllers.FirstOrDefault()?.instanceId;debugSelectedDie=debugDraft.dice.FirstOrDefault()?.dieId;
            debugJsonPath=debugJsonPath??Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"debug-scene.json");
        }
        private void DraftChanged(){debugRaw.Clear();debugErrors.Clear();ShowDebugPanel();}
        public void ShowDebugPanel()
        {
            if(!DebugUnlocked||!IsDebugWorkspace)return;
            if(debugDraft==null)LoadDebugDraft();debugPanel=true;
            float scrollPosition=debugScroll07!=null&&debugRenderedTab07==debugTab?debugScroll07.verticalNormalizedPosition:1;
            var p=Art07Modal("调试对局 · 组合试验",0,0,1920,1080);
            UiKit.Box(p,"DebugStripe",0,0,1920,4,UiKit.Violet);
            UiKit.Text(p,"DebugIsolation","候选应用后才改变试验；普通对局保留。",540,18,1180,54,24,UiKit.Muted);
            string[] labels={"控制器","道具与服务","骰池","当前提交","牌型与颜色","资源","随机与复现"};
            int[] pages={0,3,1,1,2,3,4};
            if(pages[debugNav07]!=debugTab)debugNav07=Array.IndexOf(pages,debugTab);
            UiKit.Frame(p,"DebugNavigation",24,96,288,800,UiKit.Background,UiKit.PanelLight);
            for(int i=0;i<labels.Length;i++)
            {
                int nav=i;
                string controlName=i==3||i==5?"DebugNav"+i:"DebugTab"+pages[i];
                UiKit.Button(p,controlName,labels[i],40,112+i*88,256,72,()=>{debugNav07=nav;debugTab=pages[nav];debugRenderedTab07=-1;ShowDebugPanel();if(nav==1)ScrollDebugTo07("ItemsDebug");if(nav==3)ScrollDebugTo07("CurrentResultTitle");},true,debugNav07==i);
            }
            UiKit.Button(p,"ResumeDebugSave","恢复调试存档",40,752,256,64,()=>ResumeDebugWorkspace());
            UiKit.Button(p,"ReturnNormal","返回普通对局",40,828,256,64,ReturnNormalWorkspace);
            UiKit.Frame(p,"DebugForm",336,96,896,800,UiKit.Panel,UiKit.PanelLight);
            var list=UiKit.Scroll(p,"DebugEditor"+debugTab,352,112,864,768,DebugContentHeight());
            switch(debugTab){case 0:DrawDebugControllers(list);break;case 1:DrawDebugDice(list);break;case 2:DrawDebugLevels(list);break;case 3:DrawDebugResources(list);break;case 4:DrawDebugReproduction(list);break;}
            ReflowDebug07(list,832);
            debugScroll07=list.GetComponentInParent<UnityEngine.UI.ScrollRect>();debugScroll07.verticalNormalizedPosition=scrollPosition;debugRenderedTab07=debugTab;
            DrawDebugPreview07(p);
            debugStatus=UiKit.Text(p,"DebugStatus",DebugStatusText(),336,920,1560,52,22,UiKit.Gold);
            debugStatus.textWrappingMode=TextWrappingModes.NoWrap;debugStatus.overflowMode=TextOverflowModes.Ellipsis;
            UiKit.Box(p,"DebugFooter",0,984,1920,96,UiKit.Background);
            UiKit.ActionButton(p,"ApplyDebugScene","应用场景",24,996,360,64,ApplyDebugDraft,UiKit.Violet,!IsBusy&&!Session.HasPendingSave);
            UiKit.Button(p,"ResetDebugDraft","恢复默认候选",396,996,360,64,()=>
            {
                var blank=GameSession.CreateDebugWorkspace(Config,new MemorySaveStore());debugDraft=DebugScene.FromState(blank.State);debugRaw.Clear();debugErrors.Clear();debugSelectedController=null;debugSelectedDie=debugDraft.dice[0].dieId;debugMessage="默认候选已载入；点击应用后生效。";ShowDebugPanel();
            });
            UiKit.Button(p,"RollDebugOnce","投掷一次",768,996,360,64,()=>RunDebugCommand07(CommandKind.Roll),!IsBusy);
            UiKit.Button(p,"SubmitDebugOnce","提交一次",1140,996,360,64,()=>RunDebugCommand07(CommandKind.Submit),!IsBusy);
            UiKit.Button(p,"ExportDebugScene","导出场景",1512,996,384,64,ExportDebugScene);
            Canvas.ForceUpdateCanvases();
        }
        private void RunDebugCommand07(CommandKind kind)
        {
            if(debugErrors.Count>0||JsonUtility.ToJson(debugDraft)!=JsonUtility.ToJson(DebugScene.FromState(Session.State)))
            {debugMessage="先应用候选，或重新读取当前场景，再执行一次。";ShowDebugPanel();return;}
            var result=Send(new SessionCommand{kind=kind});
            if(result.accepted){if(IsBusy)SkipVisual();LoadDebugDraft();debugMessage="已执行一次；右侧为真实提交后的状态。";}else debugMessage=result.error;
            ShowDebugPanel();
        }
        private void ScrollDebugTo07(string name)
        {
            if(debugScroll07==null)return;var child=debugScroll07.content.Find(name) as RectTransform;if(child==null)return;
            float range=debugScroll07.content.rect.height-debugScroll07.viewport.rect.height;
            if(range>0)debugScroll07.verticalNormalizedPosition=1-Mathf.Clamp01(-child.anchoredPosition.y/range);
        }
        private void DrawDebugPreview07(Transform p)
        {
            UiKit.Frame(p,"DebugReadOnly",1256,96,640,800,UiKit.Background,UiKit.Violet);
            UiKit.Text(p,"PreviewTitle","已应用场景 · 只读",1280,112,592,54,28,UiKit.Paper);
            var list=UiKit.Scroll(p,"DebugPreview",1280,180,592,608,900);float y=0;
            var s=Session.State;
            Art07DetailSection(list,"Resources","状态",$"第 {s.stage.stageIndex+1} 关 · {s.phase}\n骰子 {s.dice.Count} / 32 · 控制器 {s.controllers.Count} / 32\n金币 {s.gold} · 投掷 {s.hand?.rollsRemaining??0} · 提交 {s.stage.remainingSubmissions}",ref y,560);
            var preview=Session.PreviewScoreRange();
            string score=preview==null?"尚无可提交的已锁骰。":preview.isRandom?"随机范围 "+Big(preview.minimum.score)+" — "+Big(preview.maximum.score):Big(preview.minimum.score);
            Art07DetailSection(list,"Score","预计得分",score,ref y,560);
            if(preview!=null)Art07DetailSection(list,"Formula","独立乘区","基础 "+preview.minimum.baseTotal+"\n牌型 ×"+Factor(preview.minimum.totalHandMult100)+" · 颜色 ×"+Factor(preview.minimum.colorMult100)+"\n红组 ×"+Factor(preview.minimum.redMult100)+"\n控制器 ×"+preview.minimum.controllerMultiplierNumerator+"/"+preview.minimum.controllerMultiplierDenominator+" · 附魔 2^"+preview.minimum.enchantMultiplierCount,ref y,560);
            Art07DetailSection(list,"Locked","有序提交",s.hand==null||s.hand.orderedLockedIds.Count==0?"空":string.Join(" → ",s.hand.orderedLockedIds),ref y,560);
            Art07DetailSection(list,"Rng","当前随机流",$"Dice {s.diceRngState}\nShop {s.shopRngState}\nEffect {s.effectRngState}",ref y,560);
            if(debugErrors.Count>0)Art07DetailSection(list,"Errors","候选输入错误",string.Join("\n",debugErrors.Values),ref y,560);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Button(p,"ReloadDebugDraft","重新读取当前",1280,808,280,64,()=>{LoadDebugDraft();ShowDebugPanel();});
            UiKit.Button(p,"RunDebugScene","返回试验桌",1584,808,288,64,CloseModal);
        }
        private void ReflowDebug07(RectTransform content,float width)
        {
            // Preserve every existing field/callback; reflow their groups into the current form width.
            // Labels and inputs are one unit, so keyboard focus and target identity remain attached.
            var children=Enumerable.Range(0,content.childCount).Select(i=>content.GetChild(i) as RectTransform).Where(r=>r!=null&&r.gameObject.activeSelf).ToArray();
            var consumed=new HashSet<RectTransform>();var units=new List<DebugLayoutUnit07>();
            foreach(var r in children)
            {
                if(consumed.Contains(r))continue;
                var input=r.GetComponent<TMP_InputField>();
                if(input!=null)
                {
                    var label=children.FirstOrDefault(c=>c.name==r.name+"Label");if(label!=null)consumed.Add(label);
                    consumed.Add(r);units.Add(new DebugLayoutUnit07{root=r,label=label,x=r.anchoredPosition.x,y=label==null?-r.anchoredPosition.y:-label.anchoredPosition.y,width=r.rect.width});
                }
                else if(r.name.EndsWith("Label",StringComparison.Ordinal)&&children.Any(c=>c.name==r.name.Substring(0,r.name.Length-5)&&c.GetComponent<TMP_InputField>()!=null))continue;
                else{consumed.Add(r);units.Add(new DebugLayoutUnit07{root=r,x=r.anchoredPosition.x,y=-r.anchoredPosition.y,width=r.rect.width});}
            }
            float cursor=0;
            foreach(var row in units.OrderBy(u=>u.y).ThenBy(u=>u.x).GroupBy(u=>Mathf.RoundToInt(u.y)))
            {
                float x=0,rowHeight=0;
                foreach(var unit in row)
                {
                    float w=Math.Min(width,unit.width);
                    if(unit.root.GetComponent<TextMeshProUGUI>()!=null&&unit.width>width*.7f)w=width;
                    if(x>0&&x+w>width+1){cursor+=rowHeight+16;x=0;rowHeight=0;}
                    float height=ResizeDebugUnit07(unit,x,cursor,w);rowHeight=Math.Max(rowHeight,height);x+=w+16;
                }
                cursor+=rowHeight+20;
            }
            content.sizeDelta=new Vector2(width,Math.Max(768,cursor));
        }
        private sealed class DebugLayoutUnit07 {public RectTransform root,label;public float x,y,width;}
        private float ResizeDebugUnit07(DebugLayoutUnit07 unit,float x,float y,float width)
        {
            var r=unit.root;float height=r.rect.height;
            var text=r.GetComponent<TextMeshProUGUI>();var input=r.GetComponent<TMP_InputField>();var button=r.GetComponent<UnityEngine.UI.Button>();
            if(input!=null)
            {
                float labelHeight=unit.label==null?0:Math.Max(52,unit.label.GetComponent<TextMeshProUGUI>().GetPreferredValues(unit.label.GetComponent<TextMeshProUGUI>().text,width,float.PositiveInfinity).y+8);
                if(unit.label!=null){unit.label.anchoredPosition=new Vector2(x,-y);unit.label.sizeDelta=new Vector2(width,labelHeight);}
                height=64;r.anchoredPosition=new Vector2(x,-(y+labelHeight+8));r.sizeDelta=new Vector2(width,height);
                var viewport=input.textViewport;viewport.anchoredPosition=new Vector2(16,-6);viewport.sizeDelta=new Vector2(width-32,height-12);
                input.textComponent.rectTransform.sizeDelta=new Vector2(width-32,height-12);if(input.placeholder is TextMeshProUGUI ph)ph.rectTransform.sizeDelta=new Vector2(width-32,height-12);
                input.ForceLabelUpdate();return labelHeight+height+8;
            }
            if(text!=null)height=Math.Max(height,text.GetPreferredValues(text.text,width,float.PositiveInfinity).y+8);
            if(button!=null)
            {
                var label=button.GetComponentInChildren<TextMeshProUGUI>();height=Math.Max(64,label==null?64:label.GetPreferredValues(label.text,width-16,float.PositiveInfinity).y+18);
                foreach(RectTransform child in r)
                {
                    if(child.name=="Label"){child.anchoredPosition=new Vector2(8,-3);child.sizeDelta=new Vector2(width-16,height-10);}
                    else if(child.name=="Face"){child.anchoredPosition=new Vector2(4,-4);child.sizeDelta=new Vector2(width-8,height-10);}
                    else if(child.name=="Step"){child.anchoredPosition=new Vector2(3,-(height-4));child.sizeDelta=new Vector2(width-6,6);}
                    else if(child.name=="Lip")child.sizeDelta=new Vector2(width-16,2);
                }
            }
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return height;
        }
        private float DebugContentHeight()
        {
            if(debugTab==0)return 900+((Config.controllers.Length+3)/4)*64+((debugDraft.controllers.Count+2)/3)*62;
            if(debugTab==1)return 1150+((debugDraft.dice.Count+7)/8)*55;
            if(debugTab==2)return 1600;
            if(debugTab==3)return 1450+((debugDraft.inventory.Count+3)/4)*60;
            return 1250+debugDraft.controllers.Count*64+debugDraft.dice.Count*52;
        }
        private string DebugStatusText()=>debugErrors.Count>0?string.Join("；",debugErrors.Values.Take(3)):debugMessage;
        private void DebugField(Transform p,string key,string label,string value,float x,float y,float w,Action<string> set,bool enabled=true)
        {
            UiKit.Text(p,key+"Label",label,x,y,w,30,21,UiKit.Muted);
            var input=FreeInput(p,key,"",debugRaw.TryGetValue(key,out var raw)?raw:value,x,y+33,w,48,v=>
            {
                debugRaw[key]=v;
                try{set(v);debugErrors.Remove(key);}catch(Exception e) when(e is FormatException||e is OverflowException||e is ArgumentException){debugErrors[key]=label+"：输入无效";}
                if(debugStatus!=null)debugStatus.text=DebugStatusText();
            },4096);input.interactable=enabled;
        }
        private static int DebugInt(string v,int min,int max){int n=int.Parse(v,NumberStyles.Integer,CultureInfo.InvariantCulture);if(n<min||n>max)throw new ArgumentOutOfRangeException();return n;}
        private static long DebugLong(string v,long min,long max){long n=long.Parse(v,NumberStyles.Integer,CultureInfo.InvariantCulture);if(n<min||n>max)throw new ArgumentOutOfRangeException();return n;}
        private static string DebugScore(string v){if(string.IsNullOrEmpty(v)||v.Any(c=>c<'0'||c>'9'))throw new FormatException();return System.Numerics.BigInteger.Parse(v,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);}
        private void ApplyDebugDraft()
        {
            if(debugErrors.Count>0){debugMessage="请先修正输入。";ShowDebugPanel();return;}
            var result=Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=debugDraft.Clone()});
            if(result.accepted){PlaySound("debug");LoadDebugDraft();debugMessage="已重建测试手并保存；明确指定的成员和面值已回填。";}
            else debugMessage=result.error;
            ShowDebugPanel();
        }
        private void DrawDebugControllers(Transform p)
        {
            UiKit.Text(p,"ControllerCount","控制器 "+debugDraft.controllers.Count+" / 32 · 允许同名与任意附魔",10,0,1450,40,26,UiKit.Teal);
            float y=48;
            for(int i=0;i<debugDraft.controllers.Count;i++)
            {
                var c=debugDraft.controllers[i];UiKit.Button(p,"DebugOwned"+c.instanceId,NameOf(c.definitionId)+" · "+EnchantShort(c.enchantmentId),(i%3)*485,y+(i/3)*62,465,50,()=>{debugSelectedController=c.instanceId;DraftChanged();},true,debugSelectedController==c.instanceId);
            }
            y+=Math.Max(1,(debugDraft.controllers.Count+2)/3)*62+18;
            var selected=debugDraft.controllers.FirstOrDefault(c=>c.instanceId==debugSelectedController);
            if(selected!=null)
            {
                UiKit.Text(p,"SelectedController",selected.instanceId+" · "+Description(selected.definitionId),10,y,1450,80,24);y+=85;
                for(int i=0;i<DebugEnchantments.Length;i++){string enchant=DebugEnchantments[i];UiKit.Button(p,"DebugEnchant"+enchant,EnchantShort(enchant),i*242,y,226,48,()=>{selected.enchantmentId=enchant;DraftChanged();},true,selected.enchantmentId==enchant);}y+=60;
                if(!ControllerRules.IsEnchantmentLegal(selected.definitionId,selected.enchantmentId,Config))UiKit.Text(p,"DebugEnchantWarning","此附魔只允许调试构造；非提交能力不会触发计分附魔。",10,y,1430,35,21,UiKit.Gold);y+=40;
                int index=debugDraft.controllers.IndexOf(selected);
                UiKit.Button(p,"DebugControllerLeft","← 前移",10,y,280,48,()=>{debugDraft.controllers.RemoveAt(index);debugDraft.controllers.Insert(index-1,selected);debugDraft.mimicBindings=null;DraftChanged();},index>0);
                UiKit.Button(p,"DebugControllerRight","后移 →",310,y,280,48,()=>{debugDraft.controllers.RemoveAt(index);debugDraft.controllers.Insert(index+1,selected);debugDraft.mimicBindings=null;DraftChanged();},index<debugDraft.controllers.Count-1);
                UiKit.Button(p,"DebugRemoveController","移除（不触发卖出成长）",610,y,490,48,()=>{debugDraft.controllers.Remove(selected);debugDraft.mimicBindings=null;debugSelectedController=null;DraftChanged();});y+=65;
                DebugField(p,"Turnover","人员流动本体倍率 n（≥1.5，0.5步进）",(selected.state.turnoverUnits2/2m).ToString(CultureInfo.InvariantCulture),10,y,650,v=>{decimal n=decimal.Parse(v,CultureInfo.InvariantCulture);if(n<1.5m||n*2!=decimal.Truncate(n*2))throw new FormatException();selected.state.turnoverUnits2=checked((int)(n*2));},selected.definitionId=="C51");
                DebugField(p,"Fund","本实例基金累积（3的倍数）",selected.state.fundAccrual.ToString(),710,y,740,v=>{long n=DebugLong(v,0,long.MaxValue-1);if(n%3!=0)throw new FormatException();selected.state.fundAccrual=n;},selected.definitionId=="C52"||selected.definitionId=="C41");y+=100;
                UiKit.Button(p,"ActiveColor","当前颜色："+(selected.state.activeColor??"不适用"),10,y,450,48,()=>{selected.state.activeColor=selected.state.activeColor=="red"?"blue":"red";DraftChanged();},selected.definitionId=="C45");
                bool canUseC05=false;try{canUseC05=ControllerRules.ResolveSources(debugDraft.controllers,debugDraft.mimicBindings).Any(s=>s.emitterId==selected.instanceId&&s.abilityId=="C05");}catch(ArgumentException){}
                UiKit.Button(p,"C05Used","再来三次已用："+debugDraft.c05UsedEmitterIds.Contains(selected.instanceId),490,y,450,48,()=>{if(!debugDraft.c05UsedEmitterIds.Remove(selected.instanceId))debugDraft.c05UsedEmitterIds.Add(selected.instanceId);DraftChanged();},canUseC05);y+=62;
                if(selected.definitionId=="C41")
                {
                    var map=debugDraft.mimicBindings??ControllerRules.FreezeBindings(debugDraft.controllers);var binding=map.FirstOrDefault(b=>b.sourceInstanceId==selected.instanceId);
                    var targets=new[]{""}.Concat(debugDraft.controllers.Where(c=>c.instanceId!=selected.instanceId).Select(c=>c.instanceId)).ToArray();
                    UiKit.Button(p,"MimicTarget","复制绑定："+(binding?.targetInstanceId??"无")+" >",10,y,930,48,()=>{var list=map.Where(b=>b.sourceInstanceId!=selected.instanceId).ToList();int t=Array.IndexOf(targets,binding?.targetInstanceId??"");list.Add(new MimicBinding{sourceInstanceId=selected.instanceId,targetInstanceId=targets[(t+1)%targets.Length]});debugDraft.mimicBindings=list.ToArray();DraftChanged();});
                    UiKit.Button(p,"MimicAdjacent","按当前右邻绑定",960,y,490,48,()=>{debugDraft.mimicBindings=null;DraftChanged();});y+=62;
                }
            }
            UiKit.Text(p,"AddControllerTitle","添加控制器（全部50项）",10,y,1450,45,27,UiKit.Teal);y+=55;
            for(int i=0;i<Config.controllers.Length;i++)
            {
                var c=Config.controllers[i];UiKit.Button(p,"Add"+c.id,c.id+" · "+c.displayName,(i%4)*365,y+(i/4)*64,350,52,()=>
                {int serial=NextDebugItemSerial();var item=new ControllerInstance{instanceId="ITEM_"+serial.ToString("D5"),definitionId=c.id,acquiredSerial=serial,state=new ControllerDynamicState{activeColor=c.id=="C45"?"red":null,turnoverUnits2=3}};debugDraft.controllers.Add(item);debugSelectedController=item.instanceId;debugDraft.mimicBindings=null;DraftChanged();},debugDraft.controllers.Count<32);
            }
        }
        private void SetDebugDiceCount(int count)
        {
            while(debugDraft.dice.Count<count){int serial=Math.Max(ViewState.nextDieSerial,debugDraft.dice.Select(d=>d.createdSerial).DefaultIfEmpty().Max()+1);debugDraft.dice.Add(DiceFactory.CreateStandard(Config,serial));}
            if(debugDraft.dice.Count>count)debugDraft.dice.RemoveRange(count,debugDraft.dice.Count-count);
            var ids=new HashSet<string>(debugDraft.dice.Select(d=>d.dieId));debugDraft.orderedLockedIds.RemoveAll(id=>!ids.Contains(id));
            if(debugDraft.currentResults!=null){debugDraft.currentResults.RemoveAll(r=>!ids.Contains(r.dieId));EnsureDebugResults();}
            if(!ids.Contains(debugSelectedDie??""))debugSelectedDie=debugDraft.dice[0].dieId;
        }
        private void EnsureDebugResults()
        {
            debugDraft.currentResults=debugDraft.currentResults??new List<DieHandState>();
            foreach(var d in debugDraft.dice)if(!debugDraft.currentResults.Any(r=>r.dieId==d.dieId)){var f=d.faces[0];debugDraft.currentResults.Add(new DieHandState{dieId=d.dieId,currentFaceId=f.faceId,currentPip=f.permanentPip,currentColor=f.baseColor,hasRolled=true,isLocked=debugDraft.orderedLockedIds.Contains(d.dieId)});}
        }
        private void DrawDebugDice(Transform p)
        {
            UiKit.Button(p,"RemoveDie","− 骰子",10,0,225,48,()=>{SetDebugDiceCount(debugDraft.dice.Count-1);DraftChanged();},debugDraft.dice.Count>1);
            UiKit.Button(p,"AddDie","+ 骰子",255,0,225,48,()=>{SetDebugDiceCount(debugDraft.dice.Count+1);DraftChanged();},debugDraft.dice.Count<32);
            UiKit.Text(p,"DiceCount",debugDraft.dice.Count+" / 32",500,5,220,40,27,UiKit.Gold);
            UiKit.Button(p,"ResetDice","全部恢复标准六面",780,0,665,48,()=>{debugDraft.dice=debugDraft.dice.Select(d=>DiceFactory.CreateStandard(Config,d.createdSerial,d.lifetime,d.createdStageId)).ToList();debugDraft.currentResults=null;debugDraft.orderedLockedIds.Clear();DraftChanged();});
            float y=68;
            for(int i=0;i<debugDraft.dice.Count;i++){var die=debugDraft.dice[i];UiKit.Button(p,"DebugDie"+die.dieId,die.dieId,(i%8)*183,y+(i/8)*55,169,44,()=>{debugSelectedDie=die.dieId;DraftChanged();},true,debugSelectedDie==die.dieId);}
            y+=((debugDraft.dice.Count+7)/8)*55+12;
            var d=debugDraft.dice.FirstOrDefault(x=>x.dieId==debugSelectedDie)??debugDraft.dice[0];
            UiKit.Button(p,"DebugGlass","玻璃："+d.isGlass,10,y,460,48,()=>{d.isGlass=!d.isGlass;DraftChanged();});
            UiKit.Button(p,"DebugLifetime",d.lifetime=="permanent"?"永久骰":"本关临时骰",500,y,460,48,()=>{d.lifetime=d.lifetime=="permanent"?"stage_temporary":"permanent";DraftChanged();});
            UiKit.Button(p,"DebugStandard","本骰恢复标准",990,y,460,48,()=>{var replacement=DiceFactory.CreateStandard(Config,d.createdSerial,d.lifetime,d.createdStageId);debugDraft.dice[debugDraft.dice.IndexOf(d)]=replacement;DraftChanged();});y+=70;
            UiKit.Text(p,"FaceEditorHint","永久六面：点击点数/颜色/纹理/镀金切换。批量操作不改变其他维度。",10,y,1430,40,23,UiKit.Muted);y+=50;
            for(int i=0;i<6;i++)
            {
                var f=d.faces[i];float x=i*244;UiKit.Text(p,"DebugFaceLabel"+i,f.faceId,x+10,y,225,34,23,UiKit.Muted);
                UiKit.Button(p,"DebugPip"+i,"点数 "+f.permanentPip,x+10,y+40,222,48,()=>{f.permanentPip=f.permanentPip%6+1;DraftChanged();});
                UiKit.Button(p,"DebugColor"+i,f.baseColor=="red"?"红 R":"蓝 B",x+10,y+98,222,48,()=>{f.baseColor=f.baseColor=="red"?"blue":"red";DraftChanged();});
                UiKit.Button(p,"DebugTexture"+i,f.texture=="none"?"无纹理":f.texture=="smooth"?"光滑":"粗糙",x+10,y+156,222,48,()=>{f.texture=f.texture=="none"?"smooth":f.texture=="smooth"?"rough":"none";DraftChanged();});
                UiKit.Button(p,"DebugGold"+i,"镀金 "+f.isGold,x+10,y+214,222,48,()=>{f.isGold=!f.isGold;DraftChanged();});
            }
            y+=283;
            for(int i=1;i<=6;i++){int pip=i;UiKit.Button(p,"BatchPip"+i,"全部面 = "+i,(i-1)*244+10,y,222,48,()=>{foreach(var die in debugDraft.dice)foreach(var f in die.faces)f.permanentPip=pip;DraftChanged();});}y+=63;
            UiKit.Button(p,"AllRed","全部骰面染红",10,y,460,48,()=>{foreach(var die in debugDraft.dice)foreach(var f in die.faces)f.baseColor="red";DraftChanged();});
            UiKit.Button(p,"AllBlue","全部骰面染蓝",500,y,460,48,()=>{foreach(var die in debugDraft.dice)foreach(var f in die.faces)f.baseColor="blue";DraftChanged();});
            UiKit.Button(p,"FreshHand","新手空结果（清除锁定）",990,y,460,48,()=>{debugDraft.currentResults=null;debugDraft.orderedLockedIds.Clear();debugDraft.c05UsedEmitterIds.Clear();DraftChanged();});y+=72;
            UiKit.Text(p,"CurrentResultTitle","本次结果与有序提交（独立于永久面）",10,y,1430,42,27,UiKit.Teal);y+=54;
            var r=debugDraft.currentResults?.FirstOrDefault(x=>x.dieId==d.dieId);
            UiKit.Button(p,"CurrentFace","当前面："+(r?.currentFaceId??"未投"),10,y,460,48,()=>{EnsureDebugResults();var q=debugDraft.currentResults.First(x=>x.dieId==d.dieId);int index=Array.FindIndex(d.faces,f=>f.faceId==q.currentFaceId);var f=d.faces[(index+1)%6];q.currentFaceId=f.faceId;q.currentPip=f.permanentPip;q.currentColor=f.baseColor;q.remoteOverride=false;q.hasRolled=true;DraftChanged();});
            UiKit.Button(p,"CurrentPip","本次点数："+(r?.currentPip.ToString()??"未投"),500,y,460,48,()=>{EnsureDebugResults();var q=debugDraft.currentResults.First(x=>x.dieId==d.dieId);q.currentPip=q.currentPip%6+1;q.remoteOverride=true;q.hasRolled=true;DraftChanged();});
            UiKit.Button(p,"CurrentColor","当前物理面颜色："+(r?.currentColor??"未投"),990,y,460,48,()=>{EnsureDebugResults();var q=debugDraft.currentResults.First(x=>x.dieId==d.dieId);var face=d.faces.First(f=>f.faceId==q.currentFaceId);face.baseColor=face.baseColor=="red"?"blue":"red";q.currentColor=face.baseColor;q.hasRolled=true;DraftChanged();});y+=64;
            int locked=debugDraft.orderedLockedIds.IndexOf(d.dieId);
            UiKit.Button(p,"DebugLock",locked>=0?"移出提交":"加入有序提交",10,y,460,48,()=>{EnsureDebugResults();if(!debugDraft.orderedLockedIds.Remove(d.dieId))debugDraft.orderedLockedIds.Add(d.dieId);foreach(var q in debugDraft.currentResults)q.isLocked=debugDraft.orderedLockedIds.Contains(q.dieId);DraftChanged();});
            UiKit.Button(p,"LockEarlier","提交位置 ←",500,y,460,48,()=>{debugDraft.orderedLockedIds.RemoveAt(locked);debugDraft.orderedLockedIds.Insert(locked-1,d.dieId);DraftChanged();},locked>0);
            UiKit.Button(p,"LockLater","提交位置 →",990,y,460,48,()=>{debugDraft.orderedLockedIds.RemoveAt(locked);debugDraft.orderedLockedIds.Insert(locked+1,d.dieId);DraftChanged();},locked>=0&&locked<debugDraft.orderedLockedIds.Count-1);y+=65;
            UiKit.Text(p,"OrderedLocks",string.Join(" → ",debugDraft.orderedLockedIds),10,y,1430,110,25,UiKit.Gold);
        }
        private void DrawDebugLevels(Transform p)
        {
            UiKit.Button(p,"ZeroLevels","全部重置为0级",10,0,700,48,()=>{foreach(var l in debugDraft.handLevels.Concat(debugDraft.colorLevels))l.level=0;DraftChanged();});
            UiKit.Button(p,"MaxLevels","全部设为100级",750,0,700,48,()=>{foreach(var l in debugDraft.handLevels.Concat(debugDraft.colorLevels))l.level=100;DraftChanged();});
            float y=70;foreach(var l in debugDraft.handLevels.Concat(debugDraft.colorLevels)){var level=l;bool color=debugDraft.colorLevels.Contains(level);DebugField(p,"Level"+l.handId,(color?"颜色 · ":"牌型 · ")+NameOf(l.handId),l.level.ToString(),10,y,1000,v=>level.level=DebugInt(v,0,100));UiKit.Button(p,"Down"+l.handId,"−",1040,y+33,180,48,()=>{level.level=Math.Max(0,level.level-1);DraftChanged();});UiKit.Button(p,"Up"+l.handId,"+",1250,y+33,180,48,()=>{level.level=Math.Min(100,level.level+1);DraftChanged();});y+=95;}
        }
        private void DrawDebugResources(Transform p)
        {
            float y=0;
            DebugField(p,"Wallet","金币 0–1,000,000,000",debugDraft.gold.ToString(),10,y,700,v=>debugDraft.gold=DebugLong(v,0,1000000000));
            DebugField(p,"Capacity","提交容量 1–32",debugDraft.capacityOverride.ToString(),750,y,700,v=>debugDraft.capacityOverride=DebugInt(v,1,32));y+=100;
            DebugField(p,"Target","关卡目标（任意精度非负整数）",debugDraft.target,10,y,700,v=>debugDraft.target=DebugScore(v));
            DebugField(p,"StageScore","本关累计分（任意精度非负整数）",debugDraft.score,750,y,700,v=>debugDraft.score=DebugScore(v));y+=100;
            DebugField(p,"RollsLeft","剩余投掷 0–999（-1自动）",debugDraft.rollsRemaining.ToString(),10,y,700,v=>debugDraft.rollsRemaining=DebugInt(v,-1,999));
            DebugField(p,"SubmitsLeft","剩余提交 0–999（-1自动）",debugDraft.remainingSubmissions.ToString(),750,y,700,v=>debugDraft.remainingSubmissions=DebugInt(v,-1,999));y+=100;
            DebugField(p,"StageIndex","关卡 1–24",(debugDraft.stageIndex+1).ToString(),10,y,700,v=>debugDraft.stageIndex=DebugInt(v,1,24)-1);
            DebugField(p,"InventorySlots","道具持有槽 "+Config.initialConsumableSlots+"–"+Config.debug.maxConsumableInventory,debugDraft.consumableSlots.ToString(),750,y,700,v=>debugDraft.consumableSlots=DebugInt(v,Config.initialConsumableSlots,Config.debug.maxConsumableInventory));y+=100;
            DebugField(p,"CtrlShopSlots","控制器货架扩容次数 0–3",debugDraft.controllerOfferPurchases.ToString(),10,y,700,v=>debugDraft.controllerOfferPurchases=DebugInt(v,0,3));
            DebugField(p,"ItemShopSlots","道具货架扩容次数 0–3",debugDraft.consumableOfferPurchases.ToString(),750,y,700,v=>debugDraft.consumableOfferPurchases=DebugInt(v,0,3));y+=100;
            DebugField(p,"ItemDiscount","道具折扣 0–3",debugDraft.itemDiscount.ToString(),10,y,700,v=>debugDraft.itemDiscount=DebugInt(v,0,3));
            DebugField(p,"StageRollBonus","关内额外投掷 0–999",debugDraft.stageRollBonus.ToString(),750,y,700,v=>debugDraft.stageRollBonus=DebugInt(v,0,999));y+=100;
            UiKit.Button(p,"FreeRefresh","免费首刷："+debugDraft.freeFirstRefreshOwned,10,y,460,48,()=>{debugDraft.freeFirstRefreshOwned=!debugDraft.freeFirstRefreshOwned;DraftChanged();});
            UiKit.Button(p,"RiskStage","高压模式："+debugDraft.risk,500,y,460,48,()=>{debugDraft.risk=!debugDraft.risk;DraftChanged();});
            UiKit.Button(p,"RedWave","本手红潮："+debugDraft.redWave,990,y,460,48,()=>{debugDraft.redWave=!debugDraft.redWave;DraftChanged();});y+=65;
            UiKit.Button(p,"LargeBias","本手大数加权："+debugDraft.largeBuff,10,y,700,48,()=>{debugDraft.largeBuff=!debugDraft.largeBuff;DraftChanged();});
            UiKit.Button(p,"SmallBias","本手小数加权："+debugDraft.smallBuff,750,y,700,48,()=>{debugDraft.smallBuff=!debugDraft.smallBuff;DraftChanged();});y+=70;
            UiKit.Text(p,"ItemsDebug","添加局内道具 · "+debugDraft.inventory.Count+" / 32",10,y,1430,45,26,UiKit.Teal);y+=55;
            for(int i=0;i<Config.consumables.Length;i++){var item=Config.consumables[i];UiKit.Button(p,"AddItem"+item.id,item.displayName,(i%4)*365,y+(i/4)*62,350,48,()=>{int serial=NextDebugItemSerial();debugDraft.inventory.Add(new OwnedItem{instanceId="ITEM_"+serial.ToString("D5"),contentId=item.id});debugDraft.consumableSlots=Math.Max(debugDraft.consumableSlots,debugDraft.inventory.Count);DraftChanged();},debugDraft.inventory.Count<32);}y+=140;
            UiKit.Text(p,"RemoveItemTitle","点击移除已添加道具（不产生出售收入）",10,y,1430,40,23,UiKit.Muted);y+=48;
            for(int i=0;i<debugDraft.inventory.Count;i++){var item=debugDraft.inventory[i];UiKit.Button(p,"RemoveItem"+item.instanceId,NameOf(item.contentId)+" ×",(i%4)*365,y+(i/4)*60,350,46,()=>{debugDraft.inventory.Remove(item);DraftChanged();});}
        }
        [Serializable] private sealed class DebugExport
        {
            public string format="DiceCraft-debug-v1",configVersion,exportUtc,sourceRunId,runId;
            public bool isDebug;
            public DebugScene scene;
            public ScorePreview preview;
            public SubmissionRecord lastSubmission;
            public CommandReceipt lastReceipt;
        }
        private void DrawDebugReproduction(Transform p)
        {
            DebugField(p,"DebugSeed","种子 uint32",debugDraft.seed.ToString(),10,0,700,v=>debugDraft.seed=uint.Parse(v,CultureInfo.InvariantCulture));
            DebugField(p,"DiceRng","骰子 RNG 状态",debugDraft.diceRngState.ToString(),750,0,700,v=>debugDraft.diceRngState=uint.Parse(v,CultureInfo.InvariantCulture));
            DebugField(p,"ShopRng","商店 RNG 状态",debugDraft.shopRngState.ToString(),10,100,700,v=>debugDraft.shopRngState=uint.Parse(v,CultureInfo.InvariantCulture));
            DebugField(p,"EffectRng","效果 RNG 状态",debugDraft.effectRngState.ToString(),750,100,700,v=>debugDraft.effectRngState=uint.Parse(v,CultureInfo.InvariantCulture));
            string[] handIds=new[]{"","scatter"}.Concat(Config.handDefinitions.Select(h=>h.id)).ToArray();
            UiKit.Button(p,"PreviousPrimary","上一主牌型："+(string.IsNullOrEmpty(debugDraft.previousPrimaryHandId)?"无历史":NameOf(debugDraft.previousPrimaryHandId))+" >",10,205,1440,48,()=>{int index=Array.IndexOf(handIds,debugDraft.previousPrimaryHandId??"");debugDraft.previousPrimaryHandId=handIds[(index+1)%handIds.Length];DraftChanged();});
            DebugField(p,"ScenePath","本地场景 JSON 路径",debugJsonPath,10,275,1440,v=>debugJsonPath=v);
            UiKit.Button(p,"ExportDebug","导出当前已应用场景与回执",10,375,460,50,ExportDebugScene);
            UiKit.Button(p,"ImportDebug","载入 JSON 为候选",500,375,460,50,ImportDebugScene);
            UiKit.Button(p,"CopyDebug","复制复现摘要",990,375,460,50,()=>{GUIUtility.systemCopyBuffer=JsonUtility.ToJson(CurrentDebugExport(),true);debugMessage="已复制场景与计分/RNG回执。";ShowDebugPanel();});
            UiKit.Text(p,"BranchesHelp","下一次随机分支：指定后仍消耗正常抽样，使用一次即清除；原票值与覆盖值写入回执。\nC53 绑定真实状态 owner；C42 绑定实际效果 emitter；未指定则正常随机。",10,450,1430,100,24,UiKit.Muted);
            float y=560;
            AbilitySource[] sources;
            try{sources=ControllerRules.ResolveSources(debugDraft.controllers,debugDraft.mimicBindings);}catch(ArgumentException){UiKit.Text(p,"InvalidBinding","复制绑定存在环或非法目标；应用前需修正。",10,y,1430,65,24,UiKit.Red);return;}
            foreach(var source in sources.Where(s=>s.abilityId=="C53"||s.abilityId=="C45").GroupBy(s=>s.stateOwnerId).Select(g=>g.First()))
            {string kind=source.abilityId,id=source.stateOwnerId;DrawBranchButton(p,kind,id,kind=="C53"?new[]{"2","4","6","8","10","12","14","16","1"}:new[]{"red","blue"},y);y+=64;}
            foreach(var source in sources.Where(s=>s.abilityId=="C42")){DrawBranchButton(p,"C42",source.emitterId,new[]{"break","keep"},y);y+=64;}
            foreach(var die in debugDraft.dice.Where(d=>d.isGlass)){DrawBranchButton(p,"Glass",die.dieId,new[]{"break","keep"},y);y+=64;}
            UiKit.Text(p,"OverrideCount","指定分支 "+debugDraft.randomOverrides.Count+" 项；查看预览不会消耗随机数。",10,y+5,1430,60,24,UiKit.Gold);
        }
        private void DrawBranchButton(Transform p,string kind,string id,string[] options,float y)
        {
            var old=debugDraft.randomOverrides.FirstOrDefault(x=>x.kind==kind&&x.sourceId==id);string value=old?.value;
            string text=kind=="C53"&&value!=null?"×"+(int.Parse(value)/2m).ToString("0.#"):value??"随机";
            UiKit.Button(p,"Branch"+kind+id,kind+" / "+id+" ："+text+" >",10,y,1440,50,()=>{debugDraft.randomOverrides.RemoveAll(x=>x.kind==kind&&x.sourceId==id);int index=Array.IndexOf(options,value);if(index+1<options.Length)debugDraft.randomOverrides.Add(new DebugRandomOverride{kind=kind,sourceId=id,value=options[index+1]});DraftChanged();});
        }
        private int NextDebugItemSerial()
        {
            int high=debugDraft.controllers.Select(c=>c.acquiredSerial).Concat(debugDraft.inventory.Select(i=>int.Parse(i.instanceId.Substring(5),CultureInfo.InvariantCulture))).DefaultIfEmpty().Max();
            return Math.Max(ViewState.nextItemSerial,checked(high+1));
        }
        private DebugExport CurrentDebugExport()
        {
            var s=Session.State;return new DebugExport{configVersion=Config.configVersion,exportUtc=DateTime.UtcNow.ToString("O"),sourceRunId=s.sourceRunId,runId=s.runId,isDebug=s.isDebug,scene=DebugScene.FromState(s),preview=Session.PreviewScoreRange(),lastSubmission=s.lastSubmission,lastReceipt=s.processedReceipts.LastOrDefault()};
        }
        private void ExportDebugScene()
        {
            try{string full=Path.GetFullPath(debugJsonPath);Directory.CreateDirectory(Path.GetDirectoryName(full));File.WriteAllText(full,JsonUtility.ToJson(CurrentDebugExport(),true));debugMessage="已导出："+full;}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){debugMessage="导出失败："+e.Message;}ShowDebugPanel();
        }
        private void ImportDebugScene()
        {
            try
            {
                var info=new FileInfo(debugJsonPath);if(info.Length>4*1024*1024)throw new IOException("场景文件超过4 MiB。");string json=File.ReadAllText(info.FullName);
                var envelope=JsonUtility.FromJson<DebugExport>(json);var imported=envelope?.scene??JsonUtility.FromJson<DebugScene>(json);
                if(imported==null||imported.configVersion!=Config.configVersion||imported.dice==null||imported.controllers==null||imported.inventory==null||imported.handLevels==null||imported.colorLevels==null||imported.dice.Count<1||imported.dice.Count>32||imported.controllers.Count>32)throw new ArgumentException("内容或版本不合法。");
                // Run the exact transaction in memory before exposing a loaded candidate. It cannot touch either save slot.
                var validator=GameSession.CreateDebugWorkspace(Config,new MemorySaveStore(),Session.State);
                var result=validator.Execute(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=imported,commandId=Guid.NewGuid().ToString("N"),expectedActionSequence=validator.ActionSequence});
                if(!result.accepted)throw new ArgumentException(result.error);
                debugDraft=imported;debugRaw.Clear();debugErrors.Clear();debugSelectedController=imported.controllers.FirstOrDefault()?.instanceId;debugSelectedDie=imported.dice[0].dieId;debugMessage="已载入候选，点击应用后才写入调试存档。";
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is FormatException){debugMessage="导入失败，保留原候选："+e.Message;}ShowDebugPanel();
        }
    }
}
