using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Notas.Infrastructure.Services;

/// <summary>Remove definitivamente notas que estão na lixeira há mais de 30 dias.</summary>
public sealed class TrashPurgeService(
    IDbContextFactory<AppDbContext> factory,
    IFileStorage storage,
    ILogger<TrashPurgeService> logger) : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(stoppingToken);
                var cutoff = DateTime.UtcNow - Retention;
                var n = await NotePurger.PurgeAsync(db, storage, db.Notes.Where(x => x.DeletedAt != null && x.DeletedAt < cutoff), stoppingToken);
                if (n > 0) logger.LogInformation("Lixeira: {Count} nota(s) removida(s) definitivamente.", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao purgar a lixeira.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
