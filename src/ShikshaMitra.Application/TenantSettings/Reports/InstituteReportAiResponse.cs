using System;
using System.Text;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.Reports;

public static class InstituteReportAiResponse
{
	public static string Read(string responseJson, bool chatCompletion)
	{
		try
		{
			using var document = JsonDocument.Parse(responseJson);
			var root = document.RootElement;
			var text = chatCompletion ? ReadChat(root) : ReadResponses(root);
			if (string.IsNullOrWhiteSpace(text)) throw Invalid();
			text = text.Trim();
			if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
			var firstLineEnd = text.IndexOf('\n');
			var closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
			return firstLineEnd >= 0 && closingFence > firstLineEnd ? text[(firstLineEnd + 1)..closingFence].Trim() : throw Invalid();
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException) { throw Invalid(); }
	}
	private static string ReadChat(JsonElement root)
	{
		if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) throw Invalid();
		var choice = choices[0];
		if (choice.TryGetProperty("finish_reason", out var reason) && reason.GetString() == "length") throw Incomplete();
		if (!choice.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String) throw Invalid();
		return content.GetString() ?? "";
	}
	private static string ReadResponses(JsonElement root)
	{
		if (root.TryGetProperty("status", out var status) && status.GetString() == "incomplete") throw Incomplete();
		if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) throw Invalid();
		var text = new StringBuilder();
		foreach (var item in output.EnumerateArray())
		{
			if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
			foreach (var part in content.EnumerateArray())
				if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
					text.Append(value.GetString());
		}
		return text.ToString();
	}
	private static BusinessException Invalid() => new("InstituteReports:AiInvalidResponse");
	private static BusinessException Incomplete() => new("InstituteReports:AiResponseIncomplete");
}
