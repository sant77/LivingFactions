using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>Muestra el rango del asentamiento en el panel de inspección del mapa del mundo.</summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetInspectString))]
    public static class Patch_Settlement_GetInspectString
    {
        private static void Postfix(Settlement __instance, ref string __result)
        {
            if (!LivingFactionsMod.Settings.enabled || !LivingFactionsMod.Settings.showTierInInspect)
            {
                return;
            }
            SettlementTier? tier = WorldComponent_SettlementTiers.Instance?.TierOf(__instance);
            if (!tier.HasValue)
            {
                return;
            }
            string line = "LF_TierInspect".Translate(tier.Value.LabelCap());
            __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
        }
    }
}
