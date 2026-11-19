using System.Linq;
using RimworldTweak.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace RimworldTweak.Presentation
{
    public sealed partial class SettlementView
    {
        public Button DeveloperButton { get; private set; }
        public Button RaidButton { get; private set; }
        public Button AttackButton { get; private set; }
        public Button RepairButton { get; private set; }
        public Text RaidStatusText { get; private set; }
        private readonly Text[] raidMarkers = new Text[2];
        private bool choosingAttackTarget;

        private void BuildCombatUI()
        {
            DeveloperButton = MakeButton("개발자 모드", workPanel, 536, 3, 152);
            DeveloperButton.onClick.AddListener(() => {
                if (Runner == null || Runner.IsLoadConfirmationOpen) return;
                Runner.ToggleDeveloperMode();
                RefreshNow();
            });
            RaidButton = MakeButton("일반 습격 발생", inspectionPanel, 790, 150, 190);
            RaidButton.onClick.AddListener(() => {
                Runner.StartDebugRaid();
                RefreshNow();
            });
            AttackButton = MakeButton("공격 지정", inspectionPanel, 790, 104, 190);
            AttackButton.onClick.AddListener(() => {
                CancelWorkTool();
                choosingAttackTarget = !choosingAttackTarget;
                RefreshNow();
            });
            RepairButton = MakeButton("수리 지정", inspectionPanel, 790, 58, 190);
            RepairButton.onClick.AddListener(() => {
                var site = Runner.Session.World.Buildings.FirstOrDefault(b => b.Exists && b.Id == SelectedObjectId);
                if (site != null) Runner.SetRepair(site.Id, !site.RepairDesignated);
                RefreshNow();
            });
            LifeText.rectTransform.sizeDelta = new Vector2(450, 60);
            CargoText.rectTransform.sizeDelta = new Vector2(450, 30);
            DestinationText.rectTransform.sizeDelta = new Vector2(450, 30);
            ClaimText.rectTransform.sizeDelta = new Vector2(450, 30);
            RaidStatusText = Label("Raid status", uiRoot.transform, "", 560, 638, 665, 36, 16, Ink);
            for (var index = 0; index < raidMarkers.Length; index++)
            {
                raidMarkers[index] = Label("Raid marker " + index, board, "", 0, 0, 48, 32, 12, Color.white);
                raidMarkers[index].alignment = TextAnchor.MiddleCenter;
                raidMarkers[index].rectTransform.pivot = Vector2.one * .5f;
                var background = raidMarkers[index].gameObject.AddComponent<Outline>();
                background.effectColor = new Color(.35f, .04f, .04f, 1f);
                background.effectDistance = new Vector2(2, -2);
            }
        }

        private bool TryHandleAttackClick(WorldState world, GridPosition position, bool rightButton)
        {
            if (!choosingAttackTarget) return false;
            if (rightButton) choosingAttackTarget = false;
            else
            {
                var raider = world.Raiders.LastOrDefault(enemy => enemy.Position == position);
                if (raider != null && SelectedColonistId is int pawnId)
                    choosingAttackTarget = !Runner.OrderAttack(pawnId, raider.Id);
            }
            RefreshNow();
            return true;
        }

        private void RefreshCombatUI(WorldState world)
        {
            var pawn = world.Colonists.FirstOrDefault(p => p.Id == SelectedColonistId);
            var site = world.Buildings.FirstOrDefault(b => b.Exists && b.Id == SelectedObjectId);
            var active = world.RaidPhase == RaidPhase.Active;
            var showRaid = Runner.DeveloperMode || world.RaidPhase != RaidPhase.Idle;
            var available = !Runner.IsLoadConfirmationOpen;
            DeveloperButton.GetComponentInChildren<Text>().text = Runner.DeveloperMode ? "개발자 모드 켜짐" : "개발자 모드";
            RaidButton.gameObject.SetActive(Runner.DeveloperMode);
            RaidButton.interactable = available && (world.RaidPhase == RaidPhase.Idle || world.RaidPhase == RaidPhase.Resolved);
            AttackButton.gameObject.SetActive(showRaid);
            AttackButton.interactable = available && active && pawn != null && pawn.IsDrafted && !pawn.IsDowned;
            if (!AttackButton.interactable) choosingAttackTarget = false;
            AttackButton.GetComponentInChildren<Text>().text = choosingAttackTarget ? "맵에서 적 선택" : "공격 지정";
            RepairButton.gameObject.SetActive(site?.Durability < 100);
            RepairButton.interactable = available && site != null && !site.DemolitionDesignated;
            RepairButton.GetComponentInChildren<Text>().text = site?.RepairDesignated == true ? "수리 취소" : "수리 지정";
            RaidStatusText.gameObject.SetActive(showRaid);
            var stage = world.RaidPhase switch {
                RaidPhase.Warning => "습격 예고 · 적 2명 · " + world.RaidRemaining + "틱",
                RaidPhase.Active => "교전 · 적 " + world.Raiders.Count + "명 · " + world.RaidRemaining + "틱" +
                    (world.Raiders.Any(enemy => enemy.Attack.BlockedReason == "Unreachable") ? " · 접근이 막힌 적 있음" : ""),
                RaidPhase.Retreating => "적 철수 중 · " + world.RaidRemaining + "틱",
                RaidPhase.Resolved => (world.RaidOutcome switch {
                    RaidOutcome.Defended => world.NormalRaid ? "방어 성공 · 적 전멸" : "방어 성공 · 보호 시설 유지",
                    RaidOutcome.ObjectiveLost => "방어 실패 · 보호 시설 고장",
                    RaidOutcome.TimeLimit => "교전 제한 시간 종료",
                    _ => "패배 · 전원 전투 불능 · 불러오기 또는 초기화"
                }) + " · 부상 " + world.RaidInjuredCount + "명 · 전투 불능 " + world.RaidDownedCount +
                    "명" + (world.NormalRaid ? "" : " · 시설 피해 " + world.RaidFacilityDamage) +
                    (world.RaidForcedRetreats > 0 ? " · 철수 시간 종료 " + world.RaidForcedRetreats + "명" : ""),
                _ => "개발자 모드 · 일반 습격을 발생시킬 수 있습니다"
            };
            if (world.NormalRaid && active)
                stage = "일반 습격 · " + string.Join(" / ", world.Raiders.Select(enemy =>
                    "적 #" + enemy.Id + " → 정착민 #" + enemy.Attack.TargetId));
            RaidStatusText.text = stage;
            if (site != null && site.Durability < 100)
                RaidStatusText.text += " | " + (site.Durability == 0 ? "고장" : "내구도 " + site.Durability) +
                    " · 목재 " + site.RepairWood + "/2 · 수리 " + site.RepairProgress + "/40" +
                    (site.RepairDesignated ? " · " + WorkDiagnostics.GetReasonText(world.GetRepairDiagnostic(site.Id)) : "");
            if (pawn != null && showRaid)
            {
                NeedsText.text += "    " + (pawn.IsDowned ? "전투 불능" : "체력 " + pawn.Health + "/100");
                if (pawn.TreatedBy.HasValue) LifeText.text += "\n치료자 #" + pawn.TreatedBy.Value;
                else if (pawn.Health < 100 && !pawn.IsDrafted) LifeText.text += "\n치료 · " + WorkDiagnostics.GetReasonText(world.GetCareDiagnostic(pawn.Id));
                if (pawn.FeatureStep == WorkStep.Care) LifeText.text += "\n치료 진행 " + pawn.CareProgress + "/40";
                if (pawn.Attack.TargetId.HasValue)
                    LifeText.text += "\n표적 #" + pawn.Attack.TargetId + " · " + (pawn.Attack.BlockedReason switch {
                        "AttackOutOfRange" => "사거리 밖",
                        "AttackBlocked" => "사선 차단",
                        _ => pawn.Attack.Cooldown > 0 ? "공격 대기 " + pawn.Attack.Cooldown : "공격 준비 " + pawn.Attack.Preparation + "/10"
                    });
            }
            for (var index = 0; index < raidMarkers.Length; index++)
            {
                var marker = raidMarkers[index];
                var warning = world.RaidPhase == RaidPhase.Warning;
                var raider = index < world.Raiders.Count ? world.Raiders[index] : null;
                marker.gameObject.SetActive(warning || raider != null);
                if (!marker.gameObject.activeSelf) continue;
                var position = warning ? world.RaidEntrance(index) : raider.Position;
                var size = board.rect.width / world.Width;
                marker.rectTransform.anchoredPosition = new Vector2((position.X + .5f) * size, (position.Y + .5f) * size);
                marker.rectTransform.SetAsLastSibling();
                marker.text = warning ? "▼\n진입" : (world.NormalRaid ? "◆" + raider.Id : "◆") + "\n" + raider.Health;
                marker.color = warning ? new Color(1f, .76f, .34f) : new Color(1f, .53f, .48f);
            }
        }
    }
}
