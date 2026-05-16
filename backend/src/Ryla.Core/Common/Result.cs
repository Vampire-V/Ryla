namespace Ryla.Core.Common;

/// <summary>
/// Error value สำหรับ Result pattern — แทน exception ใน expected error paths
/// </summary>
public sealed record Error(string Code, string Message);

/// <summary>
/// Result pattern — ไม่ใช้ raw exception สำหรับ control flow
/// </summary>
public sealed record Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(Error error) => new(default, error);
}
