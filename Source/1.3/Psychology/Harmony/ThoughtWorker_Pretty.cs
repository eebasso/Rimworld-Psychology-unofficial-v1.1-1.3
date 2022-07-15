﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using HarmonyLib;

// TODO: CHANGE BEAUTY OVERRIDE TO A MULTIPLIER THAT REDUCES MAGNITUDE OF OPINION CHANGES FROM VARIOUS TRAITS INCLUDING BODYMODDER/TRANSHUMANIST

namespace Psychology.Harm
{
    [HarmonyPatch(typeof(ThoughtWorker_Pretty), "CurrentSocialStateInternal")]
    public static class ThoughtWorker_PrettyPatch
    {
        [LogPerformance]
        [HarmonyPostfix]
        public static void Disable(ref ThoughtState __result, Pawn pawn, Pawn other)
        {
            if (__result.StageIndex != ThoughtState.Inactive.StageIndex && PsychologyBase.BeautyOverride())
            {
                if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight) == 0f)
                {
                    __result = false;
                }
                if (RelationsUtility.IsDisfigured(other))
                {
                    __result = false;
                }
                if (PsycheHelper.PsychologyEnabled(pawn) && PsycheHelper.PsychologyEnabled(other))
                {
                    __result = false;
                }
            }
        }
    }
}
