using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingFactions
{
    /// <summary>
    /// Guarda el rango de cada asentamiento NPC (clave: WorldObject.ID).
    /// RimWorld instancia automáticamente todas las subclases de WorldComponent.
    /// </summary>
    public class WorldComponent_SettlementTiers : WorldComponent
    {
        private Dictionary<int, SettlementTier> tiers = new Dictionary<int, SettlementTier>();

        // Listas temporales que usa Scribe para guardar el diccionario.
        private List<int> tmpKeys;
        private List<SettlementTier> tmpValues;

        public WorldComponent_SettlementTiers(World world) : base(world)
        {
        }

        public static WorldComponent_SettlementTiers Instance => Find.World?.GetComponent<WorldComponent_SettlementTiers>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tiers, "tiers", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && tiers == null)
            {
                tiers = new Dictionary<int, SettlementTier>();
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            RemoveMissingSettlements();
            // Mundo nuevo o partida anterior al mod: asignar rangos a todas las facciones.
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (IsEligible(faction) && SettlementsOf(faction).Any(s => !tiers.ContainsKey(s.ID)))
                {
                    AssignTiers(faction);
                }
            }
        }

        public static bool IsEligible(Faction faction)
        {
            return faction != null && !faction.IsPlayer;
        }

        /// <summary>
        /// Solo bases en la superficie. Las orbitales (Odyssey, ej. Gremio de Comerciantes) usan otro
        /// generador de mapas (SettlementPlatform) que los parches de la Fase 1 no modifican.
        /// </summary>
        public static bool IsEligible(Settlement settlement)
        {
            return settlement != null && IsEligible(settlement.Faction) && settlement.Tile.Layer.IsRootSurface;
        }

        /// <summary>Rango del asentamiento, o null si no aplica (jugador, órbita, sitio, etc.).</summary>
        public SettlementTier? TierOf(Settlement settlement)
        {
            if (!IsEligible(settlement))
            {
                return null;
            }
            if (!tiers.TryGetValue(settlement.ID, out SettlementTier tier))
            {
                // Asentamiento creado después de la generación del mundo.
                AssignTiers(settlement.Faction);
                tier = tiers.TryGetValue(settlement.ID, out SettlementTier t) ? t : SettlementTier.Town;
            }
            return tier;
        }

        /// <summary>
        /// Rango forzado durante la generación de una base de prueba (herramienta de depuración). Mientras
        /// está activo, cualquier mapa se trata como una base de ese rango.
        /// </summary>
        public static SettlementTier? ForcedTier { get; set; }

        public static bool GeneratingTestBase => ForcedTier.HasValue;

        public static SettlementTier? TierOfMap(Map map)
        {
            if (!LivingFactionsMod.Settings.enabled)
            {
                return null;
            }
            if (ForcedTier.HasValue)
            {
                return ForcedTier;
            }
            return Instance?.TierOf(map?.Parent as Settlement);
        }

        private static IEnumerable<Settlement> SettlementsOf(Faction faction)
        {
            return Find.WorldObjects.Settlements.Where(s => s.Faction == faction && IsEligible(s));
        }

        private void RemoveMissingSettlements()
        {
            // También limpia rangos que partidas anteriores asignaron a bases orbitales.
            HashSet<int> alive = new HashSet<int>(Find.WorldObjects.Settlements.Where(IsEligible).Select(s => s.ID));
            foreach (int id in tiers.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                tiers.Remove(id);
            }
        }

        /// <summary>
        /// Asigna rango a los asentamientos de la facción que aún no tienen.
        /// Si la facción no tiene capital, la capital es el asentamiento más central.
        /// </summary>
        private void AssignTiers(Faction faction)
        {
            List<Settlement> all = SettlementsOf(faction).ToList();
            List<Settlement> pending = all.Where(s => !tiers.ContainsKey(s.ID)).ToList();
            if (pending.Count == 0)
            {
                return;
            }

            // Semilla estable: el mismo mundo da los mismos rangos.
            Rand.PushState(Gen.HashCombineInt(Find.World.info.Seed, faction.loadID));
            try
            {
                bool hasCapital = all.Any(s => tiers.TryGetValue(s.ID, out SettlementTier t) && t == SettlementTier.Capital);
                bool firstAssignment = pending.Count == all.Count;

                if (!hasCapital)
                {
                    Settlement capital = MostCentral(pending);
                    tiers[capital.ID] = SettlementTier.Capital;
                    pending.Remove(capital);
                }

                if (firstAssignment)
                {
                    // Ciudades: ~20 % de los restantes, entre 0 y el máximo de las opciones.
                    int cities = Mathf.Clamp(Mathf.RoundToInt(pending.Count * 0.2f), pending.Count >= 3 ? 1 : 0, LivingFactionsMod.Settings.maxCitiesPerFaction);
                    foreach (Settlement s in pending.InRandomOrder().Take(cities).ToList())
                    {
                        tiers[s.ID] = SettlementTier.City;
                        pending.Remove(s);
                    }
                    foreach (Settlement s in pending)
                    {
                        tiers[s.ID] = Rand.Chance(0.6f) ? SettlementTier.Town : SettlementTier.Outpost;
                    }
                }
                else
                {
                    // Asentamientos nuevos en una facción ya establecida: empiezan como puesto avanzado.
                    foreach (Settlement s in pending)
                    {
                        tiers[s.ID] = SettlementTier.Outpost;
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static Settlement MostCentral(List<Settlement> settlements)
        {
            WorldGrid grid = Find.WorldGrid;
            return settlements.MinBy(a =>
            {
                Vector3 pa = grid.GetTileCenter(a.Tile);
                return settlements.Sum(b => Vector3.Distance(pa, grid.GetTileCenter(b.Tile)));
            });
        }

        // Para la acción de depuración.
        public IEnumerable<(Faction faction, Settlement settlement, SettlementTier tier)> AllTiers()
        {
            foreach (Settlement s in Find.WorldObjects.Settlements)
            {
                SettlementTier? tier = TierOf(s);
                if (tier.HasValue)
                {
                    yield return (s.Faction, s, tier.Value);
                }
            }
        }
    }
}
