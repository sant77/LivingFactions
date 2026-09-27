using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LivingFactions.AI;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Solo en modo desarrollador: cuando los defensores de una base con rango pasan al ataque, escribe en el
    /// log qué lo provocó. Vanilla tiene DebugViewSettings.logLordToilTransitions, pero registra todo.
    /// </summary>
    public static class Patch_LogAssaultReason
    {
        private static readonly Dictionary<Lord, Type> lastTrigger = new Dictionary<Lord, Type>();

        private static readonly Dictionary<Type, string> Reasons = new Dictionary<Type, string>
        {
            { typeof(Trigger_TicksPassed), "se acabó su tiempo de espera (vanilla: ~10 h en la reserva, 2 días en los puestos)" },
            { typeof(Trigger_ChanceOnTickInterval), "azar (vanilla: 3 % cada hora de juego)" },
            { typeof(Trigger_PawnHarmed), "hirieron a uno de ellos" },
            { typeof(Trigger_ChanceOnPlayerHarmNPCBuilding), "el jugador dañó un edificio de la base" },
            { typeof(Trigger_FractionPawnsLost), "perdieron parte del grupo" },
            { typeof(Trigger_UrgentlyHungry), "hambre urgente" },
            { typeof(Trigger_OnClamor), "habilidad psíquica ruidosa cerca" },
            { typeof(Trigger_LFGarrisonStarving), "la mayoría del grupo tiene hambre urgente (sin raciones ni despensa)" }
        };

        [HarmonyPatch]
        public static class RecordTrigger
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (Type type in Reasons.Keys)
                {
                    yield return AccessTools.Method(type, nameof(Trigger.ActivateOn));
                }
            }

            private static void Postfix(Trigger __instance, Lord lord, bool __result)
            {
                if (__result && Prefs.DevMode && lord != null)
                {
                    lastTrigger[lord] = __instance.GetType();
                }
            }
        }

        [HarmonyPatch(typeof(Lord), nameof(Lord.GotoToil))]
        public static class LogTransition
        {
            private static void Postfix(Lord __instance, LordToil newLordToil)
            {
                if (!Prefs.DevMode || !(newLordToil is LordToil_AssaultColony))
                {
                    return;
                }
                if (!(__instance.LordJob is LordJob_DefendBase || __instance.LordJob is LordJob_LFPerimeterPost))
                {
                    return;
                }
                Map map = __instance.lordManager?.map;
                if (map == null || !WorldComponent_SettlementTiers.TierOfMap(map).HasValue)
                {
                    return;
                }
                string reason = lastTrigger.TryGetValue(__instance, out Type type) && Reasons.TryGetValue(type, out string text) ? text : "desconocido";
                string group = __instance.LordJob is LordJob_DefendBase ? "La reserva central" : "Un puesto del perímetro";
                Log.Message($"[Living Factions] {group} ({__instance.ownedPawns.Count} pawns) pasa al ataque: {reason}.");
                lastTrigger.Remove(__instance);
            }
        }
    }
}
