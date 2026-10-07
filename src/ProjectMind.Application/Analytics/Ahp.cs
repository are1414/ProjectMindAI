using System.Globalization;
using ProjectMind.Application.Common;

namespace ProjectMind.Application.Analytics;

public sealed record AhpResult(IReadOnlyList<decimal> Weights, decimal LambdaMax, decimal ConsistencyIndex, decimal ConsistencyRatio)
{
    /// <summary>Saaty: CR &lt; 0,10 ise karşılaştırmalar tutarlı kabul edilir.</summary>
    public bool IsConsistent => ConsistencyRatio < Ahp.ConsistencyThreshold;
}

/// <summary>
/// Analitik Hiyerarşi Süreci (Saaty): ikili karşılaştırma matrisinden ağırlıklar (temel özvektör, kuvvet yöntemi),
/// λmax, tutarlılık indeksi CI = (λmax − n)/(n − 1) ve tutarlılık oranı CR = CI / RI.
/// </summary>
public static class Ahp
{
    public const decimal ConsistencyThreshold = 0.10m;
    private const int MaxIterations = 1000;
    private const double Convergence = 1e-12;

    // Saaty rastgele indeks değerleri (n = 1..10)
    private static readonly double[] RandomIndex = [0, 0, 0, 0.58, 0.90, 1.12, 1.24, 1.32, 1.41, 1.45, 1.49];

    public static AhpResult Compute(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        if (n != matrix.GetLength(1) || n < 1 || n >= RandomIndex.Length)
            throw new BusinessRuleException("AHP matrisi kare olmalı (1–10 ölçüt).");

        var w = Enumerable.Repeat(1.0 / n, n).ToArray();
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var next = new double[n];
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                    next[i] += matrix[i, j] * w[j];
            var sum = next.Sum();
            for (var i = 0; i < n; i++)
                next[i] /= sum;
            var delta = next.Zip(w, (a, b) => Math.Abs(a - b)).Max();
            w = next;
            if (delta < Convergence)
                break;
        }

        double lambda = 0;
        for (var i = 0; i < n; i++)
        {
            double row = 0;
            for (var j = 0; j < n; j++)
                row += matrix[i, j] * w[j];
            lambda += row / w[i];
        }
        lambda /= n;

        var ci = n > 2 ? (lambda - n) / (n - 1) : 0;
        var cr = n > 2 ? ci / RandomIndex[n] : 0;
        return new AhpResult(w.Select(x => (decimal)Math.Round(x, 4)).ToList(),
            (decimal)Math.Round(lambda, 4), (decimal)Math.Round(ci, 4), (decimal)Math.Round(cr, 4));
    }

    /// <summary>"1 2 1/3" biçimindeki satırlardan matris kurar (ayarlarda okunaklı saklamak için).</summary>
    public static double[,] Parse(IReadOnlyList<string> rows)
    {
        var parsed = rows.Select(r => r.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ParseValue).ToArray()).ToArray();
        var n = parsed.Length;
        var matrix = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            if (parsed[i].Length != n)
                throw new BusinessRuleException($"AHP matrisinin {i + 1}. satırında {n} değer olmalı.");
            for (var j = 0; j < n; j++)
                matrix[i, j] = parsed[i][j];
        }
        return matrix;
    }

    private static double ParseValue(string text)
    {
        var parts = text.Split('/');
        var numerator = double.Parse(parts[0], CultureInfo.InvariantCulture);
        return parts.Length == 2 ? numerator / double.Parse(parts[1], CultureInfo.InvariantCulture) : numerator;
    }
}
