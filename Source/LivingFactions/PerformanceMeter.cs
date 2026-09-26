using System.Diagnostics;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingFactions
{
    /// <summary>
    /// Medición de rendimiento para pruebas: TPS real, tiempo por tick y pawns en el mapa.
    /// Se activa con la acción de depuración y escribe el resultado en Player.log.
    /// Fuera de una medición solo cuesta comprobar un bool por tick.
    /// </summary>
    public class PerformanceMeter : GameComponent
    {
        private const float DurationSeconds = 30f;

        public static bool Active { get; private set; }

        private static readonly Stopwatch tickWatch = new Stopwatch();
        private static float startRealTime;
        private static int ticks;
        private static int frames;
        private static double totalTickMs;
        private static double maxTickMs;
        private static int pausedFrames;
        private static float targetTpsSum;
        private static Map measuredMap;

        public PerformanceMeter(Game game)
        {
        }

        public static void Start()
        {
            if (Active)
            {
                Messages.Message("[Living Factions] Ya hay una medición en curso.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            measuredMap = Find.CurrentMap;
            ticks = 0;
            frames = 0;
            totalTickMs = 0;
            maxTickMs = 0;
            pausedFrames = 0;
            targetTpsSum = 0;
            startRealTime = Time.realtimeSinceStartup;
            Active = true;
            Messages.Message($"[Living Factions] Midiendo rendimiento durante {DurationSeconds:F0} s. No pauses el juego.", MessageTypeDefOf.NeutralEvent, false);
        }

        public override void GameComponentUpdate()
        {
            if (!Active)
            {
                return;
            }
            frames++;
            TickManager tm = Find.TickManager;
            if (tm.Paused)
            {
                pausedFrames++;
            }
            else
            {
                targetTpsSum += 60f * tm.TickRateMultiplier;
            }
            if (Time.realtimeSinceStartup - startRealTime >= DurationSeconds)
            {
                Finish();
            }
        }

        private static void Finish()
        {
            Active = false;
            float elapsed = Time.realtimeSinceStartup - startRealTime;
            int runningFrames = frames - pausedFrames;
            float targetTps = runningFrames > 0 ? targetTpsSum / runningFrames : 0f;
            float tps = ticks / elapsed;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Living Factions] ===== Medición de rendimiento =====");
            sb.AppendLine($"Duración: {elapsed:F1} s reales, {frames} frames ({frames / elapsed:F0} FPS), pausado en {pausedFrames} frames");
            sb.AppendLine($"TPS real: {tps:F0} / objetivo {targetTps:F0} ({(targetTps > 0 ? tps / targetTps : 0f).ToStringPercent()})");
            sb.AppendLine($"Tiempo por tick: promedio {(ticks > 0 ? totalTickMs / ticks : 0):F2} ms, máximo {maxTickMs:F2} ms ({ticks} ticks)");

            if (measuredMap != null && Find.Maps.Contains(measuredMap))
            {
                sb.AppendLine($"Mapa: {measuredMap.Parent?.Label ?? "?"} ({measuredMap.Size.x}x{measuredMap.Size.z})");
                sb.AppendLine($"Base: {measuredMap.GetComponent<MapComponent_SettlementInfo>()}");

                var pawns = measuredMap.mapPawns.AllPawnsSpawned;
                int colonists = pawns.Count(p => p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike);
                int hostileHumans = pawns.Count(p => p.RaceProps.Humanlike && p.HostileTo(Faction.OfPlayer));
                int hostileHumansActive = pawns.Count(p => p.RaceProps.Humanlike && p.HostileTo(Faction.OfPlayer) && !p.Downed);
                int mechs = pawns.Count(p => p.RaceProps.IsMechanoid);
                int animals = pawns.Count(p => p.RaceProps.Animal);
                sb.AppendLine($"Pawns: {pawns.Count} en total | tuyos {colonists} | enemigos humanos {hostileHumans} ({hostileHumansActive} en pie) | mecanoides {mechs} | animales {animals}");
                int turrets = measuredMap.listerBuildings.allBuildingsNonColonist.Count(b => b is Building_Turret);
                sb.AppendLine($"Torretas enemigas en el mapa: {turrets}");
            }
            if (pausedFrames > frames / 2)
            {
                sb.AppendLine("AVISO: el juego estuvo pausado más de la mitad del tiempo; la medición no es fiable.");
            }
            Log.Message(sb.ToString().TrimEnd());
            Messages.Message("[Living Factions] Medición terminada. Resultado en el log.", MessageTypeDefOf.NeutralEvent, false);
        }

        [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
        public static class Patch_TickManager_DoSingleTick
        {
            private static void Prefix()
            {
                if (Active)
                {
                    tickWatch.Restart();
                }
            }

            private static void Postfix()
            {
                if (!Active)
                {
                    return;
                }
                tickWatch.Stop();
                double ms = tickWatch.Elapsed.TotalMilliseconds;
                ticks++;
                totalTickMs += ms;
                if (ms > maxTickMs)
                {
                    maxTickMs = ms;
                }
            }
        }
    }
}
