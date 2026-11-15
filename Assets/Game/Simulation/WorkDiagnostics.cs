using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public static class WorkDiagnostics
    {
        public static WorkReasonCode ForCategory(WorkCategory category, IReadOnlyList<WorkPolicyState> policies,
            IReadOnlyList<WorkReasonCode> reasons)
        {
            var enabled = false;
            if (policies != null)
                foreach (var policy in policies) if (policy.GetPriority(category) > 0) { enabled = true; break; }
            if (!enabled) return WorkReasonCode.NoWorker;
            if (reasons == null || reasons.Count == 0) return WorkReasonCode.NoDesignation;
            // Prefer a feasible result; otherwise choose the same diagnostic regardless of pawn enumeration order.
            var result = WorkReasonCode.NoDesignation;
            var disabled = false;
            foreach (var reason in reasons)
            {
                if (reason == WorkReasonCode.None) return WorkReasonCode.None;
                if (reason == WorkReasonCode.Disabled) { disabled = true; continue; }
                if (reason == WorkReasonCode.NoWorker || reason == WorkReasonCode.NoDesignation) continue;
                if (result == WorkReasonCode.NoDesignation || (int)reason < (int)result) result = reason;
            }
            return result == WorkReasonCode.NoDesignation && disabled ? WorkReasonCode.Disabled : result;
        }
        public static string GetReasonText(WorkReasonCode reason)
        {
            switch (reason)
            {
                case WorkReasonCode.None: return "작업 가능";
                case WorkReasonCode.Disabled: return "개인 작업 끔";
                case WorkReasonCode.NoWorker: return "담당자 없음";
                case WorkReasonCode.Drafted: return "소집 중";
                case WorkReasonCode.NeedsPriority: return "생활 행동 우선";
                case WorkReasonCode.NoDesignation: return "지정된 작업 없음";
                case WorkReasonCode.MissingMaterials: return "재료 부족";
                case WorkReasonCode.Unreachable: return "접근 불가";
                case WorkReasonCode.TargetReserved: return "대상 점유 중";
                case WorkReasonCode.OutputFull: return "출력 공간 부족";
                case WorkReasonCode.TargetReached: return "생산 목표 도달";
                case WorkReasonCode.AwaitingClearance: return "현장 비우기 대기";
                case WorkReasonCode.InvalidTarget: return "대상 사용 불가";
                case WorkReasonCode.NoAllowedStorage: return "허용된 저장 구역 없음";
                case WorkReasonCode.DestinationReserved: return "저장 위치 점유 중";
                case WorkReasonCode.StorageFull: return "저장 공간 부족";
                default: return "작업 대기";
            }
        }
        public static string GetCategoryText(WorkCategory category)
        {
            switch (category)
            {
                case WorkCategory.Gather: return "채집";
                case WorkCategory.Haul: return "운반";
                case WorkCategory.Build: return "건설";
                case WorkCategory.Farm: return "농사";
                case WorkCategory.Cook: return "조리";
                default: return "작업";
            }
        }
    }
}
