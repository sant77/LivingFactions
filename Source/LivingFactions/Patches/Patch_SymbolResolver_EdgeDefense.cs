using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace LivingFactions.Patches
{
    [HarmonyPatch(typeof(SymbolResolver_EdgeDefense), nameof(SymbolResolver_EdgeDefense.Resolve))]
    public static class Patch_SymbolResolver_EdgeDefense
    {
        /// <summary>
        /// Vanilla crea un grupo (Lord) aparte para los guardias del perímetro. Cada Lord huye por su cuenta
        /// al perder cierto % de sus pawns, así que si caían 2 de los 4 guardias salía "están huyendo"
        /// mientras el resto de defensores seguía atacando. Unimos los guardias al grupo principal.
        /// </summary>
        private static void Prefix(ref ResolveParams rp)
        {
            if (rp.singlePawnLord == null && rp.settlementLord != null)
            {
                rp.singlePawnLord = rp.settlementLord;
            }
        }

        /// <summary>
        /// Vanilla solo coloca mini torretas (Turret_MiniTurret fijo en el código). En ciudades y capitales
        /// añadimos torretas pesadas en el mismo anillo. GenStep_Power las conecta a la energía después.
        /// </summary>
        private static void Postfix(ResolveParams rp)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(BaseGen.globalSettings.map);
            if (!tier.HasValue)
            {
                return;
            }
            TierData data = TierData.For(tier.Value);
            Faction faction = rp.faction ?? BaseGen.globalSettings.map.ParentFaction;
            if (faction == null || faction.def.techLevel < TechLevel.Industrial)
            {
                return;
            }
            PushTurrets(rp, faction, DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Autocannon"), data.autocannons);
            PushTurrets(rp, faction, DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Sniper"), data.sniperTurrets);
        }

        private static void PushTurrets(ResolveParams rp, Faction faction, ThingDef turret, int count)
        {
            if (turret == null)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                ResolveParams turretParams = rp;
                turretParams.faction = faction;
                turretParams.singleThingDef = turret;
                turretParams.rect = rp.rect.ContractedBy(1);
                turretParams.edgeThingAvoidOtherEdgeThings = rp.edgeThingAvoidOtherEdgeThings ?? true;
                BaseGen.symbolStack.Push("edgeThing", turretParams);
            }
        }
    }
}
