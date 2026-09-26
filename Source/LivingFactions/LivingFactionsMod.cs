using HarmonyLib;
using UnityEngine;
using Verse;

namespace LivingFactions
{
    public class LivingFactionsSettings : ModSettings
    {
        public bool enabled = true;
        public float defenderMultiplier = 1f;
        public float lootMultiplier = 1f;
        public int maxCitiesPerFaction = 3;
        public bool showTierInInspect = true;
        public bool autoMeasure = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoMeasure, "autoMeasure", true);
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref defenderMultiplier, "defenderMultiplier", 1f);
            Scribe_Values.Look(ref lootMultiplier, "lootMultiplier", 1f);
            Scribe_Values.Look(ref maxCitiesPerFaction, "maxCitiesPerFaction", 3);
            Scribe_Values.Look(ref showTierInInspect, "showTierInInspect", true);
        }
    }

    public class LivingFactionsMod : Mod
    {
        public const string HarmonyId = "santiagoduque.livingfactions";

        public static LivingFactionsSettings Settings { get; private set; }

        public LivingFactionsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LivingFactionsSettings>();
            new Harmony(HarmonyId).PatchAll();
            Log.Message("[Living Factions] Cargado.");
        }

        public override string SettingsCategory() => "Living Factions";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("LF_Settings_Phase1".Translate());
            list.GapLine();
            list.CheckboxLabeled("LF_Settings_Enabled".Translate(), ref Settings.enabled, "LF_Settings_Enabled_Desc".Translate());
            list.CheckboxLabeled("LF_Settings_ShowTier".Translate(), ref Settings.showTierInInspect);

            list.Gap();
            list.Label("LF_Settings_DefenderMultiplier".Translate(Settings.defenderMultiplier.ToStringPercent()));
            Settings.defenderMultiplier = Mathf.Round(list.Slider(Settings.defenderMultiplier, 0.25f, 2f) * 20f) / 20f;

            list.Label("LF_Settings_LootMultiplier".Translate(Settings.lootMultiplier.ToStringPercent()));
            Settings.lootMultiplier = Mathf.Round(list.Slider(Settings.lootMultiplier, 0.25f, 2f) * 20f) / 20f;

            list.Label("LF_Settings_MaxCities".Translate(Settings.maxCitiesPerFaction));
            Settings.maxCitiesPerFaction = Mathf.RoundToInt(list.Slider(Settings.maxCitiesPerFaction, 0f, 6f));

            list.Gap();
            list.Label("LF_Settings_ExistingNote".Translate());

            if (Prefs.DevMode)
            {
                list.Gap();
                list.CheckboxLabeled("LF_Settings_AutoMeasure".Translate(), ref Settings.autoMeasure, "LF_Settings_AutoMeasure_Desc".Translate());
            }

            list.End();
        }
    }
}
