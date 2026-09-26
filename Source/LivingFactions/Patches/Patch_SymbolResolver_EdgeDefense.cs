using HarmonyLib;
using RimWorld.BaseGen;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Vanilla crea un grupo (Lord) aparte para los guardias del perímetro. Cada Lord huye por su cuenta
    /// al perder cierto % de sus pawns, así que si caían 2 de los 4 guardias salía "están huyendo"
    /// mientras el resto de defensores seguía atacando. Unimos los guardias al grupo principal.
    /// </summary>
    [HarmonyPatch(typeof(SymbolResolver_EdgeDefense), nameof(SymbolResolver_EdgeDefense.Resolve))]
    public static class Patch_SymbolResolver_EdgeDefense
    {
        private static void Prefix(ref ResolveParams rp)
        {
            if (rp.singlePawnLord == null && rp.settlementLord != null)
            {
                rp.singlePawnLord = rp.settlementLord;
            }
        }
    }
}
