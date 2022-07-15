﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.Sound;
using HarmonyLib;
//using EdB.PrepareCarefully;

namespace Psychology.Harm
{
    [HarmonyPatch(typeof(EdB.PrepareCarefully.PanelBackstory), nameof(EdB.PrepareCarefully.PanelBackstory.Draw))]
    public static class EdBPrepareCarefully_PanelBackstory
    {
        [HarmonyPrefix]
        public static void Draw(EdB.PrepareCarefully.State state, float y)
        {
            Pawn pawn = state.CurrentPawn.Pawn;
            float y2 = y + EdB.PrepareCarefully.PanelModule.Margin.y;
            if (PsycheHelper.PsychologyEnabled(pawn))
            {
                Rect psycheRect = new Rect(230f, y2 + 5f, 28f, 28f);
                Color oldColor = GUI.color;
                GUI.color = psycheRect.Contains(Event.current.mousePosition) ? PsychColor.ButtonLightColor : PsychColor.ButtonDarkColor;
                GUI.DrawTexture(psycheRect, PsychologyTexCommand.PsycheButton);
                GUI.color = oldColor;
                if (Widgets.ButtonInvisible(psycheRect, false))
                {
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                    Find.WindowStack.Add(new Dialog_ViewPsyche(pawn, true));
                }
            }
        }
    }
}