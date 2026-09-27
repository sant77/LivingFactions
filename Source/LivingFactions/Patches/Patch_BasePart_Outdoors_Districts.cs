using HarmonyLib;
using LivingFactions.Generation;
using RimWorld.BaseGen;

namespace LivingFactions.Patches
{
    /// <summary>
    /// En ciudades y capitales, la primera llamada a basePart_outdoors (el interior completo de la base) se
    /// reemplaza por el trazado de distritos. Las llamadas internas de BaseGen (dentro de cada pieza) no se tocan.
    /// </summary>
    [HarmonyPatch(typeof(SymbolResolver_BasePart_Outdoors), nameof(SymbolResolver_BasePart_Outdoors.Resolve))]
    public static class Patch_BasePart_Outdoors_Districts
    {
        /// <summary>Lo activa Patch_SymbolResolver_Settlement al generar una ciudad o capital con muralla.</summary>
        public static bool pending;
        public static bool reserveCenter;

        private static bool Prefix(ResolveParams rp)
        {
            if (!pending)
            {
                return true;
            }
            pending = false;
            DistrictPlanner.Plan(rp, reserveCenter);
            return false;
        }
    }
}
