using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

public class ApiProblemDetailsMiddleware : ITransientDependency
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ApiProblemDetailsMiddleware> _logger;

	public ApiProblemDetailsMiddleware(
		RequestDelegate next,
		ILogger<ApiProblemDetailsMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await _next(context);
			return;
		}

		try
		{
			await _next(context);
		}
		catch (Exception exception)
		{
			if (context.Response.HasStarted)
			{
				throw;
			}

			_logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);
			var problem = MapException(context, exception);
			await WriteProblemDetailsAsync(context, problem);
			return;
		}

		if (context.Response.StatusCode < StatusCodes.Status400BadRequest)
		{
			return;
		}

		if (IsProblemDetailsContentType(context.Response.ContentType))
		{
			return;
		}

		// If the response has already started, we cannot safely rewrite the payload.
		if (context.Response.HasStarted)
		{
			return;
		}

		var fallback = CreateProblemDetails(
			context,
			context.Response.StatusCode,
			GetDefaultTitle(context.Response.StatusCode),
			GetDefaultDetail(context.Response.StatusCode),
			errorCode: null,
			errors: null);

		await WriteProblemDetailsAsync(context, fallback);
	}

	private static bool IsProblemDetailsContentType(string? contentType)
	{
		return !string.IsNullOrWhiteSpace(contentType) &&
		       contentType.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
	}

	private static ProblemDetails MapException(HttpContext context, Exception exception)
	{
		return exception switch
		{
			AbpValidationException validationException => BuildValidationProblem(context, validationException),
			ValidationException validationException => CreateProblemDetails(
				context,
				StatusCodes.Status400BadRequest,
				"Validation failed",
				validationException.Message,
				"VALIDATION_ERROR",
				null),
			AbpAuthorizationException authorizationException => CreateProblemDetails(
				context,
				StatusCodes.Status403Forbidden,
				"Forbidden",
				string.IsNullOrWhiteSpace(authorizationException.Message)
					? "You don't have permission to access this resource."
					: authorizationException.Message,
				errorCode: null,
				errors: null),
			EntityNotFoundException entityNotFoundException => CreateProblemDetails(
				context,
				StatusCodes.Status404NotFound,
				"Resource not found",
				entityNotFoundException.Message,
				errorCode: null,
				errors: null),
			BusinessException businessException => BuildBusinessProblem(context, businessException),
			ArgumentException argumentException => CreateProblemDetails(
				context,
				StatusCodes.Status400BadRequest,
				"Invalid request",
				argumentException.Message,
				errorCode: null,
				errors: null),
			InvalidOperationException invalidOperationException => CreateProblemDetails(
				context,
				StatusCodes.Status400BadRequest,
				"Invalid operation",
				invalidOperationException.Message,
				errorCode: null,
				errors: null),
			_ => CreateProblemDetails(
				context,
				StatusCodes.Status500InternalServerError,
				"Internal server error",
				"An unexpected error occurred.",
				errorCode: null,
				errors: null)
		};
	}

	private static ProblemDetails BuildBusinessProblem(HttpContext context, BusinessException exception)
	{
		var (statusCode, title) = exception.Code switch
		{
			HRErrorCodes.LeaveEmployeeNotFound => (StatusCodes.Status403Forbidden, "Forbidden"),
			HRErrorCodes.LeaveAlreadyCancelled or HRErrorCodes.LeaveConflictTerminalState => (StatusCodes.Status409Conflict, "Conflict"),
			HRErrorCodes.LeaveAttachmentNotFound => (StatusCodes.Status404NotFound, "Resource not found"),
			HRErrorCodes.LeaveFileTooLarge => (StatusCodes.Status413PayloadTooLarge, "Payload too large"),
			_ => (StatusCodes.Status400BadRequest, "Business rule violated")
		};

		var defaultDetail = GetDefaultDetail(statusCode);
		var normalizedDetail = ApiProblemDetailsErrorMessages.NormalizeBusinessDetail(
			exception.Message,
			exception.Code,
			defaultDetail);

		return CreateProblemDetails(
			context,
			statusCode,
			title,
			normalizedDetail,
			exception.Code,
			null);
	}

	private static ProblemDetails BuildValidationProblem(HttpContext context, AbpValidationException exception)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		foreach (var validationError in exception.ValidationErrors ?? Enumerable.Empty<ValidationResult>())
		{
			var memberNames = validationError.MemberNames?.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() ?? [];
			if (memberNames.Length == 0)
			{
				AppendValidationError(errors, "_", validationError.ErrorMessage ?? "The input was not valid.");
				continue;
			}

			foreach (var memberName in memberNames)
			{
				AppendValidationError(errors, memberName, validationError.ErrorMessage ?? "The input was not valid.");
			}
		}

		return CreateProblemDetails(
			context,
			StatusCodes.Status400BadRequest,
			"Validation failed",
			exception.Message,
			"VALIDATION_ERROR",
			errors.Count == 0 ? null : errors);
	}

	private static void AppendValidationError(
		IDictionary<string, string[]> errors,
		string memberName,
		string message)
	{
		if (errors.TryGetValue(memberName, out var existing))
		{
			errors[memberName] = [.. existing, message];
			return;
		}

		errors[memberName] = [message];
	}

	private static ProblemDetails CreateProblemDetails(
		HttpContext context,
		int statusCode,
		string title,
		string? detail,
		string? errorCode,
		Dictionary<string, string[]>? errors)
	{
		var problem = new ProblemDetails
		{
			Type = $"https://httpstatuses.com/{statusCode}",
			Title = title,
			Status = statusCode,
			Detail = string.IsNullOrWhiteSpace(detail) ? title : detail,
			Instance = context.Request.Path
		};

		if (!string.IsNullOrWhiteSpace(errorCode))
		{
			problem.Extensions["errorCode"] = errorCode;
		}

		if (errors is { Count: > 0 })
		{
			problem.Extensions["errors"] = errors;
		}

		return problem;
	}

	private static string GetDefaultTitle(int statusCode)
	{
		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "Validation failed",
			StatusCodes.Status401Unauthorized => "Unauthorized",
			StatusCodes.Status403Forbidden => "Forbidden",
			StatusCodes.Status404NotFound => "Resource not found",
			StatusCodes.Status409Conflict => "Conflict",
			StatusCodes.Status413PayloadTooLarge => "Payload too large",
			_ => "Request failed"
		};
	}

	private static string GetDefaultDetail(int statusCode)
	{
		return statusCode switch
		{
			StatusCodes.Status400BadRequest => "One or more validation errors occurred.",
			StatusCodes.Status401Unauthorized => "Authentication is required to access this resource.",
			StatusCodes.Status403Forbidden => "You don't have permission to access this resource.",
			StatusCodes.Status404NotFound => "The requested resource was not found.",
			StatusCodes.Status409Conflict => "The request could not be completed because of a conflict.",
			StatusCodes.Status413PayloadTooLarge => "The request payload is too large.",
			_ => "The request failed."
		};
	}

	private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem)
	{
		context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
		context.Response.ContentType = "application/problem+json";
		context.Response.ContentLength = null;
		await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
	}
}
