using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationCloneJson
{
	private static readonly Regex GuidPattern = new(
		@"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static IEnumerable<Guid> Tokens(string value)
	{
		foreach (Match match in GuidPattern.Matches(value))
			if (Guid.TryParse(match.Value, out var id)) yield return id;
	}

	public static IEnumerable<string> Values(AdmissionCrmConfigurationSnapshot snapshot)
	{
		if (snapshot.Cycle != null)
		{
			yield return snapshot.Cycle.LeadScoringRulesJson;
			yield return snapshot.Cycle.LeadRoutingRulesJson;
			yield return snapshot.Cycle.JourneyRulesJson;
			yield return snapshot.Cycle.ApplicantChecklistJson;
			yield return snapshot.Cycle.ChannelSettingsJson;
			yield return snapshot.Cycle.SlaPolicyJson;
		}
		foreach (var x in snapshot.InquiryForms) { yield return x.FieldsJson; yield return x.LocalizationsJson; }
		foreach (var x in snapshot.MessageTemplates) { yield return x.Body; yield return x.VariablesJson; }
		foreach (var x in snapshot.Playbooks)
		{
			yield return x.ApplicabilityJson; yield return x.StagesJson; yield return x.RoundsJson;
			yield return x.SeatPlansJson; yield return x.ExamDefinitionsJson; yield return x.SlotTemplatesJson;
			yield return x.AssignmentRulesJson;
		}
		foreach (var x in snapshot.RegistrationTemplates) { yield return x.ApplicabilityJson; yield return x.SectionsJson; }
		foreach (var x in snapshot.FormNumberSeries) yield return x.ApplicabilityJson;
		foreach (var x in snapshot.DocumentChecklists) { yield return x.ApplicabilityJson; yield return x.RequirementsJson; }
		foreach (var x in snapshot.FeePolicies) { yield return x.ApplicabilityJson; yield return x.ComponentsJson; }
		foreach (var x in snapshot.RouteDefinitions) yield return x.ConfigurationJson;
		foreach (var x in snapshot.PolicyAdoptions) { yield return x.ApplicabilityJson; yield return x.OverridesJson; }
	}

	public static IEnumerable<Guid> Guids(string json)
	{
		JsonDocument document;
		try { document = JsonDocument.Parse(json); }
		catch (JsonException) { yield break; }
		using (document)
			foreach (var value in Guids(document.RootElement)) yield return value;
	}

	private static IEnumerable<Guid> Guids(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Array:
				foreach (var item in element.EnumerateArray()) foreach (var value in Guids(item)) yield return value;
				break;
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject()) foreach (var value in Guids(property.Value)) yield return value;
				break;
			case JsonValueKind.String:
				if (Guid.TryParse(element.GetString(), out var id)) yield return id;
				break;
		}
	}
}
