using Microsoft.EntityFrameworkCore.Design;

namespace Notas.Infrastructure.Data;

/// <summary>Usada apenas pelo `dotnet ef` (migrations). Em runtime a conexão vem da configuração.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=localhost;Port=5432;Database=notas;Username=notas;Password=notas_dev_pw";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options;
        return new AppDbContext(options);
    }
}
