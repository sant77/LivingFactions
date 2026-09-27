using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Vanilla hace huir a cada grupo (Lord) al perder el 40–70 % de sus pawns. En una base con puestos
    /// pequeños y oleadas eso hacía huir a grupos enteros tras 1–2 bajas. Los defensores de su propia base:
    /// - Capital: nadie huye.
    /// - Ciudad: solo la reserva central (LordJob_DefendBase) puede huir, como en vanilla.
    /// LordJob_DefendPoint sobrescribe AddFleeToil, así que los puestos se crean con addFleeToil = false.
    /// </summary>
    [HarmonyPatch(typeof(LordJob), nameof(LordJob.AddFleeToil), MethodType.Getter)]
    public static class Patch_LordJob_AddFleeToil
    {
        private static void Postfix(LordJob __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            Lord lord = __instance.lord;
            Map map = lord?.lordManager?.map;
            if (map == null || lord.faction == null || lord.faction != map.ParentFaction)
            {
                return;
            }
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            if (tier == SettlementTier.Capital || (tier == SettlementTier.City && !(__instance is LordJob_DefendBase)))
            {
                __result = false;
            }
        }
    }
}
