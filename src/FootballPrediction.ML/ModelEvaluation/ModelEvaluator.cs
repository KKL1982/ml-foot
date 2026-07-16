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
            "Per-class metrics: available via metrics.ConfusionMatrix"
        };

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatConfusionMatrix(MulticlassClassificationMetrics metrics)
    {
        if (metrics.ConfusionMatrix?.Counts == null)
            return "Confusion matrix not available.";

        var cm = metrics.ConfusionMatrix.Counts;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Confusion Matrix:");
        sb.AppendLine("Predicted \\ Actual");
        for (int i = 0; i < cm.Count; i++)
        {
            sb.Append($"  row {i}: ");
            for (int j = 0; j < cm[i].Count; j++)
                sb.Append($"{cm[i][j],6} ");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
