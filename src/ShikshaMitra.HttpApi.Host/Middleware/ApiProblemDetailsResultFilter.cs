using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.Middleware;

public class ApiProblemDetailsResultFilter : IAsyncAlwaysRunResultFilter, ITransientDependency
{
	public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
	{
		if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
		{
			await next();
			return;
		}

		var status = GetStatusCode(context);
		if (status < StatusCodes.Status400BadRequest)
		{
			await next();
			return;
		}

		var payload = ExtractPayload(context.Result);
		var problem = BuildProblemDetails(context.HttpContext.Request.Path, status, payload);
		context.Result = new ObjectResult(problem)
		{
			StatusCode = status,
			DeclaredType = typeof(ProblemDetails),
			ContentTypes = { "application/problem+json" }
		};

		await next();
	}

	private static int GetStatusCode(ResultExecutingContext context)
	{
		return context.Result switch
		{
			ForbidResult => StatusCodes.Status403Forbidden,
			ObjectResult objectResult => objectResult.StatusCode ?? context.HttpContext.Response.StatusCode,
			JsonResult jsonResult => jsonResult.StatusCode ?? context.HttpContext.Response.StatusCode,
			ContentResult contentResult => contentResult.StatusCode ?? context.HttpContext.Response.StatusCode,
			StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
			IStatusCodeActionResult statusCodeActionResult when statusCodeActionResult.StatusCode.HasValue =>
				statusCodeActionResult.StatusCode.Value,
			_ => context.HttpContext.Response.StatusCode
		};
	}

	private static object? ExtractPayload(IActionResult result)
	{
		return result switch
		{
			ObjectResult objectResult => objectResult.Value,
			JsonResult jsonResult => jsonResult.Value,
			ContentResult contentResult => ParseContentPayload(contentResult.Content),
			_ => null
		};
	}

	private static object? ParseContentPayload(string? content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<object>(content);
		}
		catch
		{
			return content;
		}
	}

	private static ProblemDetails BuildProblemDetails(PathString instance, int status, object? value)
	{
		if (value is ValidationProblemDetails validationProblemDetails)
		{
			validationProblemDetails.Type = $"https://httpstatuses.com/{status}";
			validationProblemDetails.Status = status;
			validationProblemDetails.Title ??= "Validation failed";
			validationProblemDetails.Instance ??= instance;
			validationProblemDetails.Extensions["errorCode"] = "VALIDATION_ERROR";

			if (!validationProblemDetails.Extensions.ContainsKey("errors"))
			{
				validationProblemDetails.Extensions["errors"] = validationProblemDetails.Errors;
			}

			return validationProblemDetails;
		}

		if (value is ProblemDetails problemDetails)
		{
			problemDetails.Type = $"https://httpstatuses.com/{status}";
			problemDetails.Status = status;
			problemDetails.Title ??= status == StatusCodes.Status400BadRequest ? "Validation failed" : "Request failed";
			problemDetails.Instance ??= instance;
			return problemDetails;
		}

		var (title, detail, errorCode, errors) = ParseErrorPayload(value);
		var normalized = new ProblemDetails
		{
			Type = $"https://httpstatuses.com/{status}",
			Title = string.IsNullOrWhiteSpace(title) ? GetDefaultTitle(status, errors?.Count > 0) : title,
			Status = status,
			Detail = string.IsNullOrWhiteSpace(detail) ? GetDefaultTitle(status, errors?.Count > 0) : detail,
			Instance = instance
		};

		if (!string.IsNullOrWhiteSpace(errorCode))
		{
			normalized.Extensions["errorCode"] = errorCode;
		}
		else if (status == StatusCodes.Status400BadRequest && errors is { Count: > 0 })
		{
			normalized.Extensions["errorCode"] = "VALIDATION_ERROR";
		}

		if (errors is { Count: > 0 })
		{
			normalized.Extensions["errors"] = errors;
		}

		return normalized;
	}

	private static (string? title, string? detail, string? errorCode, Dictionary<string, string[]>? errors) ParseErrorPayload(object? value)
	{
		if (value is null)
		{
			return (null, null, null, null);
		}

		if (value is string text)
		{
			if (text.Contains("<html", StringComparison.OrdinalIgnoreCase))
			{
				return (null, null, null, null);
			}

			return (null, text, null, null);
		}

		var reflected = ParseErrorPayloadFromObject(value);
		if (!string.IsNullOrWhiteSpace(reflected.title) ||
		    !string.IsNullOrWhiteSpace(reflected.detail) ||
		    !string.IsNullOrWhiteSpace(reflected.errorCode) ||
		    reflected.errors is { Count: > 0 })
		{
			return reflected;
		}

		try
		{
			using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				return (null, root.ToString(), null, null);
			}

			var title = TryGetString(root, "title");
			var detail = TryGetString(root, "detail") ?? TryGetString(root, "message");
			var errorCode = TryGetString(root, "errorCode");
			var errors = ExtractErrors(root);

			if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.Object)
			{
				errorCode ??= TryGetString(errorElement, "code");
				detail ??= TryGetString(errorElement, "details") ?? TryGetString(errorElement, "message");
				if (errors is null || errors.Count == 0)
				{
					errors = ExtractValidationErrors(errorElement);
				}
			}

			return (title, detail, errorCode, errors);
		}
		catch
		{
			return (null, value.ToString(), null, null);
		}
	}

	private static (string? title, string? detail, string? errorCode, Dictionary<string, string[]>? errors) ParseErrorPayloadFromObject(object value)
	{
		var title = GetObjectString(value, "Title");
		var detail = GetObjectString(value, "Detail") ?? GetObjectString(value, "Message");
		var errorCode = GetObjectString(value, "ErrorCode") ?? GetObjectString(value, "Code");
		var errors = ExtractObjectErrors(GetObjectProperty(value, "Errors"))
		            ?? ExtractObjectValidationErrors(GetObjectProperty(value, "ValidationErrors"));

		var errorObject = GetObjectProperty(value, "Error");
		if (errorObject is not null)
		{
			errorCode ??= GetObjectString(errorObject, "Code");
			detail ??= GetObjectString(errorObject, "Details") ?? GetObjectString(errorObject, "Message");
			errors ??= ExtractObjectErrors(GetObjectProperty(errorObject, "Errors"))
			        ?? ExtractObjectValidationErrors(GetObjectProperty(errorObject, "ValidationErrors"));
		}

		return (title, detail, errorCode, errors);
	}

	private static string GetDefaultTitle(int status, bool hasValidationErrors)
	{
		if (status == StatusCodes.Status400BadRequest && hasValidationErrors)
		{
			return "Validation failed";
		}

		return status switch
		{
			StatusCodes.Status401Unauthorized => "Unauthorized",
			StatusCodes.Status403Forbidden => "Forbidden",
			StatusCodes.Status404NotFound => "Resource not found",
			StatusCodes.Status409Conflict => "Conflict",
			StatusCodes.Status413PayloadTooLarge => "Payload too large",
			_ => "Request failed"
		};
	}

	private static string? TryGetString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var property))
		{
			return null;
		}

		return property.ValueKind switch
		{
			JsonValueKind.String => property.GetString(),
			JsonValueKind.Null => null,
			_ => property.ToString()
		};
	}

	private static Dictionary<string, string[]>? ExtractErrors(JsonElement root)
	{
		if (!root.TryGetProperty("errors", out var errorsElement) || errorsElement.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in errorsElement.EnumerateObject())
		{
			var messages = new List<string>();
			if (field.Value.ValueKind == JsonValueKind.Array)
			{
				messages.AddRange(field.Value.EnumerateArray()
					.Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
					.Where(v => !string.IsNullOrWhiteSpace(v))!
					.Select(v => v!));
			}
			else
			{
				var message = field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() : field.Value.ToString();
				if (!string.IsNullOrWhiteSpace(message))
				{
					messages.Add(message);
				}
			}

			if (messages.Count > 0)
			{
				result[field.Name] = messages.ToArray();
			}
		}

		return result.Count > 0 ? result : null;
	}

	private static Dictionary<string, string[]>? ExtractValidationErrors(JsonElement errorElement)
	{
		if (!errorElement.TryGetProperty("validationErrors", out var validationErrorsElement) ||
		    validationErrorsElement.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var validationError in validationErrorsElement.EnumerateArray())
		{
			var message = TryGetString(validationError, "message");
			if (string.IsNullOrWhiteSpace(message))
			{
				continue;
			}

			if (!validationError.TryGetProperty("members", out var membersElement) ||
			    membersElement.ValueKind != JsonValueKind.Array)
			{
				AppendError(result, "_", message);
				continue;
			}

			var members = membersElement.EnumerateArray()
				.Where(m => m.ValueKind == JsonValueKind.String)
				.Select(m => m.GetString())
				.Where(m => !string.IsNullOrWhiteSpace(m))
				.Select(m => m!)
				.ToArray();

			if (members.Length == 0)
			{
				AppendError(result, "_", message);
				continue;
			}

			foreach (var member in members)
			{
				AppendError(result, member, message);
			}
		}

		return result.Count == 0
			? null
			: result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Distinct(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
	}

	private static object? GetObjectProperty(object instance, string propertyName)
	{
		var property = instance.GetType().GetProperty(
			propertyName,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

		return property?.GetValue(instance);
	}

	private static string? GetObjectString(object instance, string propertyName)
	{
		var value = GetObjectProperty(instance, propertyName);
		return value?.ToString();
	}

	private static Dictionary<string, string[]>? ExtractObjectErrors(object? rawErrors)
	{
		if (rawErrors is null)
		{
			return null;
		}

		if (rawErrors is IDictionary dictionary)
		{
			var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
			foreach (DictionaryEntry entry in dictionary)
			{
				var key = entry.Key?.ToString();
				if (string.IsNullOrWhiteSpace(key))
				{
					continue;
				}

				var messages = ToMessages(entry.Value);
				if (messages.Count > 0)
				{
					result[key] = messages.ToArray();
				}
			}

			return result.Count > 0 ? result : null;
		}

		return null;
	}

	private static Dictionary<string, string[]>? ExtractObjectValidationErrors(object? rawValidationErrors)
	{
		if (rawValidationErrors is not IEnumerable enumerable || rawValidationErrors is string)
		{
			return null;
		}

		var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in enumerable)
		{
			if (item is null)
			{
				continue;
			}

			var message = GetObjectString(item, "Message");
			if (string.IsNullOrWhiteSpace(message))
			{
				continue;
			}

			var members = GetObjectProperty(item, "Members") as IEnumerable;
			if (members is null)
			{
				AppendError(result, "_", message);
				continue;
			}

			var memberNames = members.Cast<object?>()
				.Select(m => m?.ToString())
				.Where(m => !string.IsNullOrWhiteSpace(m))
				.Select(m => m!)
				.ToArray();

			if (memberNames.Length == 0)
			{
				AppendError(result, "_", message);
				continue;
			}

			foreach (var memberName in memberNames)
			{
				AppendError(result, memberName, message);
			}
		}

		return result.Count == 0
			? null
			: result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Distinct(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase);
	}

	private static List<string> ToMessages(object? value)
	{
		if (value is null)
		{
			return [];
		}

		if (value is string text)
		{
			return string.IsNullOrWhiteSpace(text) ? [] : [text];
		}

		if (value is IEnumerable enumerable)
		{
			return enumerable.Cast<object?>()
				.Select(v => v?.ToString())
				.Where(v => !string.IsNullOrWhiteSpace(v))
				.Select(v => v!)
				.ToList();
		}

		var scalar = value.ToString();
		return string.IsNullOrWhiteSpace(scalar) ? [] : [scalar];
	}

	private static void AppendError(Dictionary<string, List<string>> errors, string key, string message)
	{
		if (!errors.TryGetValue(key, out var list))
		{
			list = [];
			errors[key] = list;
		}

		list.Add(message);
	}
}
