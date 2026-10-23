namespace QuickPrompt.ApplicationLayer.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Replaces exception-based error handling with explicit result types.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public class Result<T>
{
    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The value returned by the operation (only valid when IsSuccess is true).
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// The error message if the operation failed (only valid when IsFailure is true).
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Optional error code for programmatic error handling.
    /// </summary>
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value, string error, string? errorCode = null)
    {
        if (isSuccess && value == null)
            throw new ArgumentException("Success result must have a value", nameof(value));

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Failure result must have an error message", nameof(error));

        IsSuccess = isSuccess;
        Value = value;
        Error = error ?? string.Empty;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a successful result with the given value.
    /// </summary>
    public static Result<T> Success(T value) =>
        new Result<T>(true, value, string.Empty);

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static Result<T> Failure(string error, string? errorCode = null) =>
        new Result<T>(false, default, error, errorCode);

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result<T> Failure(Exception exception) =>
        new Result<T>(false, default, exception.Message, exception.GetType().Name);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess && Value != null)
            action(Value);

        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);

        return this;
    }

    /// <summary>
    /// Maps the result to a new type if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error, ErrorCode);

        try
        {
            var mappedValue = mapper(Value!);
            return Result<TNew>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(ex);
        }
    }

    /// <summary>
    /// Chains another operation if the current result is successful.
    /// </summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> func)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error, ErrorCode);

        try
        {
            return await func(Value!);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(ex);
        }
    }
}

/// <summary>
/// Represents the result of an operation that doesn't return a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string error, string? errorCode = null)
    {
        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Failure result must have an error message", nameof(error));

        IsSuccess = isSuccess;
        Error = error ?? string.Empty;
        ErrorCode = errorCode;
    }

    public static Result Success() =>
        new Result(true, string.Empty);

    public static Result Failure(string error, string? errorCode = null) =>
        new Result(false, error, errorCode);

    public static Result Failure(Exception exception) =>
        new Result(false, exception.Message, exception.GetType().Name);

    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();

        return this;
    }

    public Result OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);

        return this;
    }

    /// <summary>
    /// Converts to a generic Result with a value.
    /// </summary>
    public Result<T> ToResult<T>(T value)
    {
        if (IsFailure)
            return Result<T>.Failure(Error, ErrorCode);

        return Result<T>.Success(value);
    }
}
