namespace Notas.Core.Common;

public enum ErrorKind { NotFound, Validation, Conflict, Forbidden }

public sealed record ServiceError(ErrorKind Kind, string Message);

/// <summary>Resultado explícito de operações de aplicação (sem exceções para fluxo de negócio).</summary>
public sealed record Result(ServiceError? Error = null)
{
    public bool Succeeded => Error is null;
    public static Result Ok() => new();
    public static Result Fail(ErrorKind kind, string message) => new(new ServiceError(kind, message));
    public static Result NotFound(string message = "Item não encontrado.") => Fail(ErrorKind.NotFound, message);
}

public sealed record Result<T>(T? Value, ServiceError? Error = null)
{
    public bool Succeeded => Error is null;
    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(ErrorKind kind, string message) => new(default, new ServiceError(kind, message));
    public static Result<T> NotFound(string message = "Item não encontrado.") => Fail(ErrorKind.NotFound, message);
}
