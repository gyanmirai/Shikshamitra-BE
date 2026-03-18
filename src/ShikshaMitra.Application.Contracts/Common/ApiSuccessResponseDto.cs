namespace ShikshaMitra.Common;

/// <summary>
/// Common API success response DTO matching the OpenAPI BasicSuccess schema.
/// Used for POST/PUT/DELETE operations that return a simple success message.
/// </summary>
public class ApiSuccessResponseDto
{
	/// <summary>
	/// Indicates whether the operation was successful
	/// </summary>
	public bool Success { get; set; } = true;

	/// <summary>
	/// A human-readable message describing the result
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Creates a success response with the specified message
	/// </summary>
	public static ApiSuccessResponseDto SuccessResult(string message) => new()
	{
		Success = true,
		Message = message
	};

	/// <summary>
	/// Creates a failure response with the specified message
	/// </summary>
	public static ApiSuccessResponseDto FailureResult(string message) => new()
	{
		Success = false,
		Message = message
	};
}

