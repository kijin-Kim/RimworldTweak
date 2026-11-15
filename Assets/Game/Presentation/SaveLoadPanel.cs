using System;
using UnityEngine;
using UnityEngine.UI;
namespace RimworldTweak.Presentation
{
    public sealed class SaveLoadPanel
    {
        public RectTransform Root { get; private set; }
        public Button SaveButton { get; private set; }
        public Button LoadButton { get; private set; }
        public Button ConfirmButton { get; private set; }
        public Button CancelButton { get; private set; }
        public Text InfoText { get; private set; }
        private RectTransform confirmation;
        private Text confirmationText;
        private Font font;
        public void Build(Transform parent,Font textFont,Action onSave,Action onLoad,Action onConfirm,Action onCancel)
        {
            font=textFont;Root=Rect("Save and load",parent,0,0,208,36);
            SaveButton=Button("저장 덮어쓰기",Root,0,0,120,onSave);
            LoadButton=Button("불러오기",Root,128,0,80,onLoad);
            confirmation=Rect("Load confirmation",Root,-620,-120,700,200);
            var background=confirmation.gameObject.AddComponent<Image>();background.color=new Color(.07f,.11f,.13f,.99f);background.raycastTarget=true;
            confirmationText=Label("Load details",confirmation,20,66,660,116,16);InfoText=confirmationText;
            ConfirmButton=Button("현재 진행 교체",confirmation,20,16,220,onConfirm);
            CancelButton=Button("취소",confirmation,254,16,140,onCancel);
            Refresh(string.Empty,string.Empty,false,false);
        }
        public void Refresh(string path,string message,bool confirming,bool busy)
        {
            SaveButton.interactable=LoadButton.interactable=!busy&&!confirming;
            ConfirmButton.interactable=CancelButton.interactable=!busy;
            confirmation.gameObject.SetActive(confirming);
            confirmationText.text="저장된 정착지로 현재 진행을 교체합니다.\n저장하지 않은 현재 진행은 사라집니다.\n저장 파일: "+path;
            // Runner supplies a localized application message, never raw file content.
            var display=message??string.Empty;
            if(!string.IsNullOrEmpty(display))InfoText.text+="\n"+display;
        }
        private Button Button(string text,Transform parent,float x,float y,float width,Action action)
        {
            var rect=Rect(text,parent,x,y,width,34);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.19f,.29f,.31f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label("Label",rect,0,0,width,34,14);label.text=text;label.alignment=TextAnchor.MiddleCenter;
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
