using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions
{
    /// <summary>
    /// Datos de la base NPC de este mapa: cómo se generó y el estado de sus oleadas de refuerzo.
    /// RimWorld crea automáticamente un MapComponent de cada tipo en todos los mapas.
    /// </summary>
    public class MapComponent_SettlementInfo : MapComponent
    {
        private const int CheckInterval = 250;

        // Generación.
        public bool generated;
        public SettlementTier tier;
        public float defenderPoints;
        public IntVec2 size;
        public int turrets;
        public int mortars;
        public int guards;

        // Oleadas.
        public FactionStyle style;
        public float[] waveThresholds = new float[0];
        public float pointsPerWave;
        private int wavesSent;
        private int lastWaveTick = -99999;
        private const int MinTicksBetweenWaves = GenDate.TicksPerHour;
        private List<Pawn> garrison = new List<Pawn>();
        private int garrisonInitial;
        private bool garrisonCounted;

        // Ciudadela: casillas de la despensa (única fuente de comida además de las raciones).
        public List<IntVec3> pantryCells = new List<IntVec3>();
        // Ciudadela: salón del mando (donde está el comandante con su guardia).
        public List<IntVec3> hallCells = new List<IntVec3>();
        // Ciudadela: sala de energía, el almacén de combustible y acero del mantenimiento.
        public List<IntVec3> depotCells = new List<IntVec3>();
        // Tribus: puntos de emboscada pendientes (se activan cuando un colono se acerca).
        public List<IntVec3> ambushPoints = new List<IntVec3>();
        public float pointsPerAmbush;
        private const float AmbushTriggerRadius = 12f;

        // Comandante de la capital: se anuncia con una carta al llegar.
        public Pawn commander;
        private bool commanderAnnounced;

        // Mantenimiento: edificios recargables de la facción (se refresca cada cierto tiempo, no se guarda).
        private List<Building> refuelables = new List<Building>();
        private int refuelablesTick = -99999;
        private const int RefuelablesRefreshTicks = 2000;

        // Medición automática (solo modo desarrollador). No se guarda: se repite al recargar.
        private int ticksOnMap;
        private bool measuredCalm;
        private bool measuredCombat;

        public MapComponent_SettlementInfo(Map map) : base(map)
        {
        }

        public int TotalWaves => waveThresholds.Length;

        public bool WavesPending => generated && wavesSent < TotalWaves;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generated, "generated");
            Scribe_Values.Look(ref tier, "tier");
            Scribe_Values.Look(ref defenderPoints, "defenderPoints");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref turrets, "turrets");
            Scribe_Values.Look(ref mortars, "mortars");
            Scribe_Values.Look(ref guards, "guards");

            Scribe_Values.Look(ref style, "style");
            List<float> thresholds = waveThresholds?.ToList();
            Scribe_Collections.Look(ref thresholds, "waveThresholds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                waveThresholds = thresholds?.ToArray() ?? new float[0];
            }
            Scribe_Values.Look(ref pointsPerWave, "pointsPerWave");
            Scribe_Values.Look(ref wavesSent, "wavesSent");
            Scribe_Values.Look(ref lastWaveTick, "lastWaveTick", -99999);
            Scribe_Collections.Look(ref garrison, "garrison", LookMode.Reference);
            Scribe_Values.Look(ref garrisonInitial, "garrisonInitial");
            Scribe_Values.Look(ref garrisonCounted, "garrisonCounted");
            Scribe_Collections.Look(ref pantryCells, "pantryCells", LookMode.Value);
            Scribe_Collections.Look(ref hallCells, "hallCells", LookMode.Value);
            Scribe_Collections.Look(ref depotCells, "depotCells", LookMode.Value);
            Scribe_References.Look(ref commander, "commander");
            Scribe_Collections.Look(ref ambushPoints, "ambushPoints", LookMode.Value);
            Scribe_Values.Look(ref pointsPerAmbush, "pointsPerAmbush");
            Scribe_Values.Look(ref commanderAnnounced, "commanderAnnounced");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                garrison ??= new List<Pawn>();
                garrison.RemoveAll(p => p == null);
                pantryCells ??= new List<IntVec3>();
                hallCells ??= new List<IntVec3>();
                depotCells ??= new List<IntVec3>();
                ambushPoints ??= new List<IntVec3>();
            }
        }

        public override void MapComponentTick()
        {
            if (!generated)
            {
                return;
            }
            ticksOnMap++;
            if (ticksOnMap % CheckInterval != 0)
            {
                return;
            }
            if (WavesPending)
            {
                CheckWaves();
            }
            if (!commanderAnnounced)
            {
                AnnounceCommander();
            }
            if (ambushPoints.Count > 0)
            {
                CheckAmbushes();
            }
            if (Prefs.DevMode && LivingFactionsMod.Settings.autoMeasure)
            {
                CheckAutoMeasure();
            }
        }

        // ---------------- Oleadas ----------------

        private Faction Faction => map.ParentFaction;

        private void CheckWaves()
        {
            Faction faction = Faction;
            if (faction == null || faction.IsPlayer || !faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }
            if (!garrisonCounted)
            {
                garrison = map.mapPawns.SpawnedPawnsInFaction(faction).Where(p => p.RaceProps.Humanlike).ToList();
                garrisonInitial = garrison.Count;
                garrisonCounted = true;
                return;
            }
            // Sin colonos en pie no hay asalto en curso: los refuerzos esperan.
            if (!map.mapPawns.FreeColonistsSpawned.Any(p => !p.Downed))
            {
                return;
            }
            if (LossFraction() < waveThresholds[wavesSent])
            {
                return;
            }
            // Si el límite de enemigos retrasó una oleada, la siguiente no llega pegada a ella.
            if (Find.TickManager.TicksGame - lastWaveTick < MinTicksBetweenWaves)
            {
                return;
            }
            int maxActive = LivingFactionsMod.Settings.maxActiveEnemies + (style == FactionStyle.Tribal ? LivingFactionsMod.Settings.tribalExtraEnemies : 0);
            if (ActiveEnemies(faction) >= maxActive)
            {
                return;
            }
            SendWave(faction);
        }

        private float LossFraction()
        {
            if (garrisonInitial <= 0)
            {
                return 1f;
            }
            int standing = garrison.Count(p => p.Spawned && p.Map == map && !p.Dead && !p.Downed);
            return 1f - (float)standing / garrisonInitial;
        }

        private int ActiveEnemies(Faction faction)
        {
            return map.mapPawns.SpawnedPawnsInFaction(faction).Count(p => p.RaceProps.Humanlike && !p.Downed);
        }

        private void SendWave(Faction faction)
        {
            bool lastWave = wavesSent == TotalWaves - 1;
            wavesSent++;
            lastWaveTick = Find.TickManager.TicksGame;

            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                points = pointsPerWave,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = FactionStyleUtility.WaveArrivalMode(style, lastWave)
            };
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
                {
                    Log.Warning($"[Living Factions] No se encontró por dónde enviar la oleada {wavesSent} en {map.Parent?.Label}.");
                    return;
                }
            }

            PawnGroupMakerParms groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms, ensureCanGenerateAtLeastOnePawn: true);
            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
            if (pawns.Count == 0)
            {
                Log.Warning($"[Living Factions] La oleada {wavesSent} de {faction.Name} no generó pawns ({pointsPerWave:F0} pts).");
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                if (pawn.needs?.food != null)
                {
                    pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
                }
            }
            parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false), map, pawns);

            string label = "LF_WaveLetterLabel".Translate(wavesSent, TotalWaves);
            string text = (lastWave ? "LF_WaveLetterTextLast" : "LF_WaveLetterText").Translate(faction.NameColored, pawns.Count);
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, new LookTargets(pawns), faction);

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Oleada {wavesSent}/{TotalWaves} de {faction.Name}: {pawns.Count} pawns, {pointsPerWave:F0} pts, " +
                    $"llegada {parms.raidArrivalMode.defName}, pérdidas de la guarnición {LossFraction().ToStringPercent()}.");
            }
        }

        /// <summary>Emboscada tribal: al acercarse un colono a un punto, aparecen guerreros desde la cobertura.</summary>
        private void CheckAmbushes()
        {
            Faction faction = Faction;
            if (faction == null || !faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed).ToList();
            if (colonists.Count == 0)
            {
                return;
            }
            for (int i = ambushPoints.Count - 1; i >= 0; i--)
            {
                IntVec3 point = ambushPoints[i];
                Pawn target = colonists.FirstOrDefault(p => p.Position.InHorDistOf(point, AmbushTriggerRadius));
                if (target == null)
                {
                    continue;
                }
                ambushPoints.RemoveAt(i);
                SpawnAmbush(faction, point, target);
            }
        }

        private void SpawnAmbush(Faction faction, IntVec3 point, Pawn target)
        {
            PawnGroupMakerParms parms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = System.Math.Max(pointsPerAmbush, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))
            };
            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(parms).ToList();
            if (pawns.Count == 0)
            {
                return;
            }
            foreach (Pawn pawn in pawns)
            {
                if (pawn.needs?.food != null)
                {
                    pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
                }
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(point, map, 4);
                GenSpawn.Spawn(pawn, cell, map);
            }
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false), map, pawns);
            Find.LetterStack.ReceiveLetter("LF_AmbushLabel".Translate(), "LF_AmbushText".Translate(faction.NameColored, pawns.Count, target.LabelShort),
                LetterDefOf.ThreatBig, new LookTargets(pawns), faction);
            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Emboscada de {faction.Name}: {pawns.Count} guerreros ({pointsPerAmbush:F0} pts) junto a {target.LabelShort}.");
            }
        }

        /// <summary>Carta al llegar: quién defiende la ciudadela. Al hacer clic, la cámara va al comandante.</summary>
        private void AnnounceCommander()
        {
            if (commander == null || !commander.Spawned || commander.Dead)
            {
                commanderAnnounced = true;
                return;
            }
            // Espera a que lleguen los colonos del jugador.
            if (map.mapPawns.FreeColonistsSpawnedCount == 0)
            {
                return;
            }
            commanderAnnounced = true;
            Faction faction = commander.Faction;
            Find.LetterStack.ReceiveLetter(
                commander.LabelShort,
                "LF_CommanderLetter".Translate(faction?.NameColored ?? "", commander.LabelShort, commander.kindDef.label),
                LetterDefOf.NeutralEvent, new LookTargets(commander), faction);
        }

        // ---------------- Mantenimiento ----------------

        /// <summary>Edificios de la facción con combustible o cañón recargable (generadores, torretas, morteros).</summary>
        public List<Building> Refuelables
        {
            get
            {
                if (Find.TickManager.TicksGame - refuelablesTick > RefuelablesRefreshTicks)
                {
                    refuelablesTick = Find.TickManager.TicksGame;
                    refuelables.Clear();
                    Faction faction = Faction;
                    foreach (Building b in map.listerBuildings.allBuildingsNonColonist)
                    {
                        if (b.Faction == faction && b.TryGetComp<CompRefuelable>() != null)
                        {
                            refuelables.Add(b);
                        }
                    }
                }
                return refuelables;
            }
        }

        // ---------------- Medición automática ----------------

        private void CheckAutoMeasure()
        {
            if (measuredCalm && measuredCombat)
            {
                return;
            }
            if (!measuredCombat && DefendersAssaulting())
            {
                // Si la medición de calma sigue en curso, se reintenta en la siguiente comprobación.
                measuredCombat = PerformanceMeter.Start(map, "combate");
                measuredCalm = true;
            }
            else if (!measuredCalm && ticksOnMap >= 500)
            {
                measuredCalm = PerformanceMeter.Start(map, "calma, antes del combate");
            }
        }

        private bool DefendersAssaulting()
        {
            foreach (Lord lord in map.lordManager.lords)
            {
                if (lord.LordJob is LordJob_DefendBase && lord.CurLordToil is LordToil_AssaultColony)
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (!generated)
            {
                return "sin datos de Living Factions";
            }
            string waves = TotalWaves > 0 ? $", oleadas {wavesSent}/{TotalWaves} de {pointsPerWave:F0} pts ({style})" : "";
            return $"{tier}, tamaño {size.x}x{size.z}, guarnición {defenderPoints:F0} pts ({garrisonInitial} pawns), " +
                $"torretas {turrets}, morteros {mortars}, guardias {guards}{waves}";
        }
    }
}
