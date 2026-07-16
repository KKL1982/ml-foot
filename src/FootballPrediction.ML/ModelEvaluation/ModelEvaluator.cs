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
            {
                lines.Add($"  Class {i}: LogLoss = {metrics.PerClassLogLoss[i]:F4}");
            }
        }

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
        sb.AppendLine($"{"Trainer",-25} {"Accuracy",-12} {"LogLoss",-10}");
        sb.AppendLine(new string('-', 50));

        foreach (var r in results.OrderByDescending(r => r.Accuracy))
        {
            sb.AppendLine($"{r.Trainer,-25} {r.Accuracy:P2,-12} {r.LogLoss:F4,-10}");
        }

        return sb.ToString();
    }
}
