using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.HR;
using ShikshaMitra.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;

namespace ShikshaMitra.Common.Middleware;

public class GlobalExceptionHandlingMiddleware : ITransientDependency
{
	private readonly RequestDelegate _next;
	private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
	private readonly IStringLocalizer<ShikshaMitraResource> _localizer;

	public GlobalExceptionHandlingMiddleware(
		RequestDelegate next,
		ILogger<GlobalExceptionHandlingMiddleware> logger,
		IStringLocalizer<ShikshaMitraResource> localizer)
	{
		_next = next;
		_logger = logger;
		_localizer = localizer;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// Only intercept API requests; let MVC/Razor pages flow through normally
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		var originalResponseStream = context.Response.Body;

		try
		{
			using var responseStream = new MemoryStream();
			context.Response.Body = responseStream;

			await _next(context);

			// Check if the response was an error response
			if (context.Response.StatusCode >= 400)
			{
				// Reset to original stream first
				context.Response.Body = originalResponseStream;

				responseStream.Seek(0, SeekOrigin.Begin);
				var responseBody = await new StreamReader(responseStream).ReadToEndAsync();

				// Always handle error responses with our custom format
				await HandleErrorResponse(context, context.Response.StatusCode, responseBody);
				return;
			}

			// Copy successful response back
			context.Response.Body = originalResponseStream;
			responseStream.Seek(0, SeekOrigin.Begin);
			await responseStream.CopyToAsync(originalResponseStream);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "An unhandled exception occurred");
			context.Response.Body = originalResponseStream;
			await HandleExceptionAsync(context, exception);
		}
	}

	private async Task HandleErrorResponse(HttpContext context, int statusCode, string responseBody)
	{
		// Don't clear if response has already started
		if (!context.Response.HasStarted)
		{
			context.Response.Clear();
		}

		var (errorCode, errorMessage, errorData, validationFields) = GetErrorDetailsFromResponse(responseBody, statusCode);
		var localizedMessage = ResolveErrorMessage(errorCode, errorMessage, errorData, validationFields);

		var error = CreateErrorForStatusCode(statusCode, errorCode, localizedMessage, validationFields);

		var result = Result.Failure(error);
		var jsonResponse = JsonSerializer.Serialize(result, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		context.Response.StatusCode = statusCode;
		context.Response.ContentType = "application/json";
		context.Response.ContentLength = System.Text.Encoding.UTF8.GetByteCount(jsonResponse);

		await context.Response.WriteAsync(jsonResponse);
	}

	private static Error CreateErrorForStatusCode(
		int statusCode,
		string? upstreamCode,
		string message,
		Dictionary<string, string>? validationFields)
	{
		return statusCode switch
		{
			400 when validationFields?.Count > 0 =>
				Error.Create("VALIDATION_ERROR", message, validationFields),
			400 =>
				Error.Create(upstreamCode ?? "INVALID_REQUEST", message),
			401 =>
				Error.Create("UNAUTHORIZED", message),
			403 =>
				Error.Create("FORBIDDEN", message),
			404 =>
				Error.Create("RESOURCE_NOT_FOUND", message),
			409 =>
				Error.Create("DUPLICATE_RESOURCE", message),
			_ =>
				Error.Create("INTERNAL_SERVER_ERROR", message)
		};
	}

	private (string? ErrorCode, string ErrorMessage, Dictionary<string, object>? ErrorData, Dictionary<string, string>? ValidationFields)
		GetErrorDetailsFromResponse(string responseBody, int statusCode)
	{
		try
		{
			if (!string.IsNullOrEmpty(responseBody))
			{
				var jsonDoc = JsonDocument.Parse(responseBody);

				// Check for .NET validation errors format (model state / ProblemDetails)
				if (jsonDoc.RootElement.TryGetProperty("errors", out var errorsElement))
				{
					var fields = new Dictionary<string, string>(StringComparer.Ordinal);
					foreach (var error in errorsElement.EnumerateObject())
					{
						var fieldName = error.Name;
						if (error.Value.ValueKind != JsonValueKind.Array)
						{
							continue;
						}

						var msgs = error.Value.EnumerateArray()
							.Select(e => e.GetString())
							.Where(s => !string.IsNullOrEmpty(s))
							.ToList();
						if (msgs.Count > 0)
						{
							fields[fieldName] = string.Join("; ", msgs);
						}
					}

					if (fields.Count > 0)
					{
						var msg = string.Join("; ", fields.Select(kv => $"{kv.Key}: {kv.Value}"));
						return (null, msg, null, fields);
					}

					if (jsonDoc.RootElement.TryGetProperty("title", out var titleElement))
					{
						var title = titleElement.GetString();
						if (!string.IsNullOrEmpty(title))
						{
							return (null, title, null, null);
						}
					}
				}
				// Check for custom validation payload shape: { error: "...", details: { field: "message" } }
				else if (jsonDoc.RootElement.TryGetProperty("details", out var detailsElement) &&
					detailsElement.ValueKind == JsonValueKind.Object)
				{
					var fields = new Dictionary<string, string>(StringComparer.Ordinal);
					foreach (var field in detailsElement.EnumerateObject())
					{
						if (field.Value.ValueKind == JsonValueKind.String)
						{
							var message = field.Value.GetString();
							if (!string.IsNullOrWhiteSpace(message))
							{
								fields[field.Name] = message;
							}
						}
						else
						{
							var message = field.Value.ToString();
							if (!string.IsNullOrWhiteSpace(message))
							{
								fields[field.Name] = message;
							}
						}
					}

					if (fields.Count > 0)
					{
						var message = string.Join("; ", fields.Select(kv => $"{kv.Key}: {kv.Value}"));
						return (null, message, null, fields);
					}

					if (jsonDoc.RootElement.TryGetProperty("error", out var errorTextElement) &&
						errorTextElement.ValueKind == JsonValueKind.String)
					{
						var errorText = errorTextElement.GetString();
						if (!string.IsNullOrWhiteSpace(errorText))
						{
							return (null, errorText, null, null);
						}
					}
				}

				// Check for ABP error structure
				else if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
				{
					if (errorElement.ValueKind != JsonValueKind.Object)
					{
						var nonObjectErrorText = errorElement.ToString();
						if (!string.IsNullOrWhiteSpace(nonObjectErrorText))
						{
							return (null, nonObjectErrorText, null, null);
						}

						return (null, "An error occurred", null, null);
					}

					string? errorCode = null;
					string? errorMessage = null;
					Dictionary<string, object>? errorData = null;
					Dictionary<string, string>? validationFields = null;

					if (errorElement.TryGetProperty("code", out var codeElement))
					{
						errorCode = codeElement.GetString();
					}

					if (errorElement.TryGetProperty("message", out var messageElement))
					{
						errorMessage = messageElement.GetString();
					}

					if (errorElement.TryGetProperty("data", out var dataElement))
					{
						if (dataElement.ValueKind == JsonValueKind.Object)
						{
							errorData = new Dictionary<string, object>();
							foreach (var property in dataElement.EnumerateObject())
							{
								errorData[property.Name] = property.Value.ToString();
							}
						}
						else if (dataElement.ValueKind != JsonValueKind.Null)
						{
							errorData = new Dictionary<string, object>
							{
								["data"] = dataElement.ToString()
							};
						}
					}

					if (errorElement.TryGetProperty("validationErrors", out var validationErrorsElement) &&
						validationErrorsElement.ValueKind == JsonValueKind.Array)
					{
						validationFields = ParseValidationErrors(validationErrorsElement);
					}

					if (validationFields?.Count > 0)
					{
						var validationMessage = string.Join("; ",
							validationFields.Select(kv => kv.Key == "_" ? kv.Value : $"{kv.Key}: {kv.Value}"));
						return (errorCode, validationMessage, errorData, validationFields);
					}

					if (!string.IsNullOrEmpty(errorCode))
					{
						return (errorCode, errorMessage ?? errorCode, errorData, null);
					}

					if (!string.IsNullOrEmpty(errorMessage))
					{
						return (null, errorMessage, errorData, null);
					}

					if (errorElement.TryGetProperty("details", out var abpDetailsElement))
					{
						var details = abpDetailsElement.GetString();
						if (!string.IsNullOrEmpty(details))
						{
							return (null, details, errorData, null);
						}
					}
				}

				else if (jsonDoc.RootElement.TryGetProperty("title", out var simpleTitleElement))
				{
					var title = simpleTitleElement.GetString();
					if (!string.IsNullOrEmpty(title))
					{
						return (null, title, null, null);
					}
				}
			}
		}
		catch (Exception ex)
		{
			// If response parsing fails, fall back to default messages.
			_logger.LogWarning(ex, "Failed to parse error response body. Falling back to default API error message.");
		}

		var defaultMessage = statusCode switch
		{
			400 => "Validation failed",
			401 => "Authentication required. Please provide valid credentials.",
			403 => "You don't have permission to access this resource.",
			404 => "Resource not found",
			_ => "An error occurred"
		};

		return (null, defaultMessage, null, null);
	}

	private string LocalizeErrorMessage(string messageOrCode, Dictionary<string, object>? errorData)
	{
		try
		{
			// Try to localize the message/code
			var localizedString = _localizer[messageOrCode];

			// If localization is found and different from the key, use it
			if (!localizedString.ResourceNotFound)
			{
				var message = localizedString.Value;

				// Replace placeholders with data from error if available
				if (errorData != null && errorData.Count > 0)
				{
					foreach (var key in errorData.Keys)
					{
						var placeholder = $"{{{key}}}";
						var value = errorData[key]?.ToString() ?? string.Empty;
						message = message.Replace(placeholder, value);
					}
				}

				return message;
			}

			// If localization not found, return the original message
			return messageOrCode;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to localize error message: {Message}", messageOrCode);
			return messageOrCode;
		}
	}

	private string ResolveErrorMessage(
		string? errorCode,
		string errorMessage,
		Dictionary<string, object>? errorData,
		Dictionary<string, string>? validationFields)
	{
		if (validationFields?.Count > 0)
		{
			return errorMessage;
		}

		return !string.IsNullOrWhiteSpace(errorCode)
			? LocalizeErrorMessage(errorCode, errorData)
			: errorMessage;
	}

	private static Dictionary<string, string>? ParseValidationErrors(JsonElement validationErrorsElement)
	{
		var fields = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var validationError in validationErrorsElement.EnumerateArray())
		{
			if (!validationError.TryGetProperty("message", out var messageElement))
			{
				continue;
			}

			var message = messageElement.GetString();
			if (string.IsNullOrWhiteSpace(message))
			{
				continue;
			}

			var members = new List<string>();
			if (validationError.TryGetProperty("members", out var membersElement) &&
				membersElement.ValueKind == JsonValueKind.Array)
			{
				members.AddRange(membersElement.EnumerateArray()
					.Select(m => m.GetString())
					.Where(m => !string.IsNullOrWhiteSpace(m))
					.Select(m => m!));
			}

			if (members.Count == 0)
			{
				AppendValidationMessage(fields, "_", message);
				continue;
			}

			foreach (var member in members)
			{
				AppendValidationMessage(fields, member, message);
			}
		}

		return fields.Count > 0 ? fields : null;
	}

	private static void AppendValidationMessage(IDictionary<string, string> fields, string key, string message)
	{
		if (fields.TryGetValue(key, out var existing))
		{
			fields[key] = $"{existing}; {message}";
			return;
		}

		fields[key] = message;
	}

	private async Task HandleExceptionAsync(HttpContext context, Exception exception)
	{
		var (statusCode, error) = GetErrorDetails(exception);

		context.Response.StatusCode = statusCode;
		context.Response.ContentType = "application/json";

		var result = Result.Failure(error);

		var jsonResponse = JsonSerializer.Serialize(result, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		await context.Response.WriteAsync(jsonResponse);
	}

	private (int StatusCode, Error Error) GetErrorDetails(Exception exception)
	{
		return exception switch
		{
			AbpValidationException validationEx => BuildValidationErrorResponse(validationEx),
			ValidationException validationEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("VALIDATION_ERROR", validationEx.Message)
			),
			AbpAuthorizationException => (
				(int) HttpStatusCode.Forbidden,
				Error.Create("FORBIDDEN", "You don't have permission to access this resource")
			),
			EntityNotFoundException entityNotFoundEx => (
				(int) HttpStatusCode.NotFound,
				Error.Create("RESOURCE_NOT_FOUND", entityNotFoundEx.Message)
			),
			BusinessException businessEx => BuildBusinessExceptionResponse(businessEx),
			ArgumentException argEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_REQUEST", argEx.Message)
			),
			InvalidOperationException invalidOpEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_STATE_TRANSITION", invalidOpEx.Message)
			),
			_ => (
				(int) HttpStatusCode.InternalServerError,
				Error.Create("INTERNAL_SERVER_ERROR", exception.Message ?? "An internal server error occurred")
			)
		};
	}

	private (int StatusCode, Error Error) BuildBusinessExceptionResponse(BusinessException businessEx)
	{
		var localizedMessage = GetLocalizedBusinessExceptionMessage(businessEx);
		var code = businessEx.Code ?? "BUSINESS_ERROR";

		if (code is HRErrorCodes.LeaveAlreadyCancelled or HRErrorCodes.LeaveConflictTerminalState)
		{
			return (
				(int)HttpStatusCode.Conflict,
				Error.Create("DUPLICATE_RESOURCE", localizedMessage));
		}

		if (code == HRErrorCodes.LeaveAttachmentNotFound)
		{
			return (
				(int)HttpStatusCode.NotFound,
				Error.Create("RESOURCE_NOT_FOUND", localizedMessage));
		}

		if (code == HRErrorCodes.LeaveFileTooLarge)
		{
			return (
				(int)HttpStatusCode.RequestEntityTooLarge,
				Error.Create("VALIDATION_ERROR", localizedMessage));
		}

		return (
			(int)HttpStatusCode.BadRequest,
			Error.Create(code, localizedMessage));
	}

	private static (int StatusCode, Error Error) BuildValidationErrorResponse(AbpValidationException validationException)
	{
		var (message, fields) = GetValidationErrorDetails(validationException);
		return (
			(int) HttpStatusCode.BadRequest,
			Error.Create("VALIDATION_ERROR", message, fields)
		);
	}

	private static (string Message, Dictionary<string, string>? Fields) GetValidationErrorDetails(
		AbpValidationException validationException)
	{
		if (validationException.ValidationErrors?.Any() != true)
		{
			return (validationException.Message, null);
		}

		var fields = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var error in validationException.ValidationErrors)
		{
			var member = error.MemberNames?.FirstOrDefault();
			if (string.IsNullOrEmpty(member))
			{
				member = "_";
			}

			if (fields.TryGetValue(member, out var existing))
			{
				fields[member] = $"{existing}; {error.ErrorMessage}";
			}
			else
			{
				fields[member] = error.ErrorMessage ?? string.Empty;
			}
		}

		var message = string.Join("; ", fields.Select(kv => kv.Key == "_" ? kv.Value : $"{kv.Key}: {kv.Value}"));
		return (message, fields);
	}

	private string GetLocalizedBusinessExceptionMessage(BusinessException businessException)
	{
		if (string.IsNullOrEmpty(businessException.Code))
		{
			return businessException.Message ?? "A business error occurred";
		}

		try
		{
			// Try to localize the error code
			var localizedString = _localizer[businessException.Code];

			// If localization is found and different from the key, use it
			if (!localizedString.ResourceNotFound)
			{
				// Replace placeholders with data from exception if available
				var message = localizedString.Value;

				if (businessException.Data != null && businessException.Data.Count > 0)
				{
					foreach (var key in businessException.Data.Keys)
					{
						var placeholder = $"{{{key}}}";
						var value = businessException.Data[key]?.ToString() ?? string.Empty;
						message = message.Replace(placeholder, value);
					}
				}

				return message;
			}

			// If localization not found, return the error code or message
			return !string.IsNullOrEmpty(businessException.Message)
				? businessException.Message
				: businessException.Code;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to localize business exception code: {Code}", businessException.Code);
			return businessException.Message ?? businessException.Code ?? "A business error occurred";
		}
	}
}

