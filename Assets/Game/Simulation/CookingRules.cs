using System;
using System.Collections.Generic;
namespace RimworldTweak.Simulation
{
    public static class CookingRules
    {
        public const int Duration = 40;
        public const int IngredientsPerMeal = 2;
        public const int MealsPerCycle = 1;
        public const int DefaultTarget = 12;
        public const int MaximumTarget = 99;
        public static IReadOnlyList<WorkCandidate> GetCandidates(WorkReadContext context,int pawnId)
        {
            if(context==null) throw new ArgumentNullException(nameof(context));
            var result=new List<WorkCandidate>();
            if(context.PawnId==pawnId)
                foreach(var option in context.Options)
                    if(option.Reason==WorkReasonCode.None && option.Candidate.Category==WorkCategory.Cook)
                        result.Add(option.Candidate);
            return result.AsReadOnly();
        }
        public static FeatureWorkResult EvaluateStep(WorkReadContext context,int pawnId)
        {
            if(context==null) throw new ArgumentNullException(nameof(context));
            var source=context.ActiveWork;
            if(source==null || context.PawnId!=pawnId || source.PawnId!=pawnId || source.Step!=WorkStep.Cook)
                return new FeatureWorkResult(source,source?.Progress??0,false,ResourceKind.Ingredients,0,ResourceKind.Meal,0,WorkReasonCode.InvalidTarget);
            var reason=source.Reason;
            if(source.Progress<0 || source.Progress>=Duration || source.Duration!=Duration || source.RequiredInput!=IngredientsPerMeal)
                reason=WorkReasonCode.InvalidTarget;
            var progress=reason==WorkReasonCode.None ? source.Progress+1 : source.Progress;
            var completed=reason==WorkReasonCode.None && progress==Duration;
            return new FeatureWorkResult(source,progress,completed,ResourceKind.Ingredients,completed?IngredientsPerMeal:0,
                ResourceKind.Meal,completed?MealsPerCycle:0,reason);
        }
    }
}
