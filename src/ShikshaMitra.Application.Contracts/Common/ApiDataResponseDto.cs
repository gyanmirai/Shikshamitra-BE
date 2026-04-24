namespace ShikshaMitra.Common;

/// <summary>
/// Generic API response envelope that carries a strongly-typed <see cref="Data"/> payload.
/// Matches the <c>{ "success": true, "data": { ... } }</c> shape defined in the OpenAPI spec.
/// </summary>
/// <typeparam name="T">Type of the payload object.</typeparam>
public class ApiDataResponseDto<T>
{
    /// <summary>Indicates whether the operation completed successfully.</summary>
    public bool Success { get; init; } = true;

    /// <summary>The response payload.</summary>
    public T Data { get; init; }

    public ApiDataResponseDto(T data)
    {
        Data = data;
    }

    /// <summary>Factory method for successful responses.</summary>
    public static ApiDataResponseDto<T> SuccessResult(T data) => new(data);
}
