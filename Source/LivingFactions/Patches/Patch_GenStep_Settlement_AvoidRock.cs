using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Ciudades y capitales con muralla evitan ubicarse donde la roca natural cubre más del 15 % del área:
    /// la montaña corta la ciudad en trozos e impide construir la muralla. El filtro es tolerante: si ningún
    /// sitio lo cumple, se busca otra vez sin él (si no, el juego no generaría la base).
    /// </summary>
    [HarmonyPatch(typeof(GenStep_Scatterer), "TryFindScatterCell")]
    public static class Patch_GenStep_Settlement_AvoidRock
    {
        private const float MaxRockFraction = 0.15f;
        private const int SampleStep = 3;

        private static readonly MethodInfo TryFindScatterCell = AccessTools.Method(typeof(GenStep_Scatterer), "TryFindScatterCell");

        private static bool rockFilter;
        private static bool retrying;

        private static void Prefix(GenStep_Scatterer __instance, Map map)
        {
            if (__instance is GenStep_Settlement && !retrying && IsWalledTier(map))
            {
                rockFilter = true;
            }
        }

        private static void Postfix(GenStep_Scatterer __instance, Map map, ref IntVec3 result, ref bool __result)
        {
            if (!(__instance is GenStep_Settlement) || retrying || !rockFilter)
            {
                return;
            }
            rockFilter = false;
            if (__result)
            {
                return;
            }
            retrying = true;
            try
            {
                object[] args = { map, null };
                __result = (bool)TryFindScatterCell.Invoke(__instance, args);
                result = (IntVec3)args[1];
                if (Prefs.DevMode)
                {
                    Log.Message("[Living Factions] No hay sitio con poca roca para la base; se ubica sin ese filtro.");
                }
            }
            finally
            {
                retrying = false;
            }
        }

        private static bool IsWalledTier(Map map)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            return tier.HasValue && TierData.For(tier.Value).walled;
        }

        /// <summary>Solo activo durante la primera búsqueda de sitio de una ciudad o capital.</summary>
        [HarmonyPatch(typeof(GenStep_Settlement), "CanScatterAt")]
        public static class Patch_CanScatterAt
        {
            private static void Postfix(IntVec3 c, Map map, ref bool __result)
            {
                if (!__result || !rockFilter)
                {
                    return;
                }
                SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
                if (!tier.HasValue)
                {
                    return;
                }
                int size = TierData.For(tier.Value).size.min;
                CellRect rect = CellRect.CenteredOn(c, size / 2).ClipInsideMap(map);
                int samples = 0;
                int rock = 0;
                for (int x = rect.minX; x <= rect.maxX; x += SampleStep)
                {
                    for (int z = rect.minZ; z <= rect.maxZ; z += SampleStep)
                    {
                        samples++;
                        Building edifice = new IntVec3(x, 0, z).GetEdifice(map);
                        if (edifice != null && edifice.def.mineable)
                        {
                            rock++;
                        }
                    }
                }
                if (samples > 0 && (float)rock / samples > MaxRockFraction)
                {
                    __result = false;
                }
            }
        }
    }
}
