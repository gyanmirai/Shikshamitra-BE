using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Responses;
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

	public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
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

		var errorMessage = GetErrorMessageFromResponse(responseBody, statusCode);

		var error = statusCode switch
		{
			400 => Error.Create("VALIDATION_ERROR", errorMessage),
			401 => Error.Create("UNAUTHORIZED", errorMessage),
			403 => Error.Create("FORBIDDEN", errorMessage),
			404 => Error.Create("NOT_FOUND", errorMessage),
			_ => Error.Create("ERROR", errorMessage)
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

	private static string GetErrorMessageFromResponse(string responseBody, int statusCode)
	{
		try
		{
			if (!string.IsNullOrEmpty(responseBody))
			{
				var jsonDoc = JsonDocument.Parse(responseBody);

				// Check for .NET validation errors format
				if (jsonDoc.RootElement.TryGetProperty("errors", out var errorsElement))
				{
					var validationErrors = new List<string>();

					foreach (var error in errorsElement.EnumerateObject())
					{
						var fieldName = error.Name;
						if (error.Value.ValueKind == JsonValueKind.Array)
						{
							foreach (var errorMessage in error.Value.EnumerateArray())
							{
								validationErrors.Add($"{fieldName}: {errorMessage.GetString()}");
							}
						}
					}

					if (validationErrors.Any())
					{
						return string.Join("; ", validationErrors);
					}

					// If we have a title, use that
					if (jsonDoc.RootElement.TryGetProperty("title", out var titleElement))
					{
						var title = titleElement.GetString();
						if (!string.IsNullOrEmpty(title))
						{
							return title;
						}
					}
				}

				// Check for ABP error structure
				else if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
				{
					if (errorElement.TryGetProperty("message", out var messageElement))
					{
						var message = messageElement.GetString();
						if (!string.IsNullOrEmpty(message))
						{
							return message;
						}
					}

					// If message is empty, try details
					if (errorElement.TryGetProperty("details", out var detailsElement))
					{
						var details = detailsElement.GetString();
						if (!string.IsNullOrEmpty(details))
						{
							return details;
						}
					}
				}

				// Check for simple title property (for .NET error responses)
				else if (jsonDoc.RootElement.TryGetProperty("title", out var simpleTitleElement))
				{
					var title = simpleTitleElement.GetString();
					if (!string.IsNullOrEmpty(title))
					{
						return title;
					}
				}
			}
		}
		catch (JsonException)
		{
			// If JSON parsing fails, fall back to default messages
		}

		// Fallback to default messages based on status code
		return statusCode switch
		{
			400 => "Validation failed",
			401 => "Unauthorized access",
			403 => "Access forbidden",
			404 => "Resource not found",
			_ => "An error occurred"
		};
	}

	private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
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

	private static (int StatusCode, Error Error) GetErrorDetails(Exception exception)
	{
		return exception switch
		{
			AbpValidationException validationEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("VALIDATION_ERROR", GetValidationErrorMessage(validationEx))
			),
			ValidationException validationEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("VALIDATION_ERROR", validationEx.Message)
			),
			AbpAuthorizationException => (
				(int) HttpStatusCode.Forbidden,
				Error.Create("AUTHORIZATION_ERROR", "You don't have permission to access this resource")
			),
			EntityNotFoundException => (
				(int) HttpStatusCode.NotFound,
				Error.Create("NOT_FOUND", "The requested resource was not found")
			),
			BusinessException businessEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create(businessEx.Code ?? "BUSINESS_ERROR", businessEx.Message)
			),
			ArgumentException argEx => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_ARGUMENT", argEx.Message)
			),
			InvalidOperationException => (
				(int) HttpStatusCode.BadRequest,
				Error.Create("INVALID_OPERATION", "The requested operation is not valid")
			),
			_ => (
				(int) HttpStatusCode.InternalServerError,
				Error.Create("INTERNAL_ERROR", "An internal server error occurred")
			)
		};
	}

	private static string GetValidationErrorMessage(AbpValidationException validationException)
	{
		if (validationException.ValidationErrors?.Any() == true)
		{
			var errors = validationException.ValidationErrors
				.Select(error => $"{error.MemberNames.FirstOrDefault()}: {error.ErrorMessage}")
				.ToList();

			return string.Join("; ", errors);
		}

		return validationException.Message;
	}
}
