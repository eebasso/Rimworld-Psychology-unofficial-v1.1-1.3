using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using HarmonyLib;

namespace Psychology.Harmony
{
    [HarmonyPatch(typeof(ThoughtWorker_CreepyBreathing), "CurrentSocialStateInternal")]
    public static class ThoughtWorker_CreepyBreathingPatch
    {
        [LogPerformance]
        [HarmonyPostfix]
        public static void Disable(ref ThoughtState __result, Pawn pawn, Pawn other)
        {
            if (__result.StageIndex != ThoughtState.Inactive.StageIndex)
            {
                //if (PsycheHelper.PsychologyEnabled(pawn) && PsycheHelper.PsychologyEnabled(other))
                if (PsychologyBase.TraitOpinionMultiplier() == 0f)
                {
                    __result = false;
                }
            }
        }
    }
}
