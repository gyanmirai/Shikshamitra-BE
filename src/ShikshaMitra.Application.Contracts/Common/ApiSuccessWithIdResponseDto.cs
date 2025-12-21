using System;

namespace ShikshaMitra.Common;

/// <summary>
/// API success response DTO that includes the ID of a created resource.
/// Used for POST operations that need to return the created resource identifier.
/// </summary>
public class ApiSuccessWithIdResponseDto : ApiSuccessResponseDto
{
	/// <summary>
	/// The unique identifier of the created resource
	/// </summary>
	public Guid Id { get; set; }
	
	/// <summary>
	/// Creates a success response with the specified message and resource ID
	/// </summary>
	public static ApiSuccessWithIdResponseDto SuccessResultWithId(string message, Guid id) => new()
	{
		Success = true,
		Message = message,
		Id = id
	};
}
