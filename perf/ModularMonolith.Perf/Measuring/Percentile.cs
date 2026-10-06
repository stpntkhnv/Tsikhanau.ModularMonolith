namespace ModularMonolith.Perf.Measuring;

internal static class Percentile
{
    public static Double Of(IReadOnlyList<Double> sortedValues, Double fraction)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var rank = fraction * (sortedValues.Count - 1);
        var lowIndex = (Int32)Math.Floor(rank);
        var highIndex = (Int32)Math.Ceiling(rank);
        return sortedValues[lowIndex] + (sortedValues[highIndex] - sortedValues[lowIndex]) * (rank - lowIndex);
    }
}
