using System;
using RimworldTweak.Simulation;
using UnityEngine;
namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        public CookingPanel CookingPanel { get; private set; }
        public WorkPolicyPanel WorkPolicyPanel { get; private set; }
        private int? inspectedStoveId;
        private int? lastInspectedStoveId;
        private void BuildPlanningUI()
        {
            CookingPanel=new CookingPanel();
            CookingPanel.Initialize(productionPanel,font,(enabled,target)=>{if(inspectedStoveId.HasValue)Runner.SetCookingOrder(inspectedStoveId.Value,enabled,target);RefreshNow();},
                ()=>{if(inspectedStoveId.HasValue)Runner.DeleteCookingOrder(inspectedStoveId.Value);RefreshNow();},
                ()=>{if(inspectedStoveId.HasValue)Runner.SetCookingOrder(inspectedStoveId.Value,false,12);RefreshNow();});
            CookingPanel.Root.anchoredPosition=new Vector2(0,80);
            WorkPolicyPanel=new WorkPolicyPanel();
            WorkPolicyPanel.Build(productionPanel,font,(category,priority)=>{if(SelectedColonistId.HasValue)Runner.SetWorkPriority(SelectedColonistId.Value,category,priority);RefreshNow();});
            WorkPolicyPanel.Root.anchoredPosition=Vector2.zero;
        }
        private void RefreshPlanningUI(WorldState world)
        {
            if(CookingPanel==null)return;
            inspectedStoveId=null;WorkPolicyState policy=null;ColonistState pawn=null;
            foreach(var p in world.Colonists)if(p.Id==SelectedColonistId)pawn=p;
            foreach(var p in world.WorkPolicies)if(p.PawnId==SelectedColonistId)policy=p;
            var target=SelectedObjectId;
            if(pawn!=null&&(pawn.FeatureStep==WorkStep.Cook||pawn.FeatureStep==WorkStep.DeliverCook))target=pawn.PendingDeliveryTargetId??pawn.WorkTargetId;
            foreach(var b in world.Buildings)if(b.Exists&&b.Kind==BuildingKind.Stove&&b.Id==target)inspectedStoveId=b.Id;
            CookingOrderState order=null;CookingOutputState output=null;
            foreach(var o in world.CookingOrders)if(o.Exists&&o.StoveId==inspectedStoveId)order=o;
            foreach(var o in world.CookingOutputs)if(o.Exists&&o.StoveId==inspectedStoveId)output=o;
            CookingPanel.Refresh(order,output,world.ResourceTotal(ResourceKind.Meal,false)+world.ResourceTotal(ResourceKind.Meal,true)+world.CargoTotal(ResourceKind.Meal),world.LastCommandError);
            if(order!=null)
            {
                var diagnostic=world.GetWorkDiagnostic(WorkCategory.Cook,order.StoveId);
                var status=order.OwnerId.HasValue&&diagnostic==WorkReasonCode.TargetReserved&&output!=null&&!output.OwnerId.HasValue
                    ? "담당 정착민 작업 중" : WorkDiagnostics.GetReasonText(diagnostic);
                CookingPanel.DetailText.text+="\n작업 상태: "+status;
            }
            CookingPanel.Root.gameObject.SetActive(inspectedStoveId.HasValue);
            FarmingPanel.Root.gameObject.SetActive(!inspectedStoveId.HasValue);
            if(inspectedStoveId.HasValue&&inspectedStoveId!=lastInspectedStoveId){showProduction=true;productionPanel.gameObject.SetActive(true);}
            lastInspectedStoveId=inspectedStoveId;
            var reason=string.Empty;
            if(pawn!=null&&Enum.TryParse<WorkReasonCode>(pawn.WorkReason,out var code))reason=WorkDiagnostics.GetReasonText(code);
            WorkPolicyPanel.Refresh(policy,reason);
        }
    }
}
