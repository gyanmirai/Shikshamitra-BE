using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR;

namespace ShikshaMitra.Common.Middleware;

public static class ApiProblemDetailsMapper
{
	private const string ValidationErrorCode = "VALIDATION_ERROR";

	public static int ResolveBusinessStatusCode(string? errorCode)
	{
		if (string.IsNullOrWhiteSpace(errorCode))
		{
			return StatusCodes.Status400BadRequest;
		}

		return errorCode switch
		{
			HRErrorCodes.LeaveEmployeeNotFound => StatusCodes.Status403Forbidden,
			HRErrorCodes.TakeAttendanceMediumStageOutOfScope => StatusCodes.Status403Forbidden,
			HRErrorCodes.LeaveInsufficientBalance => StatusCodes.Status400BadRequest,
			HRErrorCodes.LeaveAlreadyCancelled => StatusCodes.Status409Conflict,
			HRErrorCodes.LeaveConflictTerminalState => StatusCodes.Status409Conflict,
			HRErrorCodes.LeaveAttachmentNotFound => StatusCodes.Status404NotFound,
			HRErrorCodes.LeaveFileTooLarge => StatusCodes.Status413PayloadTooLarge,
			ValidationErrorCode => StatusCodes.Status400BadRequest,
			_ => StatusCodes.Status400BadRequest
		};
	}

	public static int NormalizeStatusCode(int currentStatus, string? errorCode)
	{
		if (string.IsNullOrWhiteSpace(errorCode))
		{
			return currentStatus;
		}

		var mappedStatus = ResolveBusinessStatusCode(errorCode);

		// For business-coded payloads, canonical mapping should always win to avoid drift
		// between exception pipeline and result-filter normalization.
		return mappedStatus;
	}

	public static string GetDefaultTitle(int statusCode, bool hasValidationErrors = false)
	{
		if (statusCode == StatusCodes.Status400BadRequest && hasValidationErrors)
		{
			return "Validation failed";
		}

		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "Business rule violated",
			StatusCodes.Status401Unauthorized => "Unauthorized",
			StatusCodes.Status403Forbidden => "Forbidden",
			StatusCodes.Status404NotFound => "Resource not found",
			StatusCodes.Status409Conflict => "Conflict",
			StatusCodes.Status413PayloadTooLarge => "Payload too large",
			_ => "Request failed"
		};
	}

	public static string GetDefaultDetail(int statusCode, bool hasValidationErrors = false)
	{
		if (statusCode == StatusCodes.Status400BadRequest && hasValidationErrors)
		{
			return "One or more validation errors occurred.";
		}

		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "One or more business rules were violated.",
			StatusCodes.Status401Unauthorized => "Authentication is required to access this resource.",
			StatusCodes.Status403Forbidden => "You don't have permission to access this resource.",
			StatusCodes.Status404NotFound => "The requested resource was not found.",
			StatusCodes.Status409Conflict => "The request could not be completed because of a conflict.",
			StatusCodes.Status413PayloadTooLarge => "The request payload is too large.",
			_ => "The request failed."
		};
	}
}
