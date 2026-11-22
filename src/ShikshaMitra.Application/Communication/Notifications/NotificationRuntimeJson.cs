using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

internal static class NotificationRuntimeJson
{
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
		PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		RespectRequiredConstructorParameters = true, MaxDepth = 16
	};
	internal static string Serialize<T>(T value, string errorCode) where T : class
	{
		try
		{
			if (value == null) throw new JsonException();
			var json = JsonSerializer.Serialize(value, Options);
			if (json.Length > 65536) throw new JsonException(); return json;
		}
		catch (JsonException) { throw new BusinessException(errorCode); }
	}
	internal static T Deserialize<T>(string json, string errorCode) where T : class
	{
		try
		{
			if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) throw new JsonException();
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
			CheckUniqueMembers(document.RootElement);
			return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException();
		}
		catch (JsonException) { throw new BusinessException(errorCode); }
	}
	private static void CheckUniqueMembers(JsonElement element)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			var names = new HashSet<string>(System.StringComparer.Ordinal);
			foreach (var property in element.EnumerateObject())
			{
				if (!names.Add(property.Name)) throw new JsonException(); CheckUniqueMembers(property.Value);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
			foreach (var child in element.EnumerateArray()) CheckUniqueMembers(child);
	}
}
