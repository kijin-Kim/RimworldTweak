using UnityEngine;
using UnityEngine.UI;
namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        public SaveLoadPanel SaveLoadPanel {get;private set;}
        private RectTransform saveModalBlocker;
        private void BuildSaveUI()
        {
            saveModalBlocker=Panel("Save confirmation blocker",uiRoot.transform,0,0,1280,800,new Color(0,0,0,.65f));
            saveModalBlocker.GetComponent<Image>().raycastTarget=true;
            SaveLoadPanel=new SaveLoadPanel();
            SaveLoadPanel.Build(uiRoot.transform,font,()=>{Runner.SaveNow();RefreshNow();},()=>{Runner.BeginLoad();RefreshNow();},
                ()=>{Runner.ConfirmLoad();RefreshNow();},()=>{Runner.CancelLoad();RefreshNow();});
            SaveLoadPanel.Root.anchoredPosition=new Vector2(1022,202);
            saveModalBlocker.gameObject.SetActive(false);
        }
        private void RefreshSaveUI()
        {
            if(SaveLoadPanel==null)return;
            SaveLoadPanel.Refresh(Runner.SavePath,Runner.SaveMessage,Runner.IsLoadConfirmationOpen,Runner.SaveBusy);
            saveModalBlocker.gameObject.SetActive(Runner.IsLoadConfirmationOpen);
            if(!string.IsNullOrEmpty(Runner.SaveMessage))feedbackText.text=Runner.SaveMessage;
        }
    }
}
