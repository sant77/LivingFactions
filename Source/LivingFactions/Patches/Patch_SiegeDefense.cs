using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.Patches
{
    /// <summary>
    /// En ciudades y capitales la reserva central no sale a atacar por tiempo (~10 h) ni por azar (3 % por hora):
    /// esos disparadores existen para que las bases pequeñas vanilla no esperen eternamente, pero una capital
    /// no abandona sus murallas sin motivo. Sigue saliendo si la hieren, si dañan la base, si pierde defensores
    /// o si tiene hambre.
    /// </summary>
    [HarmonyPatch(typeof(LordJob_DefendBase), nameof(LordJob_DefendBase.CreateGraph))]
    public static class Patch_LordJob_DefendBase_NoTimer
    {
        private static void Postfix(LordJob_DefendBase __instance, StateGraph __result)
        {
            Map map = __instance.lord?.lordManager?.map;
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            if (tier != SettlementTier.City && tier != SettlementTier.Capital)
            {
                return;
            }
            if (__instance.lord.faction != map.ParentFaction)
            {
                return;
            }
            foreach (Transition transition in __result.transitions)
            {
                if (transition.target is LordToil_AssaultColony)
                {
                    transition.triggers.RemoveAll(t => t is Trigger_TicksPassed || t is Trigger_ChanceOnTickInterval);
                }
            }
        }
    }

    /// <summary>
    /// Vanilla deja una sola pila de 5–8 proyectiles por mortero. Como la guarnición de una ciudad o capital
    /// ya no sale sola, su respuesta a un asedio es la artillería: pilas extra junto a cada mortero.
    /// </summary>
    [HarmonyPatch(typeof(SymbolResolver_MannedMortar), nameof(SymbolResolver_MannedMortar.Resolve))]
    public static class Patch_MannedMortar_ExtraShells
    {
        private static void Postfix(ResolveParams rp)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(BaseGen.globalSettings.map);
            int extraStacks = tier == SettlementTier.Capital ? 3 : tier == SettlementTier.City ? 2 : 0;
            Faction faction = rp.faction ?? BaseGen.globalSettings.map.ParentFaction;
            if (extraStacks == 0 || faction == null)
            {
                return;
            }
            ThingDef mortar = rp.mortarDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Mortar");
            ThingDef shell = mortar == null ? null : TurretGunUtility.TryFindRandomShellDef(mortar, allowEMP: false, allowToxGas: false,
                mustHarmHealth: true, faction.def.techLevel, allowAntigrainWarhead: false, 250f, faction);
            if (shell == null)
            {
                return;
            }
            for (int i = 0; i < extraStacks; i++)
            {
                ResolveParams shells = rp;
                shells.faction = faction;
                shells.singleThingDef = shell;
                shells.singleThingStackCount = shell.stackLimit;
                BaseGen.symbolStack.Push("thing", shells);
            }
        }
    }
}
