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

		// Localize the error message if it looks like a localization key
		var localizedMessage = LocalizeErrorMessage(errorCode ?? errorMessage, errorData);

		var error = statusCode switch
		{
			400 => Error.Create("VALIDATION_ERROR", localizedMessage, validationFields),
			401 => Error.Create("UNAUTHORIZED", localizedMessage ?? "Authentication required. Please provide valid credentials."),
			403 => Error.Create("FORBIDDEN", localizedMessage ?? "You don't have permission to access this resource."),
			404 => Error.Create("NOT_FOUND", localizedMessage),
			_ => Error.Create("ERROR", localizedMessage)
		};

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

				// Check for ABP error structure
				else if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
				{
					string? errorCode = null;
					string? errorMessage = null;
					Dictionary<string, object>? errorData = null;

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
						errorData = new Dictionary<string, object>();
						foreach (var property in dataElement.EnumerateObject())
						{
							errorData[property.Name] = property.Value.ToString();
						}
					}

					if (!string.IsNullOrEmpty(errorCode))
					{
						return (errorCode, errorMessage ?? errorCode, errorData, null);
					}

					if (!string.IsNullOrEmpty(errorMessage))
					{
						return (null, errorMessage, errorData, null);
					}

					if (errorElement.TryGetProperty("details", out var detailsElement))
					{
						var details = detailsElement.GetString();
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
		catch (JsonException)
		{
			// If JSON parsing fails, fall back to default messages
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
				Error.Create("AUTHORIZATION_ERROR", "You don't have permission to access this resource")
			),
			EntityNotFoundException entityNotFoundEx => (
				(int) HttpStatusCode.NotFound,
				Error.Create("NOT_FOUND", entityNotFoundEx.Message)
			),
			BusinessException businessEx => BuildBusinessExceptionResponse(businessEx),
			ArgumentException argEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_ARGUMENT", argEx.Message)
			),
			InvalidOperationException invalidOpEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_OPERATION", invalidOpEx.Message)
			),
			_ => (
				(int) HttpStatusCode.InternalServerError,
				Error.Create("INTERNAL_ERROR", exception.Message ?? "An internal server error occurred")
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
				Error.Create("NOT_FOUND", localizedMessage));
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
