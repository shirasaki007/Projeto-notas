using System.Globalization;
using System.Security.Claims;
using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Notas.Core.Abstractions;
using Notas.Infrastructure.Data;

namespace Notas.Web.Services;

/// <summary>
/// Usuário atual. Em requisições HTTP usa o HttpContext; dentro de um circuito Blazor
/// (onde HttpContext não é confiável) usa o AuthenticationStateProvider.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor http, AuthenticationStateProvider auth) : ICurrentUser
{
    public async ValueTask<string> GetUserIdAsync()
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            principal = (await auth.GetAuthenticationStateAsync()).User;

        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}

public enum ToastKind { Info, Success, Error }
public sealed record Toast(Guid Id, string Message, ToastKind Kind, string? ActionLabel, Func<Task>? Action);

public sealed class ToastService
{
    private readonly List<Toast> _items = [];
    public event Action? Changed;
    public IReadOnlyList<Toast> Items => _items;

    public void Show(string message, ToastKind kind = ToastKind.Info, string? actionLabel = null, Func<Task>? action = null)
    {
        var toast = new Toast(Guid.NewGuid(), message, kind, actionLabel, action);
        lock (_items) _items.Add(toast);
        Changed?.Invoke();
        _ = Task.Delay(action is null ? 4000 : 7000).ContinueWith(_ => Dismiss(toast.Id));
    }

    public void Success(string message, string? actionLabel = null, Func<Task>? action = null) => Show(message, ToastKind.Success, actionLabel, action);
    public void Error(string message) => Show(message, ToastKind.Error);

    public void Dismiss(Guid id)
    {
        lock (_items) _items.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
    }
}

/// <summary>Fuso horário do navegador (o servidor só conhece UTC).</summary>
public sealed class ClientClock
{
    private static readonly CultureInfo Pt = new("pt-BR");
    public int OffsetMinutes { get; set; }

    public DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddMinutes(OffsetMinutes);

    public string Relative(DateTime utc)
    {
        var now = DateTime.UtcNow.AddMinutes(OffsetMinutes);
        var t = ToLocal(utc);
        var diff = now - t;
        if (diff.TotalMinutes < 1) return "agora";
        if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes} min";
        if (t.Date == now.Date) return t.ToString("HH:mm", Pt);
        if (t.Date == now.Date.AddDays(-1)) return "ontem";
        return t.ToString(t.Year == now.Year ? "d MMM" : "d MMM yyyy", Pt);
    }

    public string Full(DateTime utc) => ToLocal(utc).ToString("d 'de' MMM 'às' HH:mm", Pt);
}

/// <summary>Markdown -> HTML seguro. Raw HTML é desabilitado E o resultado passa por sanitização.</summary>
public sealed class MarkdownService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().UseSoftlineBreakAsHardlineBreak().DisableHtml().Build();

    private readonly HtmlSanitizer _sanitizer = new();
    private readonly object _lock = new();

    public MarkdownService()
    {
        _sanitizer.AllowedTags.Add("input");            // listas de tarefas
        _sanitizer.AllowedAttributes.Add("type");
        _sanitizer.AllowedAttributes.Add("checked");
        _sanitizer.AllowedAttributes.Add("disabled");
        _sanitizer.AllowedAttributes.Add("class");      // language-xxx em blocos de código
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement el && el.TagName == "A")
            {
                var href = el.GetAttribute("href") ?? "";
                if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    el.SetAttribute("target", "_blank");
                    el.SetAttribute("rel", "noopener noreferrer");
                }
            }
        };
    }

    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var html = Markdown.ToHtml(markdown, _pipeline);
        lock (_lock) return _sanitizer.Sanitize(html);
    }
}

/// <summary>E-mails de conta. Em desenvolvimento, grava o link no log. TROQUE por um provedor real (SMTP/SES/SendGrid).</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment env) : IEmailSender<AppUser>
{
    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) => Log(email, "confirmação", confirmationLink);
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) => Log(email, "redefinição de senha", resetLink);
    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) => Log(email, "código de redefinição", resetCode);

    private Task Log(string email, string kind, string payload)
    {
        if (env.IsDevelopment()) logger.LogWarning("[DEV] E-mail de {Kind} para {Email}: {Payload}", kind, email, payload);
        else logger.LogError("Envio de e-mail ({Kind}) não configurado. Registre uma implementação de IEmailSender<AppUser>.", kind);
        return Task.CompletedTask;
    }
}

public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError E(string code, string text) => new() { Code = code, Description = text };
    public override IdentityError DefaultError() => E(nameof(DefaultError), "Ocorreu um erro inesperado.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Os dados foram alterados por outra operação. Tente novamente.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Senha atual incorreta.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Link inválido ou expirado. Solicite um novo.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "E-mail inválido.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), "Este e-mail já está cadastrado.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "Este e-mail já está cadastrado.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "A senha deve conter ao menos um número.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "A senha deve conter ao menos uma letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "A senha deve conter ao menos uma letra maiúscula.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "A senha deve conter ao menos um símbolo.");
}
