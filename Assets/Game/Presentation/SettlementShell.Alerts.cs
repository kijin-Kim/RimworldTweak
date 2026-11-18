using System.Collections.Generic;
using System.Linq;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementShell
    {
        private readonly Dictionary<string,Button> alertRows=new Dictionary<string,Button>();
        public int AlertCount=>alertRows.Count;
        private void ClearAlerts()
        {
            if(alertContent!=null)Clear(alertContent);
            alertRows.Clear();
        }
        private void RefreshAlerts()
        {
            if(alertContent==null)return;
            var active=new HashSet<string>();var w=Runner.Session.World;
            foreach(var b in w.Blueprints.Where(x=>x.Exists))AddAlert(active,WorkCategory.Build,b.Id,b.Position);
            foreach(var b in w.Buildings.Where(x=>x.Exists&&(x.Kind==BuildingKind.Stove||x.DemolitionDesignated)))
                AddAlert(active,b.DemolitionDesignated?WorkCategory.Build:WorkCategory.Cook,b.Id,b.Position);
            foreach(var n in w.Gatherables.Where(x=>!x.Depleted&&x.Designated))AddAlert(active,WorkCategory.Gather,n.Id,n.Position);
            foreach(var c in w.Crops.Where(x=>x.Exists&&x.Stage!=CropStage.Growing))AddAlert(active,WorkCategory.Farm,c.Id,c.Position);
            foreach(var r in w.Resources.Where(x=>x.Exists&&x.Amount>0&&!x.StorageSlotId.HasValue))AddAlert(active,WorkCategory.Haul,r.Id,r.Position);
            foreach(var key in alertRows.Keys.Where(x=>!active.Contains(x)).ToArray())
            {
                var row=alertRows[key];if(row!=null){row.gameObject.SetActive(false);Destroy(row.gameObject);}
                alertRows.Remove(key);buttons.Remove("alert."+key);
            }
            alerts.gameObject.SetActive(alertRows.Count>0);
        }
        private void AddAlert(HashSet<string> active,WorkCategory category,int target,GridPosition position)
        {
            var reason=Runner.Session.World.GetWorkDiagnostic(category,target);
            if(reason!=WorkReasonCode.MissingMaterials&&reason!=WorkReasonCode.NoWorker&&reason!=WorkReasonCode.Unreachable
                &&reason!=WorkReasonCode.OutputFull&&reason!=WorkReasonCode.NoAllowedStorage&&reason!=WorkReasonCode.StorageFull)return;
            ShowAlert(active,target,position,reason,null);
        }
        private void ShowAlert(HashSet<string> active,int target,GridPosition position,WorkReasonCode reason,string group)
        {
            var key=target+"."+reason;active.Add(key);
            if(alertRows.TryGetValue(key,out var existing)&&group==null)return;
            var caption="! "+(group??AlertTargetName(target))+" "+position+"\n"+WorkDiagnostics.GetReasonText(reason);
            if(existing!=null)
            {
                var label=existing.GetComponentInChildren<Text>();
                if(label.text!=caption)label.text=caption;
                return;
            }
            var button=RowButton(alertContent,"alert."+key,caption,()=>{SelectObject(target);FocusTarget(position);},58);
            button.GetComponent<Image>().color=new Color(.34f,.23f,.15f);
            alertRows.Add(key,button);
        }
        private string AlertTargetName(int id)
        {
            var world=Runner.Session.World;
            var blueprint=world.Blueprints.FirstOrDefault(x=>x.Id==id);
            if(blueprint!=null)return ConstructionPanel.Name(blueprint.Kind)+" 설계";
            var building=world.Buildings.FirstOrDefault(x=>x.Id==id);
            if(building!=null)return ConstructionPanel.Name(building.Kind);
            if(world.StorageZones.Any(x=>x.Id==id))return "저장 구역";
            if(world.GrowingZones.Any(x=>x.Id==id))return "경작 구역";
            var resource=world.Resources.FirstOrDefault(x=>x.Id==id);
            if(resource!=null)return SettlementView.ResourceName(resource.Kind);
            var gather=world.Gatherables.FirstOrDefault(x=>x.Id==id);
            if(gather!=null)return gather.Kind==GatherKind.Tree?"나무":"채집 식물";
            return "작물";
        }
    }
}
