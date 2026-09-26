using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Los animales salvajes también cuestan rendimiento (en una prueba había 69 en el mapa de una capital).
    /// En los mapas de bases NPC con rango se reduce la densidad deseada del bioma. Afecta a la generación
    /// inicial (GenStep_Animals llena hasta este valor) y a la aparición de animales con el tiempo.
    /// </summary>
    [HarmonyPatch(typeof(WildAnimalSpawner), "DesiredTotalAnimalWeight", MethodType.Getter)]
    public static class Patch_WildAnimalSpawner_DesiredWeight
    {
        private static readonly AccessTools.FieldRef<WildAnimalSpawner, Map> MapRef = AccessTools.FieldRefAccess<WildAnimalSpawner, Map>("map");

        private static void Postfix(WildAnimalSpawner __instance, ref float __result)
        {
            float factor = LivingFactionsMod.Settings.settlementAnimalFactor;
            if (factor >= 1f)
            {
                return;
            }
            if (WorldComponent_SettlementTiers.TierOfMap(MapRef(__instance)).HasValue)
            {
                __result *= factor;
            }
        }
    }
}
