namespace Notas.Infrastructure.Services;

public sealed class PreferencesService(
    IDbContextFactory<AppDbContext> factory,
    ICurrentUser currentUser) : IPreferencesService
{
    public async Task<UserPreferences> GetCurrentAsync(CancellationToken ct = default)
    {
        var userId = await currentUser.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);

        var preferences = await db.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        return preferences is null
            ? UserPreferences.Defaults.Create(userId)
            : UserPreferences.Normalize(preferences, userId);
    }

    public async Task<UserPreferences> SaveAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var userId = await currentUser.GetUserIdAsync();
        var normalized = UserPreferences.Normalize(preferences, userId);
        normalized.UpdatedAt = DateTime.UtcNow;

        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.UserPreferences.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (current is null)
        {
            db.UserPreferences.Add(normalized);
        }
        else
        {
            current.EditorFont = normalized.EditorFont;
            current.FontSize = normalized.FontSize;
            current.LineHeight = normalized.LineHeight;
            current.ColumnWidth = normalized.ColumnWidth;
            current.Accent = normalized.Accent;
            current.UpdatedAt = normalized.UpdatedAt;
        }

        await db.SaveChangesAsync(ct);
        return normalized;
    }

    public Task<UserPreferences> RestoreDefaultsAsync(CancellationToken ct = default) =>
        SaveAsync(UserPreferences.Defaults.Create(), ct);
}
