using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Psychology.Harmony
{
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.Notify_RescuedBy))]
    public static class Notify_RescuedBy_BleedingHeartPatch
    {
        [LogPerformance]
        [HarmonyPostfix]
        public static void AddBleedingHeartThought(Pawn_RelationsTracker __instance, Pawn rescuer)
        {
            if (rescuer.RaceProps.Humanlike && __instance.canGetRescuedThought)
            {
                rescuer.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RescuedBleedingHeart, Traverse.Create(__instance).Field("pawn").GetValue<Pawn>());
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.SecondaryLovinChanceFactor))]
    public static class Pawn_RelationsTracker_LovinChancePatch
    {
        [LogPerformance]
        [HarmonyPostfix]
        public static void PsychologyFormula(Pawn_RelationsTracker __instance, ref float __result, Pawn otherPawn)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (PsycheHelper.PsychologyEnabled(pawn))
            {
                /* Throw away the existing result and substitute our own formula. */
                /* This formula is now used to determine dating chance. Loving frequency is determined by additional calculations  */
                if (pawn.def != otherPawn.def || pawn == otherPawn || otherPawn.AnimalOrWildMan())
                {
                    __result = 0f;
                    return;
                }

                /* SEXUAL PREFERENCE FACTOR */
                float sexualityFactor = 1f;
                /* Psychology result */
                if (PsychologyBase.ActivateKinsey())
                {
                    float kinsey = 3 - PsycheHelper.Comp(pawn).Sexuality.kinseyRating;
                    float homo = (pawn.gender == otherPawn.gender) ? 1f : -1f;
                    sexualityFactor = Mathf.InverseLerp(3f, 0f, kinsey * homo);
                }
                /* Vanilla result */
                // Vanilla Asexual, Bisexual, and Gay traits
                else if (pawn.story != null && pawn.story.traits != null)
                {
                    if (pawn.story.traits.HasTrait(TraitDefOf.Asexual))
                    {
                        __result = 0f;
                        return;
                    }
                    if (otherPawn.gender == pawn.gender)
                    {
                        if (!pawn.story.traits.HasTrait(TraitDefOf.Gay) && !pawn.story.traits.HasTrait(TraitDefOf.Bisexual))
                        {
                            __result = 0f;
                            return;
                        }
                        // Vanilla: pawns are less likely to hit recipients who are the same gender and not Gay nor Bisexual
                        if (!otherPawn.story.traits.HasTrait(TraitDefOf.Gay) && !otherPawn.story.traits.HasTrait(TraitDefOf.Bisexual))
                        {
                            sexualityFactor = 0.15f;
                        }
                    }
                    else
                    {
                        if (pawn.story.traits.HasTrait(TraitDefOf.Gay))
                        {
                            __result = 0f;
                            return;
                        }
                        // Vanilla: pawns are less likely to hit recipients who are not the same gender and Gay
                        if (otherPawn.story.traits.HasTrait(TraitDefOf.Gay))
                        {
                            sexualityFactor = 0.15f;
                        }
                    }
                }


                /* GET PAWN PERSONALITY VALUES */
                //float pawnEmpathetic = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Empathetic);
                float pawnExperimental = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
                float pawnPure = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Pure);
                float pawnSexDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive;
                float pawnRomanceDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedRomanticDrive;
                float otherPawnCool = PsycheHelper.Comp(otherPawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Cool);
                float pawnOpenMinded = pawn.story.traits.HasTrait(TraitDefOfPsychology.OpenMinded) ? 1f : 0f;
                bool pawnLecher = pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher);

                /* AGE FACTORS */
                float ageFactor = 1f;
                float ageBiologicalYearsFloat = pawn.ageTracker.AgeBiologicalYearsFloat;
                float ageBiologicalYearsFloat2 = otherPawn.ageTracker.AgeBiologicalYearsFloat;
                float minDatingAge1 = 13f;
                float minDatingAge2 = ageBiologicalYearsFloat - Mathf.Max(1f, 4f + 6f * (pawnExperimental - pawnPure + pawnOpenMinded));
                minDatingAge2 = Mathf.Clamp(minDatingAge2, minDatingAge1, 18f);
                float maxDatingAge2 = ageBiologicalYearsFloat + Mathf.Max(1f, 4f + 6f * (pawnExperimental - pawnPure + pawnOpenMinded));
                if (pawnLecher)
                {
                    minDatingAge2 = minDatingAge1;
                    maxDatingAge2 = 10000f;
                }
                if (ageBiologicalYearsFloat < minDatingAge1 || ageBiologicalYearsFloat2 < minDatingAge2)
                {
                    __result = 0f;
                    return;
                }
                if (ageBiologicalYearsFloat < 18f && ageBiologicalYearsFloat2 > maxDatingAge2)
                {
                    __result = 0f;
                    return;
                }
                if (pawn.gender == Gender.Male)
                {
                    float min = ageBiologicalYearsFloat - 30f;
                    float lower = ageBiologicalYearsFloat - 10f;
                    float upper = ageBiologicalYearsFloat + 3f;
                    float max = ageBiologicalYearsFloat + 10f;
                    float minY = Mathf.Clamp01(0.2f + 0.8f * Mathf.Pow(pawnExperimental, 2) - 0.4f * pawnPure + 0.5f * pawnOpenMinded);
                    float maxY = minY;
                    if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
                    {
                        minY = 1f;
                        maxY = 1f;
                    }
                    ageFactor = GenMath.FlatHill(minY, min, lower, upper, max, maxY, ageBiologicalYearsFloat2);
                }
                else if (pawn.gender == Gender.Female)
                {
                    float min2 = ageBiologicalYearsFloat - 10f;
                    float lower2 = ageBiologicalYearsFloat - 3f;
                    float upper2 = ageBiologicalYearsFloat + 10f;
                    float max2 = ageBiologicalYearsFloat + 30f;
                    float minY2 = Mathf.Clamp01(0.2f + 0.8f * Mathf.Pow(pawnExperimental, 2) - 0.4f * pawnPure + 0.5f * pawnOpenMinded);
                    float maxY2 = minY2;
                    if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
                    {
                        minY2 = 1f;
                        maxY2 = 1f;
                    }
                    ageFactor = GenMath.FlatHill(minY2, min2, lower2, upper2, max2, maxY2, ageBiologicalYearsFloat2);
                }

                /* BEAUTY FACTOR */
                float pawnBeauty = pawn.GetStatValue(StatDefOf.PawnBeauty);
                float otherPawnBeauty = otherPawn.GetStatValue(StatDefOf.PawnBeauty);
                otherPawnBeauty += 2f * otherPawnCool - 1f;
                /* Beautiful pawns will have higher beauty standards. Everyone wants to date out of league */
                float beautyFactor = otherPawnBeauty - 0.75f * pawnBeauty;
                //if (beautyFactor > 0)
                //{
                //    /* Cool Pawns enhance their beauty */
                //    beautyFactor *= 0.67f * (1f + otherPawnCool);
                //}
                //else
                //{
                //    /* Empathetic pawns don't care as much about negative beauty */
                //    beautyFactor *= 1.5f - pawnEmpathetic;
                //}
                /* Cool pawns are more attractive */
                beautyFactor += 2f * otherPawnCool - 1f;
                /* Open Minded pawns don't care about beauty */
                beautyFactor *= 1f - pawnOpenMinded;
                /* Pawns who can't see as well can't determine beauty as well. */
                beautyFactor *= 0.1f + 0.9f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
                /* Turn into multiplicative factor */
                beautyFactor = Mathf.Pow(0.5f + 1f / (1f + Mathf.Pow(6f, -beautyFactor)), 2f);

                /* PAWN SEX AND ROMANCE DRIVE FACTORS */
                // Should this go into MtbLovingHours instead?
                float pawnDriveFactor = 0.5f * Mathf.Pow(pawnSexDrive, 2) + 0.5f * pawnRomanceDrive;

                /* DISABILITY FACTOR */
                // Should this go into MtbLovingHours instead?
                //float disabilityFactor = 0f;
                //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking));
                //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
                //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
                ///* More experimental pawns won't care as much about disabilities */
                //disabilityFactor = Mathf.Lerp(disabilityFactor, 0f, PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental));
                ///* Open minded pawns won't care as much about disabilities */
                //disabilityFactor *= 1f - 0.9f * pawnOpenMinded;
                ///* Turn into multiplicative factor */
                //disabilityFactor = Mathf.Pow(2, disabilityFactor);


                /*  MULTIPLY TO GET RESULT */
                __result = sexualityFactor * ageFactor * beautyFactor * pawnDriveFactor;
            }
        }
    }


    //[HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.SecondaryRomanceChanceFactor))]
    //public static class Pawn_RelationsTracker_RomanceChancePatch
    //{
    //    [LogPerformance]
    //    [HarmonyPostfix]
    //    public static void PsychologyFormula(Pawn_RelationsTracker __instance, ref float __result, Pawn otherPawn)
    //    {
    //        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
    //        if (PsycheHelper.PsychologyEnabled(pawn))
    //        {
    //            /* Throw away the existing result and substitute our own formula. */
    //            /* This formula is now used to determine dating chance. Loving frequency is determined by additional calculations  */
    //            if (pawn.def != otherPawn.def || pawn == otherPawn || otherPawn.AnimalOrWildMan())
    //            {
    //                __result = 0f;
    //                return;
    //            }

    //            /* SEXUAL PREFERENCE FACTOR */
    //            float sexualityFactor = 1f;
    //            /* Psychology result */
    //            if (PsychologyBase.ActivateKinsey())
    //            {
    //                float kinsey = 3 - PsycheHelper.Comp(pawn).Sexuality.kinseyRating;
    //                float homo = (pawn.gender == otherPawn.gender) ? 1f : -1f;
    //                sexualityFactor = Mathf.InverseLerp(3f, 0f, kinsey * homo);
    //            }
    //            /* Vanilla result */
    //            // Vanilla Asexual, Bisexual, and Gay traits
    //            else if (pawn.story != null && pawn.story.traits != null)
    //            {
    //                if (pawn.story.traits.HasTrait(TraitDefOf.Asexual))
    //                {
    //                    __result = 0f;
    //                    return;
    //                }
    //                if (otherPawn.gender == pawn.gender)
    //                {
    //                    if (!pawn.story.traits.HasTrait(TraitDefOf.Gay) && !pawn.story.traits.HasTrait(TraitDefOf.Bisexual))
    //                    {
    //                        __result = 0f;
    //                        return;
    //                    }
    //                    // Vanilla: pawns are less likely to hit recipients who are the same gender and not Gay nor Bisexual
    //                    if (!otherPawn.story.traits.HasTrait(TraitDefOf.Gay) && !otherPawn.story.traits.HasTrait(TraitDefOf.Bisexual))
    //                    {
    //                        sexualityFactor = 0.15f;
    //                    }
    //                }
    //                else
    //                {
    //                    if (pawn.story.traits.HasTrait(TraitDefOf.Gay))
    //                    {
    //                        __result = 0f;
    //                        return;
    //                    }
    //                    // Vanilla: pawns are less likely to hit recipients who are not the same gender and Gay
    //                    if (otherPawn.story.traits.HasTrait(TraitDefOf.Gay))
    //                    {
    //                        sexualityFactor = 0.15f;
    //                    }
    //                }
    //            }


    //            /* GET PAWN PERSONALITY VALUES */
    //            //float pawnEmpathetic = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Empathetic);
    //            float pawnExperimental = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
    //            float pawnPure = PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Pure);
    //            float pawnSexDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive;
    //            float pawnRomanceDrive = PsycheHelper.Comp(pawn).Sexuality.AdjustedRomanticDrive;
    //            float otherPawnCool = PsycheHelper.Comp(otherPawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Cool);
    //            float pawnOpenMinded = pawn.story.traits.HasTrait(TraitDefOfPsychology.OpenMinded) ? 1f : 0f;
    //            bool pawnLecher = pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher);

    //            /* AGE FACTORS */
    //            float ageFactor = 1f;
    //            float ageBiologicalYearsFloat = pawn.ageTracker.AgeBiologicalYearsFloat;
    //            float ageBiologicalYearsFloat2 = otherPawn.ageTracker.AgeBiologicalYearsFloat;
    //            float minDatingAge1 = 13f;
    //            float minDatingAge2 = ageBiologicalYearsFloat - Mathf.Max(1f, 4f + 6f * (pawnExperimental - pawnPure + pawnOpenMinded));
    //            minDatingAge2 = Mathf.Clamp(minDatingAge2, minDatingAge1, 18f);
    //            float maxDatingAge2 = ageBiologicalYearsFloat + Mathf.Max(1f, 4f + 6f * (pawnExperimental - pawnPure + pawnOpenMinded));
    //            if (pawnLecher)
    //            {
    //                minDatingAge2 = minDatingAge1;
    //                maxDatingAge2 = 10000f;
    //            }
    //            if (ageBiologicalYearsFloat < minDatingAge1 || ageBiologicalYearsFloat2 < minDatingAge2)
    //            {
    //                __result = 0f;
    //                return;
    //            }
    //            if (ageBiologicalYearsFloat < 18f && ageBiologicalYearsFloat2 > maxDatingAge2)
    //            {
    //                __result = 0f;
    //                return;
    //            }
    //            if (pawn.gender == Gender.Male)
    //            {
    //                float min = ageBiologicalYearsFloat - 30f;
    //                float lower = ageBiologicalYearsFloat - 10f;
    //                float upper = ageBiologicalYearsFloat + 3f;
    //                float max = ageBiologicalYearsFloat + 10f;
    //                float minY = Mathf.Clamp01(0.2f + 0.8f * Mathf.Pow(pawnExperimental, 2) - 0.4f * pawnPure + 0.5f * pawnOpenMinded);
    //                float maxY = minY;
    //                if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
    //                {
    //                    minY = 1f;
    //                    maxY = 1f;
    //                }
    //                ageFactor = GenMath.FlatHill(minY, min, lower, upper, max, maxY, ageBiologicalYearsFloat2);
    //            }
    //            else if (pawn.gender == Gender.Female)
    //            {
    //                float min2 = ageBiologicalYearsFloat - 10f;
    //                float lower2 = ageBiologicalYearsFloat - 3f;
    //                float upper2 = ageBiologicalYearsFloat + 10f;
    //                float max2 = ageBiologicalYearsFloat + 30f;
    //                float minY2 = Mathf.Clamp01(0.2f + 0.8f * Mathf.Pow(pawnExperimental, 2) - 0.4f * pawnPure + 0.5f * pawnOpenMinded);
    //                float maxY2 = minY2;
    //                if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
    //                {
    //                    minY2 = 1f;
    //                    maxY2 = 1f;
    //                }
    //                ageFactor = GenMath.FlatHill(minY2, min2, lower2, upper2, max2, maxY2, ageBiologicalYearsFloat2);
    //            }

    //            /* BEAUTY FACTOR */
    //            float pawnBeauty = pawn.GetStatValue(StatDefOf.PawnBeauty);
    //            float otherPawnBeauty = otherPawn.GetStatValue(StatDefOf.PawnBeauty);
    //            otherPawnBeauty += 2f * otherPawnCool - 1f;
    //            /* Beautiful pawns will have higher beauty standards. Everyone wants to date out of league */
    //            float beautyFactor = otherPawnBeauty - 0.75f * pawnBeauty;
    //            //if (beautyFactor > 0)
    //            //{
    //            //    /* Cool Pawns enhance their beauty */
    //            //    beautyFactor *= 0.67f * (1f + otherPawnCool);
    //            //}
    //            //else
    //            //{
    //            //    /* Empathetic pawns don't care as much about negative beauty */
    //            //    beautyFactor *= 1.5f - pawnEmpathetic;
    //            //}
    //            /* Cool pawns are more attractive */
    //            beautyFactor += 2f * otherPawnCool - 1f;
    //            /* Open Minded pawns don't care about beauty */
    //            beautyFactor *= 1f - pawnOpenMinded;
    //            /* Pawns who can't see as well can't determine beauty as well. */
    //            beautyFactor *= 0.1f + 0.9f * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
    //            /* Turn into multiplicative factor */
    //            beautyFactor = Mathf.Pow(0.5f + 1f / (1f + Mathf.Pow(6f, -beautyFactor)), 2f);

    //            /* PAWN SEX AND ROMANCE DRIVE FACTORS */
    //            // Should this go into MtbLovingHours instead?
    //            float pawnDriveFactor = 0.5f * Mathf.Pow(pawnSexDrive, 2) + 0.5f * pawnRomanceDrive;

    //            /* DISABILITY FACTOR */
    //            // Should this go into MtbLovingHours instead?
    //            //float disabilityFactor = 0f;
    //            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking));
    //            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation));
    //            //disabilityFactor += Mathf.Lerp(0f, -2f, otherPawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
    //            ///* More experimental pawns won't care as much about disabilities */
    //            //disabilityFactor = Mathf.Lerp(disabilityFactor, 0f, PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental));
    //            ///* Open minded pawns won't care as much about disabilities */
    //            //disabilityFactor *= 1f - 0.9f * pawnOpenMinded;
    //            ///* Turn into multiplicative factor */
    //            //disabilityFactor = Mathf.Pow(2, disabilityFactor);


    //            /*  MULTIPLY TO GET RESULT */
    //            __result = sexualityFactor * ageFactor * beautyFactor * pawnDriveFactor;
    //        }
    //    }
    //}
}
