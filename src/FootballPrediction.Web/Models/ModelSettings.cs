namespace FootballPrediction.Web.Models;

public class ModelSettings
{
    public const string SectionName = "ModelSettings";
    public string ModelDirectory { get; set; } = "models";
    public string BinaryModelPath { get; set; } = "models/model_binary.zip";
    public string MulticlassModelPath { get; set; } = "models/model.zip";
    public string DefaultMode { get; set; } = "Binary";
}
