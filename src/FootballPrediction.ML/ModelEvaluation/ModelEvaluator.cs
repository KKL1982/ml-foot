using Microsoft.ML;
using Microsoft.ML.Data;

namespace FootballPrediction.ML.ModelEvaluation;

public class ModelEvaluator
{
    public static string FormatMetrics(MulticlassClassificationMetrics metrics)
    {
        var lines = new List<string>
        {
            "========== Model Evaluation ==========",
            $"Micro Accuracy:    {metrics.MicroAccuracy:P2}",
            $"Macro Accuracy:    {metrics.MacroAccuracy:P2}",
            $"Log Loss:          {metrics.LogLoss:F4}",
            $"Log Loss Reduction:{metrics.LogLossReduction:F4}",
            "",
            "Per-class metrics:"
        };

        if (metrics.PerClassLogLoss != null)
        {
            for (int i = 0; i < metrics.PerClassLogLoss.Count; i++)
                lines.Add($"  Class {i}: LogLoss = {metrics.PerClassLogLoss[i]:F4}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatBinaryMetrics(CalibratedBinaryClassificationMetrics metrics)
    {
        var lines = new List<string>
        {
            "========== Binary Evaluation ==========",
            $"Accuracy:   {metrics.Accuracy:P2}",
            $"F1 Score:   {metrics.F1Score:F4}",
            $"Precision:  {metrics.PositivePrecision:F4}",
            $"Recall:     {metrics.PositiveRecall:F4}",
            $"Log Loss:   {metrics.LogLoss:F4}",
            $"AUC:        {metrics.AreaUnderRocCurve:F4}",
            $"Entropy:   {metrics.Entropy:F4}"
        };
        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatConfusionMatrix(MulticlassClassificationMetrics metrics)
    {
        if (metrics.ConfusionMatrix?.Counts == null)
            return "Confusion matrix not available.";

        var cm = metrics.ConfusionMatrix;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Confusion Matrix:");
        sb.AppendLine("           Predicted");
        sb.Append("           ");
        for (int j = 0; j < cm.NumberOfClasses; j++)
            sb.Append($"  Class {j} ");
        sb.AppendLine();

        for (int i = 0; i < cm.NumberOfClasses; i++)
        {
            sb.Append($"Actual {i}: ");
            for (int j = 0; j < cm.NumberOfClasses; j++)
                sb.Append($"{cm.Counts[i][j],7} ");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string FormatComparison(List<Training.ModelTrainer.TrainingResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("========== Model Comparison ==========");
        bool isBinary = results.Any(r => r.BinaryMetrics != null);

        if (isBinary)
        {
            sb.AppendLine($"{"Trainer",-28} {"Accuracy",-10} {"LogLoss",-10} {"F1",-8} {"Precision",-10} {"Recall",-10}");
            sb.AppendLine(new string('-', 80));
            foreach (var r in results.OrderByDescending(r => r.F1Score ?? 0))
                sb.AppendLine($"{r.Trainer,-28} {r.Accuracy:P2,-10} {r.LogLoss:F4,-10} {r.F1Score:F4,-8} {r.Precision:F4,-10} {r.Recall:F4,-10}");
        }
        else
        {
            sb.AppendLine($"{"Trainer",-25} {"Accuracy",-12} {"LogLoss",-10}");
            sb.AppendLine(new string('-', 50));
            foreach (var r in results.OrderByDescending(r => r.Accuracy))
                sb.AppendLine($"{r.Trainer,-25} {r.Accuracy:P2,-12} {r.LogLoss:F4,-10}");
        }

        return sb.ToString();
    }
}
