using Microsoft.ML;
using Microsoft.ML.Data;

namespace Server.Services
{
    public interface ISentimentService
    {
        SentimentResult Predict(string text);
    }

    public record SentimentResult(bool IsPositive, float Score);

    public sealed class SentimentService : ISentimentService
    {
        private readonly Lazy<PredictionEngine<SentimentInput, SentimentOutput>> _engine;
        private readonly ILogger<SentimentService> _logger;

        public SentimentService(ILogger<SentimentService> logger)
        {
            _logger = logger;
            _engine = new Lazy<PredictionEngine<SentimentInput, SentimentOutput>>(BuildEngine, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public SentimentResult Predict(string text)
        {
            try
            {
                var result = _engine.Value.Predict(new SentimentInput { Text = text });
                return new SentimentResult(result.Prediction, result.Probability);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sentiment prediction failed for text length {Len}; defaulting to neutral.", text?.Length ?? 0);
                return new SentimentResult(true, 0.5f);
            }
        }

        private PredictionEngine<SentimentInput, SentimentOutput> BuildEngine()
        {
            var mlContext = new MLContext(seed: 42);

            var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "sentiment_data.tsv");

            var dataView = mlContext.Data.LoadFromTextFile<SentimentInput>(
                path: dataPath,
                hasHeader: true,
                separatorChar: '\t');

            var pipeline = mlContext.Transforms.Text
                .FeaturizeText("Features", nameof(SentimentInput.Text))
                .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));

            var model = pipeline.Fit(dataView);

            _logger.LogInformation("SentimentService: model trained and ready.");

            return mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentOutput>(model);
        }
    }

    // ── ML.NET data classes ──────────────────────────────────────────────────

    internal sealed class SentimentInput
    {
        [LoadColumn(0)]
        public bool Label { get; set; }

        [LoadColumn(1)]
        public string Text { get; set; } = string.Empty;
    }

    internal sealed class SentimentOutput
    {
        [ColumnName("PredictedLabel")]
        public bool Prediction { get; set; }

        public float Probability { get; set; }
        public float Score       { get; set; }
    }
}
