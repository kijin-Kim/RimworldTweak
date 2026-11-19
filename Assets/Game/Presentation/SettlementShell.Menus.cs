using System;
using System.Linq;
using RimworldTweak.Runtime;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementShell
    {
        private RectTransform menuRoot, menuFrame, menuContent, menuActions;
        private RectTransform contextMenu;
        public bool IsContextMenuOpen => contextMenu!=null&&contextMenu.gameObject.activeSelf;

        private void CloseContextMenu()
        {
            if(contextMenu==null)return;
            contextMenu.gameObject.SetActive(false);
            Clear(contextMenu);
        }
        private void ShowContextMenu(RimworldTweak.Simulation.GridPosition position)
        {
            CloseContextMenu();
            if(contextMenu==null)
            {
                contextMenu=Box("Context actions",canvasRoot);
                var layout=contextMenu.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding=new RectOffset(8,8,8,8);layout.spacing=6;
                layout.childControlWidth=true;layout.childControlHeight=true;
                layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            }
            contextMenu.gameObject.SetActive(true);contextMenu.SetAsLastSibling();
            var count=0;
            Action<string,string,Action> add=(key,label,action)=>{
                RowButton(contextMenu,"context."+key,label,()=>{CloseContextMenu();action();},36);
                count++;
            };
            var world=Runner.Session.World;
            if (SelectedPawnId.HasValue)
            {
                var drafted = world.Colonists.Count(pawn => selectedPawns.Contains(pawn.Id) && pawn.IsDrafted);
                add("move", drafted == 0 ? "소집 후 이동 가능" :
                    drafted == selectedPawns.Count ? "선택한 정착민 이동" : $"소집된 {drafted}명 이동", () => MoveSelectedPawns(position));
                buttons["context.move"].interactable = drafted > 0;
            }
            void Direct(string key,string label,Func<int,bool> command)
            {
                if(selectedPawns.Count==1)
                    add(key,label,()=>ReportDirect(command(selectedPawns.First()),label));
                else
                {
                    add(key,"정착민 한 명을 선택해",()=>{});
                    buttons["context."+key].interactable=false;
                }
            }
            foreach(var patient in world.Colonists.Where(p=>p.IsDowned && p.Position==position))
                Direct("rescue."+patient.Id,"정착민 "+patient.Id+" 구조",worker=>Runner.Rescue(worker,patient.Id));
            foreach(var id in ObjectsAt(position))
            {
                var target=id;
                var weapon=world.Weapons.FirstOrDefault(w=>w.Id==id&&!w.HolderId.HasValue);
                if(weapon!=null)
                {
                    Direct("equip."+id,WeaponName(weapon)+" 장착",pawn=>Runner.Equip(pawn,target));
                    continue;
                }
                add("select."+id,AlertTargetName(id)+" 선택",()=>SelectObject(target,position));
                var node=world.Gatherables.FirstOrDefault(n=>n.Id==id&&!n.Depleted);
                if(node!=null)add("gather."+id,node.Designated?"채집 지정 취소":"채집 지정",()=>Report(Runner.DesignateGather(target,!node.Designated),"채집 지정을 변경했어."));
                var blueprint=world.Blueprints.FirstOrDefault(b=>b.Id==id&&b.Exists);
                if(blueprint!=null)add("cancel."+id,"건설 취소",()=>Report(Runner.CancelBlueprint(target),"건설을 취소했어."));
                var building=world.Buildings.FirstOrDefault(b=>b.Id==id&&b.Exists);
                if(node?.Designated==true && node.Kind==GatherKind.Tree || blueprint!=null || building?.RepairDesignated==true)
                    Direct("priority."+id,(blueprint!=null?"건설":building!=null?"수리":"벌목")+" 우선 작업",pawn=>Runner.Prioritize(pawn,target));
                if(building!=null)add("demolish."+id,building.DemolitionDesignated?"철거 취소":"철거 지정",
                    ()=>Report(Runner.SetDemolition(target,!building.DemolitionDesignated),"철거 지정을 변경했어."));
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot,Map.GridToScreen(position),null,out var point);
            var height=count*42+16;var width=Mathf.Min(280,canvasRoot.rect.width-16);
            Place(contextMenu,Mathf.Clamp(point.x-canvasRoot.rect.xMin,8,canvasRoot.rect.width-width-8),
                Mathf.Clamp(canvasRoot.rect.yMax-point.y,8,Mathf.Max(8,canvasRoot.rect.height-height-8)),width,height);
            MapInput.CancelGesture();
        }

        private Text menuTitle, menuStatus;
        private float menuPreferredHeight;
        private bool pauseMenuLayout;
        private Action menuBack;
        public InputField SaveNameInput { get; private set; }
        public bool ExitRequested { get; private set; }
        public bool SuppressProcessExit { get; set; }
        public Action ReturnToTitle { get; set; }

        private void OpenMenu(string title, Action back, bool pauseLayout = false, float preferredHeight = 780)
        {
            CloseContextMenu();
            MapInput?.CancelGesture();
            if(menuRoot==null)
            {
                menuRoot=Box("Modal blocker",canvasRoot,new Color(.025f,.035f,.04f,.87f));Stretch(menuRoot);
                menuFrame=Box("Menu",menuRoot);
                menuTitle = Label("Menu title", menuFrame, 26);
                menuTitle.color = Accent;
                menuTitle.alignment = TextAnchor.MiddleLeft;
                menuContent=Scroll("Menu scroll",menuFrame);
                menuStatus = Label("Menu status", menuFrame, 16);
                menuStatus.alignment = TextAnchor.MiddleLeft;
                menuStatus.color = new Color(1f, .66f, .58f);
                menuActions = WorkRow(menuFrame);
            }
            pauseMenuLayout = pauseLayout;
            menuPreferredHeight = preferredHeight;
            menuRoot.gameObject.SetActive(true);
            menuRoot.SetAsLastSibling();
            menuRoot.GetComponent<Image>().color = new Color(.025f,.035f,.04f,pauseLayout ? .42f : .87f);
            Runner.SetUiBlocked(true);
            Clear(menuContent);
            Clear(menuActions);
            SaveNameInput = null;
            menuBack = back;
            menuTitle.text = title;
            menuStatus.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(null);
            var scroll = menuContent.GetComponentInParent<ScrollRect>();
            scroll.StopMovement();
            menuContent.anchoredPosition = Vector2.zero;
            LayoutMenu();
        }
        private void LayoutMenu()
        {
            if (menuFrame == null) return;
            var size = canvasRoot.rect.size;
            var width = Mathf.Min(pauseMenuLayout ? 320 : 720, size.x - 32);
            var height = Mathf.Min(menuPreferredHeight, size.y - (pauseMenuLayout ? 64 : 40));
            var x = pauseMenuLayout ? size.x - width - 8 : (size.x - width) / 2;
            var y = pauseMenuLayout ? size.y - height - 44 : (size.y - height) / 2;
            Place(menuFrame, x, y, width, height);
            Place(menuTitle.rectTransform, 12, 4, width - 24, 46);
            var scroll = (RectTransform)menuContent.GetComponentInParent<ScrollRect>().transform;
            var statusHeight = string.IsNullOrEmpty(menuStatus.text) ? 0 : 58;
            menuStatus.gameObject.SetActive(statusHeight > 0);
            Place(scroll, 10, 54, width - 20, height - 126 - statusHeight);
            Place(menuStatus.rectTransform, 12, height - 70 - statusHeight, width - 24, statusHeight);
            Place(menuActions, 10, height - 66, width - 20, 56);
        }
        public void CloseMenu()
        {
            if(!HasSettlement)return;
            menuRoot?.gameObject.SetActive(false);Runner.SetUiBlocked(false);menuBack=null;
            EventSystem.current?.SetSelectedGameObject(null);MapInput?.CancelGesture();
        }
        public void CancelMenuLevel(){if(menuBack!=null)menuBack();else if(HasSettlement)CloseMenu();}
        public void ShowMainMenu()
        {
            OpenMenu("정착지",null);
            RowLabel(menuContent,"작은 정착지를 만들고, 역할을 나누고, 다음 날을 준비해.",64);
            var recent=Runner.Settings.RecentSlotId;
            var listing=Runner.Slots.ListSlots();
            var continueButton=RowButton(menuContent,"continue","이어하기",()=>{
                if(string.IsNullOrEmpty(recent))
                {
                    var candidate=Runner.Slots.ListSlots().Slots.FirstOrDefault(x=>x.IsValid);
                    Feedback="이어하기 기록이 없어. 최근 저장 후보를 확인하고 선택해줘.";
                    ShowLoadMenu(candidate?.SlotId);
                    return;
                }
                var slot=Runner.Slots.ListSlots().Slots.FirstOrDefault(x=>x.SlotId==recent);
                if(slot==null||!slot.IsValid){Feedback="최근 저장을 읽을 수 없어. 목록에서 확인해줘.";ShowLoadMenu();return;}
                TryLoad(slot.SlotId);
            });
            continueButton.interactable=!string.IsNullOrEmpty(recent)||listing.Slots.Any(x=>x.IsValid);
            if(!continueButton.interactable)RowLabel(menuContent,"이어갈 저장이 없어. 새 정착지를 시작해줘.",60,16);
            RowButton(menuContent,"new","새 정착지",()=>{Runner.StartNewSettlement();HasSettlement=true;CloseMenu();OpenTab(string.Empty);Refresh();});
            RowButton(menuContent,"load.menu","불러오기",ShowLoadMenu);
            RowButton(menuContent,"settings","화면 설정",()=>ShowSettings(ShowMainMenu));
            RowButton(menuContent,"exit","종료",RequestExit);
            if(!listing.Success)RowLabel(menuContent,SimulationRunner.SlotError(listing.ErrorCode),75);
            if(!string.IsNullOrEmpty(Runner.SettingsWarning))RowLabel(menuContent,Runner.SettingsWarning,90);
        }
        public void ShowPauseMenu()
        {
            if(!HasSettlement){ShowMainMenu();return;}
            OpenMenu("게임 메뉴",CloseMenu, pauseLayout: true, preferredHeight: 434);
            FooterButton("resume","게임으로 돌아가기",CloseMenu);
            RowButton(menuContent,"save.menu","저장",()=>ShowSaveMenu());
            RowButton(menuContent,"load.menu","불러오기",ShowLoadMenu);
            RowButton(menuContent,"settings","화면 설정",()=>ShowSettings(ShowPauseMenu));
            RowButton(menuContent,"new","새 정착지",()=>ConfirmTransition("새 정착지를 시작할까?",()=>{Runner.StartNewSettlement();HasSettlement=true;CloseMenu();Refresh();}));
            RowButton(menuContent,"main","메인 메뉴",()=>ConfirmTransition("메인 메뉴로 돌아갈까?",()=>{
                if(ReturnToTitle!=null){ReturnToTitle();return;}
                HasSettlement=false;ClearSelection();CancelTool();ShowMainMenu();
            }));
            RowButton(menuContent,"exit","종료",()=>ConfirmTransition("게임을 종료할까?",RequestExit));
        }
        private void ConfirmTransition(string title, Action proceed)
        {
            OpenMenu(title,ShowPauseMenu, preferredHeight: 308);
            RowLabel(menuContent,"저장하지 않은 현재 진행은 사라져. 저장하거나 취소할 수 있어.",80);
            FooterButton("transition.save","저장 후 계속",()=>ShowSaveMenu(proceed));
            FooterButton("transition.discard","저장하지 않고 계속",proceed);
            FooterButton("cancel","취소",ShowPauseMenu);
        }
        public void ShowLoadMenu()
        {
            ShowLoadMenu(null);
        }
        private void ShowLoadMenu(string suggestedSlotId)
        {
            OpenMenu("불러오기",()=>{if(HasSettlement)ShowPauseMenu();else ShowMainMenu();});
            if(!string.IsNullOrEmpty(Feedback))RowLabel(menuContent,Feedback,80);
            var list=Runner.Slots.ListSlots();
            if(!list.Success)RowLabel(menuContent,SimulationRunner.SlotError(list.ErrorCode),80);
            if(list.Slots.Count==0)RowLabel(menuContent,"저장된 정착지가 없어.",55);
            foreach(var slot in list.Slots)
            {
                var entry=slot;
                var suggested=entry.SlotId==suggestedSlotId;
                var caption=(suggested?"[최근 저장 후보] ":string.Empty)+SlotCaption(entry);
                var b=RowButton(menuContent,"load."+entry.SlotId,caption,()=>{
                    if(!HasSettlement&&!suggested){TryLoad(entry.SlotId);return;}
                    OpenMenu(HasSettlement?"현재 진행을 교체할까?":"이 저장에서 이어갈까?",ShowLoadMenu, preferredHeight: 308);
                    RowLabel(menuContent,SlotCaption(entry)+(HasSettlement?"\n저장하지 않은 현재 진행은 사라져.":"\n이 저장에서 이어갈까?"),140);
                    FooterButton("load.confirm","선택한 저장 불러오기",()=>TryLoad(entry.SlotId));
                    FooterButton("cancel","취소",ShowLoadMenu);
                },90);
                b.interactable=entry.IsValid;
            }
            FooterButton("cancel","돌아가기",()=>{if(HasSettlement)ShowPauseMenu();else ShowMainMenu();});
        }
        private void TryLoad(string id)
        {
            MapInput.CancelGesture();
            if(!Runner.LoadSlot(id)){Feedback=Runner.SaveMessage;ShowLoadMenu();return;}
            HasSettlement=true;CloseMenu();Refresh();ShowRestoredSettlement();
        }
        public void ShowRestoredSettlement()
        {
            ShowResult(Runner.SaveMessage);
            if(!string.IsNullOrEmpty(Runner.SettingsWarning))ShowWarning(Runner.SettingsWarning,CloseMenu);
        }
        public void ShowSaveMenu(Action afterSave=null)
        {
            OpenMenu("정착지 저장",ShowPauseMenu);
            RowLabel(menuContent,"새 이름으로 저장하거나 아래 슬롯을 선택해 덮어써. 같은 이름도 별도로 저장할 수 있어.",80);
            var active=Runner.Slots.ListSlots().Slots.FirstOrDefault(x=>x.SlotId==Runner.ActiveSlotId);
            SaveNameInput=NameField(menuContent,active?.DisplayName??"우리 정착지");
            var input=SaveNameInput;
            Action<string> save=target=>{
                if(!Runner.SaveSlot(input.text,target))
                {
                    menuStatus.text = Runner.SaveMessage;
                    LayoutMenu();
                    return;
                }
                ShowResult(Runner.SaveMessage);
                Action finish=()=>{if(afterSave!=null)afterSave();else ShowPauseMenu();};
                if(!string.IsNullOrEmpty(Runner.SettingsWarning))ShowWarning(Runner.SettingsWarning,finish);else finish();
            };
            FooterButton("save.new","새 슬롯에 저장",()=>save(null));
            foreach(var slot in Runner.Slots.ListSlots().Slots.Where(x=>x.IsValid&&!x.IsLegacy))
            {
                var entry=slot;
                RowButton(menuContent,"overwrite."+entry.SlotId,"덮어쓰기: "+SlotCaption(entry),()=>{
                    var name=input.text;
                    OpenMenu("이 저장을 덮어쓸까?",()=>ShowSaveMenu(afterSave), preferredHeight: 308);
                    RowLabel(menuContent,SlotCaption(entry)+"\n현재 정착지로 교체해. 다른 슬롯은 유지돼.",140);
                    FooterButton("overwrite.confirm","덮어쓰기",()=>{
                        if(!Runner.SaveSlot(name,entry.SlotId)){ShowWarning(Runner.SaveMessage,()=>ShowSaveMenu(afterSave));return;}
                        ShowResult(Runner.SaveMessage);Action finish=()=>{if(afterSave!=null)afterSave();else ShowPauseMenu();};
                        if(!string.IsNullOrEmpty(Runner.SettingsWarning))ShowWarning(Runner.SettingsWarning,finish);else finish();
                    });
                    FooterButton("cancel","취소",()=>ShowSaveMenu(afterSave));
                },90);
            }
            FooterButton("cancel","취소",ShowPauseMenu);
        }
        private void FooterButton(string key, string caption, Action action)
        {
            RowButton(menuActions, key, caption, action, 56);
            EqualizeWorkCells(menuActions);
        }

        private InputField NameField(Transform parent,string value)
        {
            var r=Box("Save name",parent,new Color(.04f,.06f,.07f));r.gameObject.AddComponent<LayoutElement>().preferredHeight=55;
            var t=Label("Input text",r,21);t.supportRichText=false;Stretch(t.rectTransform,10);
            var field=r.gameObject.AddComponent<InputField>();field.textComponent=t;field.lineType=InputField.LineType.SingleLine;
            field.contentType=InputField.ContentType.Standard;field.text=value;field.characterLimit=0;
            // Do not reject composition fragments. Repository validates only the committed value at save.
            return field;
        }
        private void ShowSettings(Action back)
        {
            OpenMenu("화면 설정",back);
            RowLabel(menuContent,"UI 크기 · 작은 화면에서는 목록을 스크롤할 수 있어.",65);
            var warning=RowLabel(menuContent,Runner.SettingsWarning,90,16);
            foreach(var scale in new[]{100,125,150})
            {var percent=scale;RowButton(menuContent,"scale."+scale,"UI "+scale+"%",()=>{
                Runner.SetUiScale(percent);warning.text=Runner.SettingsWarning;RefreshLayout();
            });}
            FooterButton("cancel","돌아가기",back);
        }
        private void ShowWarning(string text,Action acknowledged)
        {
            OpenMenu("확인해줘",acknowledged, preferredHeight: 326);RowLabel(menuContent,text,170);
            FooterButton("warning.ok","확인",acknowledged);
        }
        private static string SlotCaption(SaveSlotInfo slot)
        {
            return slot.DisplayName+(slot.IsLegacy?" [기존 저장]":"")+"\n"+(slot.IsValid?slot.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")+" · "+slot.Tick+" Tick · 정착민 "+slot.ColonistCount:SimulationRunner.SlotError(slot.ErrorCode));
        }
        private void RequestExit()
        {ExitRequested=true;if(!SuppressProcessExit)Application.Quit();}
    }
}
