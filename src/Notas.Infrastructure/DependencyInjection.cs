using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notas.Infrastructure.Services;
using Notas.Infrastructure.Storage;

namespace Notas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("Defina ConnectionStrings:Default (ex.: variável ConnectionStrings__Default).");

        // Blazor Server: o DbContext NÃO deve viver pelo circuito inteiro -> usamos IDbContextFactory
        // (um contexto curto por operação). O contexto escopado abaixo é usado apenas pelo ASP.NET Identity.
        services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IPreferencesService, PreferencesService>();
        services.AddHostedService<TrashPurgeService>();
        return services;
    }
}
