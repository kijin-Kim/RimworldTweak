using System.Collections.Generic;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        public ConstructionPanel ConstructionPanel { get; private set; }
        public FarmingPanel FarmingPanel { get; private set; }
        public Button ProductionButton { get; private set; }
        private RectTransform productionPanel;
        private bool showProduction;
        private int? inspectedBlueprintId, inspectedBuildingId;
        private int? trackedConstructionTarget, trackedConstructionPawn;
        private BuildingKind blueprintKind;
        private readonly List<RectTransform> productionMarkers=new List<RectTransform>();
        private readonly List<RectTransform> growingFloors=new List<RectTransform>();
        private void BuildProductionUI()
        {
            ProductionButton=MakeButton("건설 / 계획",workPanel,536,418,152);
            ProductionButton.onClick.AddListener(()=>{showProduction=!showProduction;CancelWorkTool();RefreshNow();});
            productionPanel=Panel("Production tools",sidebar,0,0,708,408,new Color(.08f,.12f,.14f));
            productionPanel.GetComponent<Image>().raycastTarget=true;
            ConstructionPanel=new ConstructionPanel();
            ConstructionPanel.Initialize(productionPanel,font,BeginBlueprint,()=>{
                if(inspectedBlueprintId.HasValue) Runner.CancelBlueprint(inspectedBlueprintId.Value);RefreshNow();
            },()=>{
                foreach(var b in Runner.Session.World.Buildings) if(b.Id==inspectedBuildingId&&b.Exists) {Runner.SetDemolition(b.Id,!b.DemolitionDesignated);break;}
                RefreshNow();
            });
            ConstructionPanel.Root.anchoredPosition=new Vector2(0,248);
            FarmingPanel=new FarmingPanel();
            FarmingPanel.Build(productionPanel,font,()=>SetWorkTool(WorkTool.Growing),()=>{
                var z=SelectedGrowingZone(Runner.Session.World);if(z!=null) Runner.SetSowingEnabled(z.Id,!z.SowingEnabled);RefreshNow();
            },()=>{
                var z=SelectedGrowingZone(Runner.Session.World);if(z!=null) Runner.DeleteGrowingZone(z.Id);RefreshNow();
            });
            FarmingPanel.Root.anchoredPosition=new Vector2(0,136);
        }
        public void BeginBlueprint(BuildingKind kind) {blueprintKind=kind;SetWorkTool(WorkTool.Building);}
        private GrowingZoneState SelectedGrowingZone(WorldState w)
        {
            foreach(var z in w.GrowingZones) if(z.Exists)
            {
                if(z.Id==SelectedObjectId) return z;
                foreach(var c in z.Cells) if(c.Id==SelectedObjectId) return z;
            }
            return null;
        }
        private static bool IsConstructionWorker(ColonistState pawn) => pawn.FeatureStep==WorkStep.DeliverBuild || pawn.FeatureStep==WorkStep.Construct || pawn.FeatureStep==WorkStep.Demolish;
        private void RefreshProductionUI(WorldState w)
        {
            if(productionPanel==null) return;
            BlueprintState bp=null;BuildingState building=null;CropState crop=null;
            foreach(var b in w.Blueprints) if(b.Exists&&b.Id==SelectedObjectId) bp=b;
            foreach(var b in w.Buildings) if(b.Exists&&b.Id==SelectedObjectId) building=b;
            foreach(var c in w.Crops) if(c.Exists&&c.Id==SelectedObjectId) crop=c;
            var hasBuilder=false;
            foreach(var pawn in w.Colonists) if(pawn.Id==SelectedColonistId && IsConstructionWorker(pawn))
            {
                var target=pawn.PendingDeliveryTargetId??pawn.WorkTargetId;
                if(trackedConstructionTarget!=target || trackedConstructionPawn!=pawn.Id) showProduction=true;
                trackedConstructionTarget=target;trackedConstructionPawn=pawn.Id;hasBuilder=true;
                bp=null;building=null;
                foreach(var b in w.Blueprints) if(b.Exists&&b.Id==target) bp=b;
                foreach(var b in w.Buildings) if(b.Exists&&b.Id==target) building=b;
            }
            if(!hasBuilder) {trackedConstructionTarget=null;trackedConstructionPawn=null;}
            var zone=SelectedGrowingZone(w);
            if(crop==null&&zone!=null) foreach(var c in zone.Cells) if(c.Position==SelectedWorldTile) crop=c;
            inspectedBlueprintId=bp?.Id; inspectedBuildingId=building?.Id;
            ConstructionPanel.Refresh(bp,building,w.LastCommandError);
            FarmingPanel.Refresh(zone,crop,w.LastCommandError);
            productionPanel.gameObject.SetActive(showProduction);
            ProductionButton.GetComponentInChildren<Text>().text=showProduction?"물류 보기":"건설 / 계획";
            if(showProduction)
            {
                if(ActiveWorkTool==WorkTool.Building) ConstructionPanel.DetailText.text+="\n배치 중: "+RimworldTweak.Presentation.ConstructionPanel.Name(blueprintKind);
                if(ActiveWorkTool==WorkTool.Growing) FarmingPanel.InfoText.text+="\n"+(StorageFirstCorner.HasValue?"반대쪽 모서리를 클릭":"첫 모서리를 클릭");
            }
            DrawProduction(w);
        }
        private void AddProductionObjects(WorldState w)
        {
            foreach(var b in w.Blueprints) if(b.Exists&&b.Position==SelectedWorldTile) objectIds.Add(b.Id);
            foreach(var b in w.Buildings) if(b.Exists&&b.Position==SelectedWorldTile&&!objectIds.Contains(b.Id)) objectIds.Add(b.Id);
            foreach(var c in w.Crops) if(c.Exists&&c.Position==SelectedWorldTile) objectIds.Add(c.Id);
        }
        private bool IsProductionObject(WorldState w,int? id)
        {
            foreach(var b in w.Blueprints) if(b.Exists&&b.Id==id) return true;
            foreach(var b in w.Buildings) if(b.Exists&&b.Id==id) return true;
            foreach(var c in w.Crops) if(c.Exists&&c.Id==id) return true;
            return false;
        }
        private RectTransform ProductionMarker(int index,WorldState w,GridPosition p,string caption,MapSymbol shape,Color tint)
        {
            if(index==productionMarkers.Count)
            {
                var rect=Panel("Production marker",board,0,0,1,1,Color.white);rect.pivot=new Vector2(.5f,.5f);
                Label("Label",rect,"",0,0,100,18,11,Ink);productionMarkers.Add(rect);
            }
            var marker=productionMarkers[index];var cell=480f/Mathf.Max(w.Width,w.Height);
            PlaceOverlay(marker,p,cell,.70f);SymbolOn(marker,shape,tint);
            var label=marker.GetComponentInChildren<Text>();label.text=caption;label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);
            label.rectTransform.pivot=new Vector2(.5f,0);label.rectTransform.anchoredPosition=new Vector2(0,2-cell/2);
            label.rectTransform.sizeDelta=new Vector2(110,18);marker.SetAsLastSibling();return marker;
        }
        private static MapSymbol BuildingSymbol(BuildingKind kind) => kind==BuildingKind.Wall?MapSymbol.Wall:kind==BuildingKind.Door?MapSymbol.Door:kind==BuildingKind.Bed?MapSymbol.Bed:MapSymbol.Stove;
        private void DrawProduction(WorldState w)
        {
            var index=0;var floors=0;var cell=480f/Mathf.Max(w.Width,w.Height);
            foreach(var c in w.Crops) if(c.Exists)
            {
                if(floors==growingFloors.Count) growingFloors.Add(Panel("Growing soil",board,0,0,1,1,new Color(.38f,.29f,.15f,.7f)));
                var floor=growingFloors[floors++];floor.gameObject.SetActive(true);floor.anchoredPosition=new Vector2(c.Position.X*cell,c.Position.Y*cell);floor.sizeDelta=Vector2.one*cell;
                floor.GetComponent<Image>().sprite=outlineSprite;floor.GetComponent<Image>().color=new Color(.75f,.62f,.30f,.7f);floor.SetSiblingIndex(w.Width*w.Height+floors-1);
                ProductionMarker(index++,w,c.Position,c.Stage==CropStage.Empty?"밭 · 파종":c.Stage==CropStage.Mature?"작물 · 수확":"작물 "+(100*c.GrowthTicks/FarmingRules.GrowthDuration)+"%",MapSymbol.Crop,c.Stage==CropStage.Mature?new Color(.95f,.78f,.3f):new Color(.38f,.7f,.3f));
            }
            foreach(var b in w.Blueprints) if(b.Exists) ProductionMarker(index++,w,b.Position,RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+" [설계]",BuildingSymbol(b.Kind),new Color(.35f,.70f,.95f,.65f));
            foreach(var b in w.Buildings) if(b.Exists && (b.Kind!=BuildingKind.Bed || b.DemolitionDesignated)) ProductionMarker(index++,w,b.Position,RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+(b.DemolitionDesignated?" [철거]":""),BuildingSymbol(b.Kind),new Color(.76f,.64f,.48f));
            for(var i=index;i<productionMarkers.Count;i++) productionMarkers[i].gameObject.SetActive(false);
            for(var i=floors;i<growingFloors.Count;i++) growingFloors[i].gameObject.SetActive(false);
        }
        private void InspectProduction(ColonistState pawn,WorldState w,ref string claim)
        {
            foreach(var b in w.Blueprints) if(b.OwnerId==pawn.Id && b.OwnerActionId==pawn.ActionId)
            {claim=RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+" 설계도 #"+b.Id;inspectedTarget=b.Position;inspectedTargetId=b.Id;}
            foreach(var b in w.Buildings) if(b.OwnerId==pawn.Id&&b.OwnerActionId==pawn.ActionId)
            {claim=RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+(pawn.FeatureStep==WorkStep.Cook||pawn.FeatureStep==WorkStep.DeliverCook?" #":" 철거 #")+b.Id;inspectedTarget=b.Position;inspectedTargetId=b.Id;}
            foreach(var c in w.Crops) if(c.OwnerId==pawn.Id&&c.OwnerActionId==pawn.ActionId)
            {claim="재배 칸 #"+c.Id;inspectedTarget=c.Position;inspectedTargetId=c.Id;}
            if(pawn.PendingDeliveryTargetId.HasValue) DestinationText.text+=" · 반입 현장 #"+pawn.PendingDeliveryTargetId.Value;
            if(IsConstructionWorker(pawn))
            {
                var target=pawn.PendingDeliveryTargetId??pawn.WorkTargetId;
                foreach(var b in w.Blueprints) if(b.Exists&&b.Id==target)
                    LifeText.text+="\n"+RimworldTweak.Presentation.ConstructionPanel.Name(b.Kind)+" 현장 #"+b.Id+" · 목재 "+b.DeliveredWood+"/"+b.Cost+" · 시공 "+b.Progress+"/"+b.Duration;
            }
            if(pawn.WorkActivity==WorkActivity.WorkingFeature)
            {
                foreach(var b in w.Buildings) if(b.Id==pawn.WorkTargetId&&pawn.FeatureStep==WorkStep.Demolish) LifeText.text+=" "+b.DemolitionProgress+"/30";
                foreach(var c in w.Crops) if(c.Id==pawn.WorkTargetId) LifeText.text+=" "+(pawn.FeatureStep==WorkStep.Sow?c.SowProgress+"/20":c.HarvestProgress+"/30");
            }
        }
    }
}
