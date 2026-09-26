using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Una base no cae mientras le queden oleadas de refuerzo por llegar: si el jugador acaba con la
    /// guarnición, las oleadas pendientes se envían antes de darla por derrotada.
    /// </summary>
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.IsDefeated))]
    public static class Patch_SettlementDefeatUtility_IsDefeated
    {
        private static bool Prefix(Map map, Faction faction, ref bool __result)
        {
            MapComponent_SettlementInfo info = map?.GetComponent<MapComponent_SettlementInfo>();
            if (info != null && info.WavesPending && faction == map.ParentFaction && faction.HostileTo(Faction.OfPlayer))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
