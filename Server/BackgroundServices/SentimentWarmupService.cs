using Server.Services;

namespace Server.BackgroundServices
{
    /// <summary>
    /// Hosted service that pre-warms the lazy-loaded ML.NET sentiment model
    /// at application startup so the first HR note submission is instant
    /// instead of paying the ~2-4 s model training cost on-demand.
    /// </summary>
    public class SentimentWarmupService(
        ISentimentService sentiment,
        ILogger<SentimentWarmupService> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Fire-and-forget: don't block app startup, but start immediately.
            // Lazy<T> inside SentimentService ensures thread-safe single initialisation.
            _ = Task.Run(() =>
            {
                try
                {
                    logger.LogInformation("SentimentWarmupService: pre-warming ML.NET model...");
                    sentiment.Predict("warmup text for model initialisation");
                    logger.LogInformation("SentimentWarmupService: model ready.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "SentimentWarmupService: model warmup failed; keyword fallback will be used.");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
