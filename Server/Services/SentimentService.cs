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
        private readonly Lazy<PredictionEngine<SentimentInput, SentimentOutput>?> _engine;
        private readonly ILogger<SentimentService> _logger;

        // Keyword lists for fallback scoring (used only when ML model fails to build)
        private static readonly string[] PositiveKeywords =
        [
            "excellent", "outstanding", "great", "exceptional", "remarkable", "strong",
            "consistently delivers", "ahead of schedule", "proactive", "initiative",
            "positive attitude", "team player", "collaborative", "dedicated", "committed",
            "reliable", "improvement", "progress", "growing", "leadership", "mentor",
            "innovative", "creative", "motivated", "engaged", "valued", "appreciated",
            "recognised", "recognized", "promoted", "high quality", "exceeded",
            "professional", "punctual", "helpful", "supportive", "successful",
            "accomplished", "achieved", "delivered", "resolved", "improved",
            "well done", "praise", "commended", "award", "compliment", "positive feedback",
            "above expectations", "going above", "beyond expectations"
        ];

        private static readonly string[] NegativeKeywords =
        [
            "late", "absent", "missed", "failed", "poor", "below expectations",
            "complaint", "complaints", "conflict", "hostile", "unprofessional",
            "unreliable", "missed deadline", "multiple absences", "repeatedly",
            "performance issue", "performance gap", "written warning", "disciplinary",
            "refusing", "refused", "unresponsive", "aggressive", "attitude problem",
            "disruptive", "inappropriate", "insubordinate", "terminated", "dismissed",
            "underperforming", "inability", "struggles", "lacks", "insufficient",
            "excessive absences", "warned", "counselled", "counseled", "escalated",
            "friction", "tension", "overdue", "incomplete", "negligent", "careless"
        ];

        public SentimentService(ILogger<SentimentService> logger)
        {
            _logger = logger;
            _engine = new Lazy<PredictionEngine<SentimentInput, SentimentOutput>?>(BuildEngine, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public SentimentResult Predict(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new SentimentResult(true, 0.5f);

            // Try ML model first
            try
            {
                var engine = _engine.Value;
                if (engine is not null)
                {
                    var result = engine.Predict(new SentimentInput { Text = text });
                    return new SentimentResult(result.Prediction, result.Probability);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML sentiment prediction failed for text length {Len}; using keyword fallback.", text.Length);
            }

            // Keyword fallback — used only when ML model is unavailable or throws
            return KeywordFallback(text);
        }

        private SentimentResult KeywordFallback(string text)
        {
            var lower = text.ToLowerInvariant();

            var positiveHits = PositiveKeywords.Count(k => lower.Contains(k));
            var negativeHits = NegativeKeywords.Count(k => lower.Contains(k));

            if (positiveHits == 0 && negativeHits == 0)
                return new SentimentResult(true, 0.5f);  // Neutral

            var score = 0.5f + (positiveHits - negativeHits) * 0.12f;
            score = Math.Clamp(score, 0.05f, 0.95f);
            return new SentimentResult(score >= 0.5f, score);
        }

        private PredictionEngine<SentimentInput, SentimentOutput>? BuildEngine()
        {
            try
            {
                var mlContext = new MLContext(seed: 42);

                var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "sentiment_data.tsv");

                if (!File.Exists(dataPath))
                {
                    _logger.LogWarning("SentimentService: training data not found at {Path}; keyword fallback will be used.", dataPath);
                    return null;
                }

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

                _logger.LogInformation("SentimentService: ML model trained and ready.");

                return mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentOutput>(model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SentimentService: failed to build ML model; keyword fallback will be used.");
                return null;
            }
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
