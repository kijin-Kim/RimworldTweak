using System;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;
namespace RimworldTweak.Presentation
{
    public sealed class CookingPanel
    {
        public RectTransform Root { get; private set; }
        public Button EnabledButton { get; private set; }
        public Button TargetMinusButton { get; private set; }
        public Button TargetPlusButton { get; private set; }
        public Button DeleteButton { get; private set; }
        public Button CreateButton { get; private set; }
        public Text DetailText { get; private set; }
        private Font font;
        private CookingOrderState displayedOrder;
        public void Initialize(Transform parent,Font textFont,Action<bool,int> setOrder,Action deleteOrder,Action createOrder)
        {
            font=textFont;Root=Rect("Cooking panel",parent,0,0,708,160);
            EnabledButton=Button("조리 가동",0,120,132,()=> {if(displayedOrder!=null) setOrder(!displayedOrder.Enabled,displayedOrder.TargetMeals);});
            TargetMinusButton=Button("목표 -1",140,120,112,()=> {if(displayedOrder!=null) setOrder(displayedOrder.Enabled,Math.Max(0,displayedOrder.TargetMeals-1));});
            TargetPlusButton=Button("목표 +1",260,120,112,()=> {if(displayedOrder!=null) setOrder(displayedOrder.Enabled,Math.Min(99,displayedOrder.TargetMeals+1));});
            DeleteButton=Button("주문 삭제",380,120,144,deleteOrder);
            CreateButton=Button("주문 다시 만들기",532,120,170,createOrder);
            DetailText=Label("Cooking details",Root,0,0,702,112,15);
            Refresh(null,null,0,string.Empty);
        }
        public void Refresh(CookingOrderState order,CookingOutputState output,int mealTotal,string error)
        {
            displayedOrder=order!=null&&order.Exists?order:null;
            var exists=displayedOrder!=null;
            EnabledButton.interactable=DeleteButton.interactable=exists;
            TargetMinusButton.interactable=exists&&order.TargetMeals>0;
            TargetPlusButton.interactable=exists&&order.TargetMeals<99;
            CreateButton.interactable=!exists&&output!=null&&output.Exists;
            EnabledButton.GetComponentInChildren<Text>().text=exists&&order.Enabled?"조리 중지":"조리 가동";
            var text="완공된 조리대를 선택하세요 · 식재료 2 → 식사 1\n목표 수량은 바닥·저장·운반·출력의 식사를 모두 포함합니다";
            if(exists)
                text="조리대 #"+order.StoveId+" · "+(order.Enabled?"가동":"중지")+" · 목표 "+order.TargetMeals+" / 현재 식사 "+mealTotal+
                    "\n투입 "+order.InputIngredients+"/2 · 진행 "+order.Progress+"/40 · 출력 "+(output?.Amount??0)+"/50"+
                    "\n담당 "+(order.OwnerId.HasValue?"정착민 "+order.OwnerId.Value:"없음")+" · "+Reason(order.Reason);
            else if(output!=null&&output.Exists) text="조리 주문 없음 · 다시 만들기는 중지/목표 12로 시작\n완성 출력 "+output.Amount+"/50 · 삭제한 주문의 완성 식사는 유지됩니다";
            if(!string.IsNullOrEmpty(error)) text+="\n주문을 변경할 수 없습니다. 조리대와 목표 범위를 확인하세요";
            DetailText.text=text;
        }
        private static string Reason(WorkReasonCode reason)
        { switch(reason) { case WorkReasonCode.None:return "진행 가능";case WorkReasonCode.Disabled:return "가동 중지";
            case WorkReasonCode.TargetReached:return "목표 도달";case WorkReasonCode.OutputFull:return "출력 공간 없음";
            case WorkReasonCode.MissingMaterials:return "식재료 부족";case WorkReasonCode.TargetReserved:return "대상 점유 중";
            case WorkReasonCode.Unreachable:return "접근 불가";case WorkReasonCode.NoWorker:return "담당자 없음";default:return "작업 대기"; } }
        private Button Button(string text,float x,float y,float width,Action action)
        {
            var rect=Rect(text,Root,x,y,width,34);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.19f,.29f,.31f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label("Label",rect,0,0,width,34,15);label.text=text;label.alignment=TextAnchor.MiddleCenter;
            button.onClick.AddListener(()=>action());return button;
        }
        private Text Label(string name,Transform parent,float x,float y,float width,float height,int size)
        {
            var rect=Rect(name,parent,x,y,width,height);var text=rect.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;
            text.color=new Color(.88f,.94f,.95f);text.raycastTarget=false;text.alignment=TextAnchor.UpperLeft;return text;
        }
        private static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
    }
}
