using Verse;

namespace LivingFactions
{
    public enum SettlementTier : byte
    {
        Outpost = 0,
        Town = 1,
        City = 2,
        Capital = 3
    }

    /// <summary>
    /// Parámetros de generación por rango. Valores iniciales: pendientes de balance (ver PLAN.md).
    /// </summary>
    public class TierData
    {
        public IntRange size;
        public FloatRange defenderPoints;
        public float lootMarketValue;
        // null = comportamiento vanilla.
        public int? edgeDefenseWidth;
        // Celdas de borde por torreta / mortero (vanilla: 30 / 75). null = vanilla.
        public int? cellsPerTurret;
        public int? cellsPerMortar;
        public int edgeGuards;
        public int minBarracks;

        // Oleadas: fracción de los puntos que defiende desde el inicio, y % de pérdidas de la
        // guarnición que dispara cada oleada. El resto de puntos se reparte entre las oleadas.
        public float garrisonShare = 1f;
        public float[] waveThresholds = new float[0];
        // Las tribus pelean en número: más oleadas, más pequeñas.
        public float[] tribalWaveThresholds = new float[0];

        public float[] WaveThresholdsFor(FactionStyle style)
        {
            return style == FactionStyle.Tribal && tribalWaveThresholds.Length > 0 ? tribalWaveThresholds : waveThresholds;
        }

        public static readonly TierData Outpost = new TierData
        {
            size = new IntRange(24, 30),
            defenderPoints = new FloatRange(500f, 900f),
            lootMarketValue = 900f,
            edgeDefenseWidth = 0,
            minBarracks = 1
        };

        public static readonly TierData Town = new TierData
        {
            size = new IntRange(34, 40),
            defenderPoints = new FloatRange(1100f, 1600f),
            lootMarketValue = 1800f,
            minBarracks = 1
        };

        public static readonly TierData City = new TierData
        {
            size = new IntRange(50, 60),
            defenderPoints = new FloatRange(2500f, 4000f),
            lootMarketValue = 4000f,
            edgeDefenseWidth = 4,
            cellsPerTurret = 20,
            cellsPerMortar = 60,
            edgeGuards = 4,
            minBarracks = 2,
            garrisonShare = 0.6f,
            waveThresholds = new[] { 0.4f, 0.7f },
            tribalWaveThresholds = new[] { 0.3f, 0.55f, 0.75f }
        };

        public static readonly TierData Capital = new TierData
        {
            size = new IntRange(76, 88),
            defenderPoints = new FloatRange(6000f, 9000f),
            lootMarketValue = 9000f,
            edgeDefenseWidth = 6,
            cellsPerTurret = 14,
            cellsPerMortar = 40,
            edgeGuards = 10,
            minBarracks = 3,
            garrisonShare = 0.4f,
            waveThresholds = new[] { 0.3f, 0.55f, 0.75f },
            tribalWaveThresholds = new[] { 0.25f, 0.45f, 0.6f, 0.75f }
        };

        public static TierData For(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Outpost: return Outpost;
                case SettlementTier.City: return City;
                case SettlementTier.Capital: return Capital;
                default: return Town;
            }
        }
    }

    public static class SettlementTierExtensions
    {
        public static string LabelCap(this SettlementTier tier)
        {
            return ("LF_Tier_" + tier).Translate();
        }
    }
}
