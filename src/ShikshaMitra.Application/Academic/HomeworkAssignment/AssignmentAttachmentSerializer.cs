using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal static class AssignmentAttachmentSerializer
{
	public static string? Canonicalize(string? attachment)
	{
		var value = ReadSingle(attachment);
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var item = new AttachmentDto
		{
			Name = GetFileName(value),
			Url = value,
			Size = 0
		};
		return JsonSerializer.Serialize(new[] { item });
	}

	public static string? ReadSingle(string? storedValue)
	{
		if (string.IsNullOrWhiteSpace(storedValue))
		{
			return null;
		}

		var trimmed = storedValue.Trim();
		try
		{
			using var document = JsonDocument.Parse(trimmed);
			return document.RootElement.ValueKind switch
			{
				JsonValueKind.String => document.RootElement.GetString(),
				JsonValueKind.Array => ReadFirst(document.RootElement),
				JsonValueKind.Object => ReadObject(document.RootElement),
				_ => trimmed
			};
		}
		catch (JsonException)
		{
			return trimmed;
		}
	}

	public static List<AttachmentDto>? ReadLegacyList(string? storedValue)
	{
		if (string.IsNullOrWhiteSpace(storedValue))
		{
			return null;
		}

		var trimmed = storedValue.Trim();
		try
		{
			using var document = JsonDocument.Parse(trimmed);
			if (document.RootElement.ValueKind == JsonValueKind.Array)
			{
				var items = new List<AttachmentDto>();
				foreach (var element in document.RootElement.EnumerateArray())
				{
					var item = ReadLegacyItem(element);
					if (item != null)
					{
						items.Add(item);
					}
				}

				return items.Count == 0 ? null : items;
			}

			if (document.RootElement.ValueKind == JsonValueKind.String)
			{
				return CreateScalarList(document.RootElement.GetString());
			}

			return null;
		}
		catch (JsonException)
		{
			return CreateScalarList(trimmed);
		}
	}

	private static List<AttachmentDto>? CreateScalarList(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return
		[
			new AttachmentDto
			{
				Name = GetFileName(value),
				Url = value,
				Size = 0
			}
		];
	}

	private static AttachmentDto? ReadLegacyItem(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.String)
		{
			var value = element.GetString();
			return string.IsNullOrWhiteSpace(value)
				? null
				: new AttachmentDto { Name = GetFileName(value), Url = value, Size = 0 };
		}

		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		var url = ReadStringProperty(element, "url", "Url");
		var name = ReadStringProperty(element, "name", "Name");
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}

		return new AttachmentDto
		{
			Name = string.IsNullOrWhiteSpace(name) ? GetFileName(url) : name,
			Url = url,
			Size = ReadInt64Property(element, "size", "Size") ?? 0
		};
	}

	private static string? ReadStringProperty(JsonElement value, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			if (value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
			{
				return property.GetString();
			}
		}

		return null;
	}

	private static long? ReadInt64Property(JsonElement value, params string[] propertyNames)
	{
		foreach (var propertyName in propertyNames)
		{
			if (value.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var result))
			{
				return result;
			}
		}

		return null;
	}

	private static string? ReadFirst(JsonElement array)
	{
		foreach (var element in array.EnumerateArray())
		{
			var value = element.ValueKind == JsonValueKind.Object
				? ReadObject(element)
				: element.ValueKind == JsonValueKind.String ? element.GetString() : null;
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}

		return null;
	}

	private static string? ReadObject(JsonElement value)
	{
		foreach (var propertyName in new[] { "url", "Url", "name", "Name" })
		{
			if (value.TryGetProperty(propertyName, out var property) &&
				property.ValueKind == JsonValueKind.String &&
				!string.IsNullOrWhiteSpace(property.GetString()))
			{
				return property.GetString();
			}
		}

		return null;
	}

	private static string GetFileName(string value)
	{
		var normalized = value.Replace('\\', '/');
		var queryIndex = normalized.IndexOfAny(['?', '#']);
		if (queryIndex >= 0)
		{
			normalized = normalized[..queryIndex];
		}

		var slashIndex = normalized.LastIndexOf('/');
		return slashIndex >= 0 && slashIndex < normalized.Length - 1
			? normalized[(slashIndex + 1)..]
			: value;
	}
}
