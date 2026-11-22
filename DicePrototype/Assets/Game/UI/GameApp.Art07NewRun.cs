using System;
using System.Collections;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private int newRunPreviewGeneration07;

        // S02 owns only a view of freshly copied standard faces. No current result or Session is created.
        private void OpenNewRunPanel07()
        {
            bool fresh=PrepareModalMotion07("new-run07");
            ShowMenuTray17(false);
            int generation=++newRunPreviewGeneration07;
            modal=true;diceSheet07=true;UiKit.Clear(overlay);table.Hide();main.gameObject.SetActive(false);
            var p=UiKit.Rect(overlay,"Dialog",440,220,1040,640);
            var scrim=new Color(.015f,.03f,.035f,.35f);
            // The paper, aperture and scrim travel together during the 16px entrance.
            // Extra outside extent prevents a moving edge exposing an undimmed screen strip.
            UiKit.Box(p,"ScrimTop",-504,-284,2048,436,scrim,true);
            UiKit.Box(p,"ScrimBottom",-504,384,2048,540,scrim,true);
            UiKit.Box(p,"ScrimLeft",-504,152,536,232,scrim,true);
            UiKit.Box(p,"ScrimRight",392,152,1152,232,scrim,true);
            UiKit.Box(p,"PaperTop",0,0,1040,152,UiKit.Paper,true);
            UiKit.Box(p,"PaperBottom",0,384,1040,256,UiKit.Paper,true);
            UiKit.Box(p,"PaperLeft",0,152,32,232,UiKit.Paper,true);
            UiKit.Box(p,"PaperRight",392,152,648,232,UiKit.Paper,true);
            UiKit.Outline(p,"PaperFrame",0,0,1040,640,UiKit.Ink,2);
            UiKit.Outline(p,"DiceTrayFrame",30,150,364,236,Color.Lerp(UiKit.Ink,UiKit.Paper,.5f),1);
            UiKit.Box(p,"DiceTrayRaycast",32,152,360,232,Color.clear,true);
            var title=UiKit.Text(p,"DialogTitle","新的一局",24,4,880,88,40,UiKit.Ink);title.fontStyle=FontStyles.Bold;UiKit.Role(title,TextRole.ScreenTitle);
            ReceiptRule071(p,"HeadingRule",24,92,992);
            var close=UiKit.FlatButton(p,"CloseDialog","×",952,12,64,64,CloseModal);
            close.GetComponentInChildren<TextMeshProUGUI>().color=UiKit.Ink;
            UiKit.Text(p,"InitialDiceLabel","初始 "+Config.initialDiceCount+" 颗骰子",32,96,360,48,20,UiKit.Ink);
            UiKit.Text(p,"StartRules","每手最多掷 "+Config.totalRollsPerHand+" 次\n每关 "+Config.submissionsPerStage+" 次提交\n起始金币 "+Config.initialGold,32,410,360,190,24,UiKit.Ink);
            ReceiptRule071(p,"StartRulesRule",32,400,360);
            var seedLabel=UiKit.Text(p,"SeedLabel","种子（可选）",464,100,544,56,20,UiKit.Ink);seedLabel.fontStyle=FontStyles.Bold;
            seedField=UiKit.Input(p,"SeedInput","留空则随机","",464,164,456,72);
            var random=UiKit.Button(p,"RandomSeed","",936,164,72,72,()=>
            {
                var bytes=new byte[4];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(bytes);
                seedField.text=BitConverter.ToUInt32(bytes,0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            });
            var icon=UiKit.Box(random.transform,"RandomIcon",20,18,32,32,UiKit.Ink).GetComponent<UnityEngine.UI.Image>();
            icon.sprite=UiKit.Template("ui_icon_refresh");icon.raycastTarget=false;
            UiKit.Text(p,"SeedHelp16","同一个种子会掷出同样的骰子和商店",464,244,544,40,20,LabelOn11(p.Find("PaperRight") as RectTransform,PaperMuted071));
            var details=UiKit.Scroll(p,"NewRunDetails",464,292,544,244,244);
            FillNewRunDetails07(details,"");
            Primary16(p,"StartNew",ViewState==null?"开始":"放弃当前进度并开始",464,552,544,64,()=>
            {
                string value=seedField.text;uint parsed;
                if(!string.IsNullOrWhiteSpace(value)&&!GameRng.TryParseSeed(value,out parsed))
                {
                    FillNewRunDetails07(details,SeedError16);
                    seedField.ActivateInputField();return;
                }
                if(Session?.HasPendingSave==true){SaveFailure();return;}
                CloseModal();StartNew(value);
            });
            FadeModal07(p,fresh,.18f,.10f);
            var reveal=UiKit.Box(p,"InitialDiceReveal",32,152,360,232,UiKit.Felt).GetComponent<UnityEngine.UI.Image>();
            var revealGroup=UiKit.Component<CanvasGroup>(reveal.gameObject);
            revealGroup.ignoreParentGroups=true;revealGroup.blocksRaycasts=false;revealGroup.interactable=false;
            var dice=new DieInstance[Config.initialDiceCount];
            for(int i=0;i<dice.Length;i++)dice[i]=DiceFactory.CreateStandard(Config,i+1);
            StartCoroutine(NewRunPreview07(p,p.GetComponent<Art07PanelMotion>(),revealGroup,dice,generation));
        }

        private void FillNewRunDetails07(RectTransform content,string error)
        {
            UiKit.Clear(content);float y=0;const float width=526;
            AddNewRunText07(content,"SeedError",error,ref y,width,UiKit.Danger);
            string rules=$"共 {Config.stages.Length} 关（{(Config.stages.Length+2)/3} 章）。进度会自动保存。";
            string summary=ViewState==null?rules:
                $"当前存档（第 {ViewState.stage.stageIndex+1} 关 · {PlayerNumberFormat.Gold(ViewState.gold)} 金币）会被覆盖。\n关闭此窗口则保留原进度。";
            AddNewRunText07(content,"Overwrite",summary,ref y,width,UiKit.Ink);
            if(ViewState!=null)AddNewRunText07(content,"RulesSummary",rules,ref y,width,UiKit.Ink);
            content.sizeDelta=new Vector2(content.sizeDelta.x,Mathf.Max(244,y));
            var scroll=content.GetComponentInParent<UnityEngine.UI.ScrollRect>();scroll.StopMovement();
            scroll.verticalNormalizedPosition=1;content.anchoredPosition=Vector2.zero;
        }

        private static void AddNewRunText07(RectTransform parent,string id,string value,ref float y,float width,Color color)
        {
            var text=UiKit.Text(parent,id,value,0,y,width,64,20,color);
            if(string.IsNullOrEmpty(value)){text.gameObject.SetActive(false);return;}
            float height=Mathf.Ceil(text.GetPreferredValues(value,width,Mathf.Infinity).y)+8;
            text.rectTransform.sizeDelta=new Vector2(width,height);y+=height+12;
        }

        private IEnumerator NewRunPreview07(RectTransform panel,Art07PanelMotion motion,CanvasGroup reveal,DieInstance[] dice,int generation)
        {
            var centers=new[]{new Vector2(92,210),new Vector2(212,210),new Vector2(332,210),new Vector2(152,326),new Vector2(272,326)};
            var rotations=new[]{Quaternion.Euler(0,-12,0),Quaternion.Euler(0,16,0),Quaternion.Euler(0,-6,0),Quaternion.Euler(0,12,0),Quaternion.Euler(0,-16,0)};
            var priorMatrix=Matrix4x4.zero;float priorOffset=float.NaN;
            while(modal&&modalIdentity07=="new-run07"&&generation==newRunPreviewGeneration07&&panel!=null&&panel.gameObject.activeInHierarchy)
            {
                float offset=reduced?0:16*(1-motion.Alpha);
                panel.anchoredPosition=new Vector2(440,-220-offset);reveal.alpha=1-motion.Alpha;
                var matrix=panel.localToWorldMatrix;
                var first=table.GetFaceOrientation("$static-preview:new-run-0");
                if(offset!=priorOffset||matrix!=priorMatrix||first==null||!first.gameObject.activeInHierarchy)
                {
                    for(int i=0;i<dice.Length;i++)
                    {
                        Vector3 local=table.designRoot.InverseTransformPoint(panel.TransformPoint(new Vector3(centers[i].x,-centers[i].y,0)));
                        PlaceNewRunDie07("new-run-"+i,dice[i],new Vector2(local.x,-local.y),rotations[i]);
                    }
                    priorOffset=offset;priorMatrix=matrix;
                }
                yield return null;
            }
        }

        private void PlaceNewRunDie07(string slot,DieInstance die,Vector2 desired,Quaternion rotation)
        {
            // Fit the projected real mesh, then center it. A fixed plane intercept alone shifts
            // tall geometry toward the top of the aperture. This never changes face data/rotation.
            float size=.85f;Vector2 center=desired;
            for(int pass=0;pass<3;pass++)
            {
                table.ShowStaticPreview07(slot,die,center,rotation,size);
                Transform pivot=table.GetFaceOrientation("$static-preview:"+slot);
                var low=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                var high=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                foreach(var mesh in pivot.GetComponentsInChildren<MeshFilter>(false))
                {
                    if(mesh.sharedMesh==null)continue;Bounds b=mesh.sharedMesh.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        Vector3 v=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        Vector2 point=ScreenToDesign(table.viewCamera.WorldToScreenPoint(mesh.transform.TransformPoint(v)));
                        low=Vector2.Min(low,point);high=Vector2.Max(high,point);
                    }
                }
                Vector2 span=high-low;float fit=Mathf.Min(1,Mathf.Min(96/Mathf.Max(1,span.x),96/Mathf.Max(1,span.y)));
                size*=fit;center+=desired-(low+high)*.5f;
            }
            table.ShowStaticPreview07(slot,die,center,rotation,size);
        }
    }
}
