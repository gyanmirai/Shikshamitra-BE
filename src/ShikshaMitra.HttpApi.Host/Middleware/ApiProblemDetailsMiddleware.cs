using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
	private readonly IApiProblemDetailsErrorDetailResolver _errorDetailResolver;

	public ApiProblemDetailsMiddleware(
		RequestDelegate next,
		ILogger<ApiProblemDetailsMiddleware> logger,
		IApiProblemDetailsErrorDetailResolver errorDetailResolver)
	{
		_next = next;
		_logger = logger;
		_errorDetailResolver = errorDetailResolver;
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

			_logger.LogError(exception, "Unhandled API exception {ErrorReference} for {Path}", context.TraceIdentifier, context.Request.Path);
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
			ApiProblemDetailsMapper.GetDefaultTitle(context.Response.StatusCode),
			ApiProblemDetailsMapper.GetDefaultDetail(context.Response.StatusCode),
			errorCode: null,
			errors: null);

		await WriteProblemDetailsAsync(context, fallback);
	}

	private static bool IsProblemDetailsContentType(string? contentType)
	{
		return !string.IsNullOrWhiteSpace(contentType) &&
		       contentType.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
	}

	private ProblemDetails MapException(HttpContext context, Exception exception)
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
					? ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status403Forbidden)
					: authorizationException.Message,
				errorCode: authorizationException.Code,
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

	private ProblemDetails BuildBusinessProblem(HttpContext context, BusinessException exception)
	{
		var statusCode = ApiProblemDetailsMapper.ResolveBusinessStatusCode(exception.Code);
		var title = ApiProblemDetailsMapper.GetDefaultTitle(statusCode);
		var defaultDetail = ApiProblemDetailsMapper.GetDefaultDetail(statusCode);
		var normalizedDetail = _errorDetailResolver.ResolveDetail(
			exception.Message,
			exception.Code,
			defaultDetail,
			ToDictionary(exception.Data));

		return CreateProblemDetails(
			context,
			statusCode,
			title,
			normalizedDetail,
			exception.Code,
			null,
			ToDictionary(exception.Data));
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
		Dictionary<string, string[]>? errors,
		Dictionary<string, object?>? errorData = null)
	{
		var problem = new ProblemDetails
		{
			Type = $"https://httpstatuses.com/{statusCode}",
			Title = title,
			Status = statusCode,
			Detail = string.IsNullOrWhiteSpace(detail) ? title : detail,
			Instance = context.Request.Path
		};
		problem.Extensions["traceId"] = context.TraceIdentifier;

		if (!string.IsNullOrWhiteSpace(errorCode))
		{
			problem.Extensions["errorCode"] = errorCode;
		}

		if (errors is { Count: > 0 })
		{
			problem.Extensions["errors"] = errors;
		}

		if (errorData is { Count: > 0 })
		{
			problem.Extensions["data"] = errorData;
		}

		return problem;
	}

	private static Dictionary<string, object?>? ToDictionary(System.Collections.IDictionary? source)
	{
		if (source == null || source.Count == 0)
		{
			return null;
		}

		var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
		foreach (System.Collections.DictionaryEntry entry in source)
		{
			var key = entry.Key?.ToString();
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			result[key] = entry.Value;
		}

		return result.Count == 0 ? null : result;
	}

	private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem)
	{
		ApiPermissionDeniedDetails.Apply(context, problem);
		context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
		context.Response.ContentType = "application/problem+json";
		context.Response.ContentLength = null;
		await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
	}
}
