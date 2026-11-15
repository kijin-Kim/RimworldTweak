using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        private RectTransform inspectionPanel;
        public Text CargoText { get; private set; }
        public Text DestinationText { get; private set; }
        public Text ClaimText { get; private set; }
        public Button LocateTargetButton { get; private set; }
        public Button SelectTargetButton { get; private set; }
        private Image nutritionFill, restFill;
        private RectTransform claimOutline;
        private GridPosition? inspectedTarget;
        private int? inspectedTargetId;
        private readonly List<RectTransform> storageFloors = new List<RectTransform>();
        private readonly List<RectTransform> storageEdges = new List<RectTransform>();
        private readonly Dictionary<int, Text> pawnNames = new Dictionary<int, Text>();
        public static Color PawnColor(int id)
        {
            var palette = new[] { new Color(.31f,.85f,.78f), new Color(1f,.65f,.30f), new Color(.75f,.58f,.96f), new Color(.46f,.71f,1f), new Color(.95f,.53f,.67f) };
            return palette[(id - 1) % palette.Length];
        }
        public static string PawnName(int id) => id + " · 정착민";
        public static string ResourceName(ResourceKind kind) => kind == ResourceKind.Wood ? "목재" : kind == ResourceKind.Meal ? "식사" : "식재료";
        private static void Layout(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
        }
        private void PlaceButton(Button button, Transform parent, float x, float y, float width, string title)
        {
            Layout((RectTransform)button.transform,parent,x,y,width,38);
            var label=button.GetComponentInChildren<Text>(true);
            Layout(label.rectTransform,button.transform,0,0,width,38); label.fontSize=16; label.text=title;
        }
        private void BuildInspectionLayout()
        {
            foreach (var name in new[] { "Title", "Subtitle", "Controls", "Scope" })
            { var child=uiRoot.transform.Find(name); if(child!=null) child.gameObject.SetActive(false); }
            Layout(board,uiRoot.transform,32,264,480,480);
            Layout(sidebar,uiRoot.transform,540,264,708,480);
            PlaceButton(PauseButton,uiRoot.transform,540,750,110,"일시정지");
            PlaceButton(StepButton,uiRoot.transform,658,750,90,"+1 Tick");
            PlaceButton(Speed1Button,uiRoot.transform,756,750,82,"1배속");
            PlaceButton(Speed3Button,uiRoot.transform,846,750,82,"3배속");
            PlaceButton(ResetButton,uiRoot.transform,1138,750,110,"초기화");
            Layout(TickText.rectTransform,uiRoot.transform,940,750,190,38); TickText.fontSize=20;
            statusText.gameObject.SetActive(false);
            LifeTabButton.gameObject.SetActive(false); WorkTabButton.gameObject.SetActive(false);
            Layout(workPanel,sidebar,0,0,708,480);
            PlaceButton(GatherButton,workPanel,20,418,160,"채집 지정");
            PlaceButton(StorageButton,workPanel,190,418,160,"저장 구역");
            Button select=null; foreach(var button in workPanel.GetComponentsInChildren<Button>(true)) if(button.name=="Select / Esc") select=button;
            PlaceButton(select,workPanel,360,418,160,"선택 / Esc");
            Layout(WorkInfoText.rectTransform,workPanel,20,240,668,168); WorkInfoText.fontSize=16;
            PlaceButton(CancelGatherButton,workPanel,20,188,160,"채집 지정 취소");
            PlaceButton(DeleteStorageButton,workPanel,190,188,160,"저장 구역 삭제");
            PlaceButton(NextObjectButton,workPanel,360,188,160,"다음 오브젝트");
            for(var i=0;i<filterButtons.Count;i++) PlaceButton(filterButtons[i],workPanel,20+i*170,140,160,"");
            Layout(StockTotalsText.rectTransform,workPanel,20,58,668,76); StockTotalsText.fontSize=16;
            Label("Tools help",workPanel,"R 소집/해제   우클릭 이동   Space 정지   Esc 도구 취소",20,12,668,30,14,Muted);

            inspectionPanel=Panel("Selection inspector",uiRoot.transform,32,20,1216,228,new Color(.08f,.12f,.14f));
            inspectionPanel.GetComponent<Image>().raycastTarget=true;
            Layout(SelectionText.rectTransform,inspectionPanel,20,172,275,40); SelectionText.fontSize=20;
            Layout(NeedsText.rectTransform,inspectionPanel,20,130,275,40); NeedsText.fontSize=14;
            nutritionFill=NeedBar("Nutrition bar",20,110,ColonistColor);
            restFill=NeedBar("Rest bar",20,84,new Color(.48f,.69f,.88f));
            PlaceButton(DraftButton,inspectionPanel,20,24,180,"소집 [R]");
            Layout(LifeText.rectTransform,inspectionPanel,316,148,660,60); LifeText.fontSize=18;
            CargoText=Label("Carried material",inspectionPanel,"",316,111,660,30,16,Ink);
            DestinationText=Label("Destination",inspectionPanel,"",316,76,660,30,16,Ink);
            ClaimText=Label("Claim target",inspectionPanel,"",316,41,660,30,16,Ink);
            Layout(feedbackText.rectTransform,inspectionPanel,316,8,660,28); feedbackText.fontSize=14;
            LocateTargetButton=MakeButton("위치 보기",inspectionPanel,1000,102,190);
            SelectTargetButton=MakeButton("점유 대상 선택",inspectionPanel,1000,52,190);
            LocateTargetButton.onClick.AddListener(()=> { if(inspectedTarget.HasValue) PlaceOverlay(claimOutline,inspectedTarget.Value,480f/Runner.Session.World.Width,.96f); });
            SelectTargetButton.onClick.AddListener(()=> {
                if(!inspectedTarget.HasValue) return;
                SelectWorldObject(inspectedTarget.Value);
                SelectedColonistId=null;
                if(inspectedTargetId.HasValue) SelectedObjectId=inspectedTargetId;
                RefreshNow();
            });
            claimOutline=Outline("Claim target outline",SelectedColor);
            // Object/stock diagnostics live in the always-visible work pane, not a second pawn tab.
            ReservationText.gameObject.SetActive(false);
            foreach(var label in sidebar.GetComponentsInChildren<Text>(true))
                if(label.transform.parent==sidebar && label!=WorkInfoText) label.gameObject.SetActive(false);
        }
        private Image NeedBar(string name,float x,float y,Color tint)
        {
            var back=Panel(name,inspectionPanel,x,y,270,12,new Color(.16f,.20f,.22f));
            return Panel("Fill",back,0,0,270,12,tint).GetComponent<Image>();
        }
        private void RefreshInspection(WorldState world)
        {
            if(inspectionPanel==null) return;
            RefreshMapIdentity(world);
            ColonistState pawn=null;
            foreach(var c in world.Colonists) if(c.Id==SelectedColonistId) pawn=c;
            inspectedTarget=null; inspectedTargetId=null; claimOutline.gameObject.SetActive(false);
            if(pawn==null)
            {
                SelectionText.text="선택한 정착민 없음"; NeedsText.text="맵 또는 상단 목록에서 선택";
                LifeText.text="정착민을 선택하면 행동과 점유 대상을 확인할 수 있습니다.";
                CargoText.text="운반 물자  —"; DestinationText.text="목적지  —"; ClaimText.text="점유 대상  —";
                nutritionFill.rectTransform.sizeDelta=new Vector2(0,12); restFill.rectTransform.sizeDelta=new Vector2(0,12);
            }
            else
            {
                SelectionText.text=PawnName(pawn.Id)+(pawn.IsDrafted ? "  [소집]" : ""); SelectionText.color=PawnColor(pawn.Id);
                NeedsText.text="포만도 "+NeedValue(pawn.Nutrition)+"   휴식 "+NeedValue(pawn.Rest);
                nutritionFill.rectTransform.sizeDelta=new Vector2(270f*pawn.Nutrition/10000,12);
                restFill.rectTransform.sizeDelta=new Vector2(270f*pawn.Rest/10000,12);
                nutritionFill.color=pawn.Nutrition<=3000?new Color(.95f,.55f,.3f):PawnColor(pawn.Id);
                LifeText.text="현재 행동  "+CurrentAction(pawn);
                if(pawn.WorkActivity==WorkActivity.Gathering) foreach(var node in world.Gatherables)
                    if(node.Id==pawn.WorkTargetId) LifeText.text+=" "+node.Progress+" / "+node.Duration;
                if(pawn.Nutrition==0) LifeText.text+=" · 기아";
                var reason=ReadableReason(pawn.FoodReason);
                if(string.IsNullOrEmpty(reason)) reason=ReadableReason(pawn.SleepReason);
                if(string.IsNullOrEmpty(reason) && !pawn.IsWorking && pawn.LifeActivity==LifeActivity.Idle) reason=ReadableReason(pawn.WorkReason);
                if(!pawn.IsDrafted && !string.IsNullOrEmpty(reason)) LifeText.text+="\n"+reason;
                CargoText.text="운반 물자  "+(pawn.CargoAmount>0?ResourceName(pawn.CargoKind)+" ×"+pawn.CargoAmount:"없음");
                DestinationText.text="목적지  "+(pawn.Destination.HasValue?pawn.Destination.Value.ToString():"없음");
                var claim="없음";
                foreach(var resource in world.Resources) if(resource.OwnerId==pawn.Id && resource.OwnerActionId==pawn.ActionId)
                { claim=ResourceName(resource.Kind)+" #"+resource.Id; inspectedTarget=resource.Position; inspectedTargetId=resource.Id; }
                foreach(var bed in world.Beds) if(bed.OwnerId==pawn.Id && bed.OwnerActionId==pawn.ActionId)
                { claim="침대 #"+bed.Id+(bed.IsOccupied?" · 수면 중":" · 접근 중"); inspectedTarget=bed.Position; inspectedTargetId=bed.Id; }
                foreach(var node in world.Gatherables) if(node.OwnerId==pawn.Id && node.OwnerActionId==pawn.ActionId)
                { claim=(node.Kind==GatherKind.Tree?"나무":"식용 식물")+" #"+node.Id; inspectedTarget=node.Position; inspectedTargetId=node.Id; }
                foreach(var zone in world.StorageZones) if(zone.Exists) foreach(var slot in zone.Slots)
                    if(slot.OwnerId==pawn.Id && slot.OwnerActionId==pawn.ActionId && pawn.CargoAmount>0)
                    { claim="저장 구역 "+zone.Id+" / 칸 "+slot.Position+" · 입고 예정"; inspectedTarget=slot.Position; inspectedTargetId=zone.Id; }
                InspectProduction(pawn,world,ref claim);
                ClaimText.text="점유 대상  "+claim;
                if(inspectedTarget.HasValue)
                { claimOutline.GetComponent<Image>().color=PawnColor(pawn.Id); PlaceOverlay(claimOutline,inspectedTarget.Value,480f/Mathf.Max(world.Width,world.Height),.96f); }
            }
            if(pawn==null && SelectedObjectId.HasValue)
            {
                SelectionText.text="오브젝트 #"+SelectedObjectId; SelectionText.color=Ink;
                LifeText.text=ObjectDescription(world,SelectedObjectId.Value);
                NeedsText.text=SelectedWorldTile.HasValue?"위치 "+SelectedWorldTile.Value:"";
            }
            DraftButton.GetComponentInChildren<Text>().text=pawn!=null&&pawn.IsDrafted?"소집 해제 [R]":"소집 [R]";
            LocateTargetButton.interactable=SelectTargetButton.interactable=inspectedTarget.HasValue;
            for(var i=0;i<colonistButtons.Count && i<world.Colonists.Count;i++)
            {
                var c=world.Colonists[i]; colonistButtons[i].GetComponentInChildren<Text>().text=PawnName(c.Id);
                colonistButtons[i].GetComponentInChildren<Text>().color=PawnColor(c.Id);
            }
            foreach(var c in world.Colonists)
            {
                if(!pawnNames.TryGetValue(c.Id,out var label))
                { label=Label("Pawn name "+c.Id,board,"",0,0,104,20,12,PawnColor(c.Id)); label.alignment=TextAnchor.MiddleCenter; pawnNames.Add(c.Id,label); }
                var cell=480f/Mathf.Max(world.Width,world.Height); var rank=0;
                foreach(var other in world.Colonists) if(other.Position==c.Position && other.Id<c.Id) rank++;
                label.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp((c.Position.X+.5f)*cell-52,0,board.rect.width-104),Mathf.Max(0,(c.Position.Y+1)*cell-22-rank*18));
                label.text=PawnName(c.Id)+(c.IsDrafted?" [R]":""); label.color=PawnColor(c.Id); label.gameObject.SetActive(true); label.transform.SetAsLastSibling();
            }
            foreach(var entry in pawnNames)
            { var exists=false; foreach(var c in world.Colonists) if(c.Id==entry.Key) exists=true; if(!exists) entry.Value.gameObject.SetActive(false); }
        }
        private static string ObjectDescription(WorldState world,int id)
        {
            foreach(var item in world.Resources) if(item.Id==id)
                return ResourceName(item.Kind)+" ×"+item.Amount+(item.StorageSlotId.HasValue?" · 보관 중":" · 미보관")+"\n점유자 "+(item.OwnerId.HasValue?PawnName(item.OwnerId.Value):"없음")+" · 접근 가능 "+item.AccessibleAmount;
            foreach(var bed in world.Beds) if(bed.Id==id)
                return "침대 #"+id+"\n"+(!bed.Exists?"제거됨":!bed.OwnerId.HasValue?"비어 있음":PawnName(bed.OwnerId.Value)+(bed.IsOccupied?" · 수면 중":" · 접근 중"));
            foreach(var node in world.Gatherables) if(node.Id==id)
                return (node.Kind==GatherKind.Tree?"나무":"식용 식물")+" · 진행 "+node.Progress+" / "+node.Duration+"\n"+(node.Designated?"채집 지정됨":"지정 없음")+" · 점유자 "+(node.OwnerId.HasValue?PawnName(node.OwnerId.Value):"없음");
            foreach(var zone in world.StorageZones) if(zone.Id==id) return "저장 구역 "+id+"\n오른쪽 필터에서 허용 자원을 선택합니다";
            foreach(var b in world.Blueprints) if(b.Exists&&b.Id==id) return RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+" 설계도 · 목재 "+b.DeliveredWood+"/"+b.Cost+" · 진행 "+b.Progress+"/"+b.Duration;
            foreach(var b in world.Buildings) if(b.Exists&&b.Id==id) return RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+(b.DemolitionDesignated?" · 철거 "+b.DemolitionProgress+"/30":" · 완공");
            foreach(var c in world.Crops) if(c.Exists&&c.Id==id) return "재배 칸 · "+(c.Stage==CropStage.Empty?"파종 "+c.SowProgress+"/20":c.Stage==CropStage.Growing?"성장 "+c.GrowthTicks+"/2400":"수확 "+c.HarvestProgress+"/30");
            return "대상이 더 이상 존재하지 않습니다";
        }
        private static string ReadableReason(string code)
        {
            switch(code)
            {
                case "NoFood": return "먹을 수 있는 음식이 없습니다";
                case "FoodReserved": return "음식을 다른 정착민이 점유 중입니다";
                case "FoodUnreachable": return "음식까지 이동할 수 없습니다";
                case "NoBed": return "침대가 없어 바닥에서 쉽니다";
                case "BedReserved": return "침대를 다른 정착민이 점유 중입니다";
                case "BedUnreachable": return "침대까지 이동할 수 없습니다";
                case "NoAllowedStorage": return "허용된 저장 구역이 없습니다";
                case "StorageFull": return "저장 공간이 부족합니다";
                case "DestinationReserved": return "도착 칸을 다른 정착민이 점유 중입니다";
                case "TargetReserved": return "작업 대상을 다른 정착민이 점유 중입니다";
                case "Unreachable": return "작업 대상까지 이동할 수 없습니다";
                case "NoDesignation": return "수행할 작업 지정이 없습니다";
                default:return "";
            }
        }
        private void DrawStorageGround(WorldState world)
        {
            var fills=0; var edges=0; var cell=480f/Mathf.Max(world.Width,world.Height);
            foreach(var zone in world.StorageZones) if(zone.Exists)
            {
                var positions=new HashSet<GridPosition>(); foreach(var slot in zone.Slots) positions.Add(slot.Position);
                var selected=SelectedObjectId==zone.Id;
                foreach(var slot in zone.Slots)
                {
                    if(fills==storageFloors.Count) storageFloors.Add(Panel("Storage floor",board,0,0,1,1,Color.white));
                    var floor=storageFloors[fills++]; floor.gameObject.SetActive(true);
                    floor.anchoredPosition=new Vector2(slot.Position.X*cell,slot.Position.Y*cell); floor.sizeDelta=Vector2.one*cell;
                    floor.GetComponent<Image>().color=new Color(.20f,.55f,.78f,selected?.30f:.17f);
                    floor.SetSiblingIndex(world.Width*world.Height+fills-1);
                    var offsets=new[] { new GridPosition(-1,0),new GridPosition(1,0),new GridPosition(0,-1),new GridPosition(0,1) };
                    for(var side=0;side<4;side++)
                    {
                        var d=offsets[side]; if(positions.Contains(new GridPosition(slot.Position.X+d.X,slot.Position.Y+d.Y))) continue;
                        if(edges==storageEdges.Count) storageEdges.Add(Panel("Storage outer edge",board,0,0,1,1,Color.white));
                        var edge=storageEdges[edges++]; edge.gameObject.SetActive(true); var thickness=selected?3f:2f;
                        edge.anchoredPosition=new Vector2(slot.Position.X*cell+(side==1?cell-thickness:0),slot.Position.Y*cell+(side==3?cell-thickness:0));
                        edge.sizeDelta=side<2?new Vector2(thickness,cell):new Vector2(cell,thickness);
                        edge.GetComponent<Image>().color=selected?SelectedColor:new Color(.37f,.69f,.89f,.85f);
                        edge.SetSiblingIndex(world.Width*world.Height+fills+edges-1);
                    }
                }
            }
            for(var i=fills;i<storageFloors.Count;i++) storageFloors[i].gameObject.SetActive(false);
            for(var i=edges;i<storageEdges.Count;i++) storageEdges[i].gameObject.SetActive(false);
            // Ground is always below entity symbols, including after new slots are allocated.
            foreach(var marker in markers) if(marker.Kind!=ViewKind.Tile) marker.transform.SetAsLastSibling();
        }
        private static string CurrentAction(ColonistState pawn)
        {
            if(pawn.IsDrafted) return pawn.Status==MovementStatus.Moving?"소집 · 지정 위치로 이동 중":"소집 · 플레이어 명령 대기";
            switch(pawn.LifeActivity)
            {
                case LifeActivity.GoingToFood:return "음식으로 이동 중";
                case LifeActivity.Eating:return "식사 중 "+pawn.ActionProgress+" / "+pawn.ActionDuration;
                case LifeActivity.GoingToBed:return "침대로 이동 중";
                case LifeActivity.SleepingInBed:return "침대에서 수면 중";
                case LifeActivity.SleepingOnFloor:return "바닥에서 수면 중";
            }
            switch(pawn.WorkActivity)
            {
                case WorkActivity.GoingToBuildPickup:return pawn.FeatureStep==WorkStep.DeliverCook?"조리 식재료로 이동 중":"건설 목재로 이동 중";
                case WorkActivity.PickingUpBuild:return pawn.FeatureStep==WorkStep.DeliverCook?"조리 식재료 집기":"건설 목재 집기";
                case WorkActivity.GoingToBuildDelivery:return pawn.FeatureStep==WorkStep.DeliverCook?"조리대로 반입 중":"건설 현장으로 반입 중";
                case WorkActivity.DeliveringBuild:return pawn.FeatureStep==WorkStep.DeliverCook?"조리 식재료 투입":"건설 자재 내려놓기";
                case WorkActivity.GoingToFeature:return "작업 현장으로 이동 중";
                case WorkActivity.WorkingFeature:return pawn.FeatureStep==WorkStep.Cook?"조리 중":pawn.FeatureStep==WorkStep.Construct?"시공 중":pawn.FeatureStep==WorkStep.Demolish?"철거 중":pawn.FeatureStep==WorkStep.Sow?"파종 중":"수확 중";
                case WorkActivity.GoingToGather:return "채집 대상으로 이동 중";
                case WorkActivity.Gathering:return "채집 중";
                case WorkActivity.GoingToPickup:return "물자를 가지러 이동 중";
                case WorkActivity.PickingUp:return "물자를 집는 중";
                case WorkActivity.GoingToStore:return "저장 구역으로 운반 중";
                case WorkActivity.Storing:return "저장 칸에 내려놓는 중";
            }
            return pawn.Status==MovementStatus.Moving?"지정 위치로 이동 중":"대기 중";
        }

        private static MapSymbolGraphic SymbolOn(RectTransform rect, MapSymbol kind, Color tint)
        {
            var symbol=rect.GetComponentInChildren<MapSymbolGraphic>(true);
            if(symbol==null)
            {
                var graphicRect=Rect("Symbol",rect,0,0,1,1);
                graphicRect.anchorMin=Vector2.zero; graphicRect.anchorMax=Vector2.one;
                graphicRect.offsetMin=graphicRect.offsetMax=Vector2.zero; graphicRect.SetAsFirstSibling();
                symbol=graphicRect.gameObject.AddComponent<MapSymbolGraphic>();
            }
            symbol.raycastTarget=false; symbol.SetSymbol(kind,tint); symbol.enabled=true;
            var image=rect.GetComponent<Image>(); if(image!=null) image.enabled=false;
            return symbol;
        }
        private void RefreshMapIdentity(WorldState world)
        {
            var seenResources=new HashSet<GridPosition>();
            foreach(var marker in markers)
            {
                var rect=(RectTransform)marker.transform;
                var image=marker.GetComponent<Image>(); var symbol=marker.GetComponentInChildren<MapSymbolGraphic>(true);
                if(marker.Kind==ViewKind.Tile) { image.enabled=true; if(symbol!=null) symbol.enabled=false; continue; }
                var label=marker.GetComponentInChildren<Text>(true);
                string caption=null; int? owner=null;
                if(marker.Kind==ViewKind.Colonist)
                {
                    var person=SymbolOn(rect,MapSymbol.Person,PawnColor(marker.EntityId));
                    var shares=false;
                    foreach(var r in world.Resources) if(r.Exists&&r.Amount>0&&r.Position==marker.Position) shares=true;
                    foreach(var n in world.Gatherables) if(!n.Depleted&&n.Position==marker.Position) shares=true;
                    foreach(var b in world.Beds) if(b.Exists&&b.Position==marker.Position) shares=true;
                    person.rectTransform.localScale=Vector3.one*(shares?.65f:1f);
                    person.rectTransform.anchoredPosition=shares?new Vector2(14,0):Vector2.zero;
                    rect.SetAsLastSibling(); label.gameObject.SetActive(false); continue;
                }
                if(marker.Kind==ViewKind.Bed)
                {
                    foreach(var bed in world.Beds) if(bed.Id==marker.EntityId) owner=bed.OwnerId;
                    SymbolOn(rect,MapSymbol.Bed,new Color(.48f,.69f,.88f)); caption="침대 #"+marker.EntityId;
                }
                else if(marker.Kind==ViewKind.Resource)
                {
                    ResourceState item=null; foreach(var r in world.Resources) if(r.Id==marker.EntityId) item=r;
                    if(item==null || !item.Exists || item.Amount<=0) continue;
                    if(!seenResources.Add(item.Position)) { marker.gameObject.SetActive(false); continue; }
                    marker.gameObject.SetActive(true);
                    SymbolOn(rect,item.Kind==ResourceKind.Wood?MapSymbol.Wood:item.Kind==ResourceKind.Meal?MapSymbol.Meal:MapSymbol.Ingredients,ResourceColor(item.Kind));
                    caption=ResourceName(item.Kind)+" ×"+item.Amount; owner=item.OwnerId;
                    var extra=0; foreach(var r in world.Resources) if(r.Exists&&r.Amount>0&&r.Position==item.Position&&r.Id!=item.Id) extra++;
                    if(extra>0) caption+=" +"+extra+"묶음";
                }
                label.gameObject.SetActive(true); label.text=caption+(owner.HasValue?" ["+owner.Value+"]":"");
                label.color=owner.HasValue?PawnColor(owner.Value):Ink;
                label.resizeTextForBestFit=false; label.fontSize=11; label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);
                label.rectTransform.pivot=new Vector2(.5f,0);
                label.rectTransform.anchoredPosition=new Vector2(0,2-240f/Mathf.Max(world.Width,world.Height));
                label.rectTransform.sizeDelta=new Vector2(100,18);
            }
        }
    }
}
