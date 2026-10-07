namespace ProjectMind.Application.Ml;

public sealed record BinaryMetrics(int Count, double Accuracy, double Precision, double Recall, double F1, double Auc);

/// <summary>İkili sınıflandırma ölçütleri (ML modelleri ve EVM kuralı aynı fonksiyonla değerlendirilir).</summary>
public static class ClassificationMetrics
{
    public static BinaryMetrics Compute(IReadOnlyList<bool> actual, IReadOnlyList<double> scores, double threshold = 0.5)
    {
        int tp = 0, fp = 0, tn = 0, fn = 0;
        for (var i = 0; i < actual.Count; i++)
        {
            var predicted = scores[i] >= threshold;
            if (predicted && actual[i]) tp++;
            else if (predicted) fp++;
            else if (actual[i]) fn++;
            else tn++;
        }

        var precision = tp + fp == 0 ? 0 : (double)tp / (tp + fp);
        var recall = tp + fn == 0 ? 0 : (double)tp / (tp + fn);
        var f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
        var accuracy = actual.Count == 0 ? 0 : (double)(tp + tn) / actual.Count;
        return new BinaryMetrics(actual.Count, accuracy, precision, recall, f1, Auc(actual, scores));
    }

    /// <summary>ROC-AUC = rastgele bir pozitifin skoru rastgele bir negatiften yüksek olma olasılığı (eşitlik = 0,5).</summary>
    public static double Auc(IReadOnlyList<bool> actual, IReadOnlyList<double> scores)
    {
        var positives = Enumerable.Range(0, actual.Count).Where(i => actual[i]).Select(i => scores[i]).ToList();
        var negatives = Enumerable.Range(0, actual.Count).Where(i => !actual[i]).Select(i => scores[i]).ToList();
        if (positives.Count == 0 || negatives.Count == 0)
            return double.NaN;

        double wins = 0;
        foreach (var p in positives)
            foreach (var n in negatives)
                wins += p > n ? 1 : p == n ? 0.5 : 0;
        return wins / (positives.Count * (double)negatives.Count);
    }

    public static double MeanAbsoluteError(IReadOnlyList<double> actual, IReadOnlyList<double> predicted) =>
        actual.Count == 0 ? double.NaN : actual.Zip(predicted, (a, p) => Math.Abs(a - p)).Average();
}
