using System;
using System.Text.Json.Nodes;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class StudentJourneyActorMetadata
{
	public static string Capture(string? json, Guid? actorId, string? actorName)
	{
		var metadata = string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json) as JsonObject
			?? new JsonObject { ["Details"] = JsonNode.Parse(json) };
		metadata["ActorUserId"] = actorId?.ToString("D");
		metadata["ActorName"] = actorName;
		return metadata.ToJsonString();
	}

	public static string? Name(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try { return (JsonNode.Parse(json) as JsonObject)?["ActorName"]?.GetValue<string>(); }
		catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException) { return null; }
	}
}
