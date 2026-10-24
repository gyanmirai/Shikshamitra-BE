using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Canonical settings for the five-stage admission model.</summary>
public static class AdmissionStageConfiguration
{
	public static string? Normalize(AdmissionWorkflowStageType type, string? configurationJson)
	{
		var root = string.IsNullOrWhiteSpace(configurationJson) ? new JsonObject() :
			JsonNode.Parse(configurationJson, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject ?? throw new ArgumentException("Stage settings must be an object.");
		switch (type)
		{
			case AdmissionWorkflowStageType.Form:
				var form = Settings(root, "form");
				form["allowSaveAndResume"] = true;
				form["autoAdvanceOnSubmit"] = true;
				form["requireSectionVerification"] = false;
				form["verificationSectionCodes"] = new JsonArray();
				break;
			case AdmissionWorkflowStageType.Assessment:
				var assessment = Settings(root, "assessment");
				assessment["reviewRequired"] = false;
				assessment["maxAttempts"] ??= 1;
				break;
			case AdmissionWorkflowStageType.Payment:
				root.Remove("payment");
				_ = AdmissionPaymentCompletionPolicy.Read(root.ToJsonString());
				break;
			case AdmissionWorkflowStageType.DocumentVerification:
				var documents = Settings(root, "documentVerification");
				documents["allowWaivers"] = false;
				documents["requireIndependentReview"] = false;
				documents["collectionFormStageCode"] = null;
				if (documents["requirements"] is JsonArray requirements)
				{
					var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var item in requirements)
						if (item is JsonObject existing && existing["code"]?.GetValue<string>() is string code && !string.IsNullOrWhiteSpace(code))
							codes.Add(code);
					foreach (var item in requirements)
					{
						if (item is not JsonObject requirement) throw new ArgumentException("Document requirements must be objects.");
						if (requirement["requirement"]?.GetValue<string>() is string level && level is not ("required" or "optional"))
							throw new ArgumentException("Choose required or optional for each document.");
						if (string.IsNullOrWhiteSpace(requirement["code"]?.GetValue<string>()))
						{
							var name = requirement["name"]?.GetValue<string>() ?? "Document";
							var stem = "DOC_" + Regex.Replace(name.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
							stem = stem[..Math.Min(stem.Length, 54)];
							var generated = stem;
							var suffix = 2;
							while (!codes.Add(generated)) generated = $"{stem}_{suffix++}";
							requirement["code"] = generated;
						}
						requirement["collectionMode"] = "upload";
					}
				}
				break;
		}
		return root.Count == 0 ? null : root.ToJsonString();
	}

	private static JsonObject Settings(JsonObject root, string key)
	{
		var settings = root[key] as JsonObject ?? new JsonObject();
		if (root[key] == null) root[key] = settings;
		else if (root[key] is not JsonObject) throw new ArgumentException("Stage settings must be an object.");
		settings["version"] ??= 1;
		settings["enabled"] = true;
		return settings;
	}
}
