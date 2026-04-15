namespace ManufacturingERP.Shared.Results;

public class Result
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;

    public static Result Success(string message = "Success") => new() { IsSuccess = true, Message = message };
    public static Result Failure(string message) => new() { IsSuccess = false, Message = message };
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Success(T value, string message = "Success") => new() { IsSuccess = true, Message = message, Value = value };
    public new static Result<T> Failure(string message) => new() { IsSuccess = false, Message = message };
}
