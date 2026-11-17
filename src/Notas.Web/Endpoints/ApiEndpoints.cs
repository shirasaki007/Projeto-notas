using System.Text.Json;
using Notas.Core.Abstractions;

namespace Notas.Web.Endpoints;

public static class ApiEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1").RequireAuthorization();

        // Download/exibição de anexos. Sempre filtrado pelo dono; 404 também para "não é seu" (sem vazar existência).
        v1.MapGet("/attachments/{id:guid}", async (Guid id, IAttachmentService attachments, HttpContext http, CancellationToken ct) =>
        {
            var r = await attachments.OpenAsync(id, ct);
            if (!r.Succeeded) return Results.NotFound();

            var f = r.Value!;
            http.Response.Headers.CacheControl = "private, max-age=3600";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            // Imagens inline; todo o resto força download (nunca renderiza conteúdo enviado por usuários).
            return f.IsImage ? Results.File(f.Content, f.ContentType) : Results.File(f.Content, f.ContentType, f.FileName);
        });

        // Portabilidade de dados (LGPD): todas as notas do usuário em JSON.
        v1.MapGet("/export", async (INoteService notes, CancellationToken ct) =>
        {
            var all = await notes.ExportAllAsync(ct);
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(all, Json), "application/json", $"notas-{DateTime.UtcNow:yyyyMMdd}.json");
        });

        return app;
    }
}
