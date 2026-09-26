using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Vanilla usa un tamaño fijo (GenStep_Settlement.SettlementSizeRange = 34..38) para toda base NPC.
    /// Reemplazamos cada lectura de ese campo por un rango que depende del rango del asentamiento.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GenStep_Settlement
    {
        private static readonly FieldInfo SizeRangeField = AccessTools.Field(typeof(GenStep_Settlement), "SettlementSizeRange");
        private static readonly IntRange VanillaSizeRange = (IntRange)SizeRangeField.GetValue(null);

        private static IntRange currentSizeRange = VanillaSizeRange;

        public static IntRange CurrentSizeRange() => currentSizeRange;

        public static ref IntRange CurrentSizeRangeRef() => ref currentSizeRange;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GenStep_Settlement), "CanScatterAt");
            yield return AccessTools.Method(typeof(GenStep_Settlement), "ScatterAt");
        }

        // Ambos métodos reciben el mapa como segundo argumento.
        private static void Prefix(Map map)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            currentSizeRange = tier.HasValue ? TierData.For(tier.Value).size : VanillaSizeRange;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo byValue = AccessTools.Method(typeof(Patch_GenStep_Settlement), nameof(CurrentSizeRange));
            MethodInfo byRef = AccessTools.Method(typeof(Patch_GenStep_Settlement), nameof(CurrentSizeRangeRef));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand is FieldInfo field && field == SizeRangeField)
                {
                    if (instruction.opcode == OpCodes.Ldsfld)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = byValue;
                        replaced++;
                    }
                    else if (instruction.opcode == OpCodes.Ldsflda)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = byRef;
                        replaced++;
                    }
                }
                yield return instruction;
            }
            if (replaced == 0)
            {
                Log.Error("[Living Factions] No se encontró SettlementSizeRange en GenStep_Settlement. ¿Cambió el juego?");
            }
        }
    }
}
