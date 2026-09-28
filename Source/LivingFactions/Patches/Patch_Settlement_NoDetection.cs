using HarmonyLib;
using RimWorld.Planet;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Vanilla (Settlement.PostMapGenerate) inicia al entrar a cualquier base NPC una cuenta atrás de 4 días
    /// (TimedDetectionRaids) tras la cual llegan raids al mapa, para que el jugador no acampe indefinidamente.
    /// En ciudades y capitales ese papel lo cumplen las oleadas de refuerzo y chocaba con los asedios largos:
    /// ahí se desactiva. En los pueblos se mantiene.
    /// </summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.PostMapGenerate))]
    public static class Patch_Settlement_NoDetection
    {
        private static void Postfix(Settlement __instance)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(__instance.Map);
            if ((tier == SettlementTier.City || tier == SettlementTier.Capital) && __instance.TryGetComponent(out TimedDetectionRaids detection))
            {
                detection.ResetCountdown();
            }
        }
    }
}
