using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmMessageTemplateRenderer
{
	public static Dictionary<string, string> SystemVariables(AdmissionInquiryLead lead)
	{
		var studentName = string.Join(" ", new[] { lead.FirstName, lead.MiddleName, lead.LastName }
			.Where(x => !string.IsNullOrWhiteSpace(x)));
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["studentName"] = string.IsNullOrWhiteSpace(studentName) ? "Applicant" : studentName,
			["parentName"] = lead.ParentGuardianName ?? "Parent", ["admissionIntent"] = lead.AdmissionIntent,
			["desiredClass"] = lead.DesiredClassName ?? string.Empty, ["campusBranch"] = lead.CampusBranchName ?? string.Empty,
			["preferredLanguage"] = lead.PreferredCommunicationLanguage ?? string.Empty,
			["primaryContactNo"] = lead.PrimaryContactNo ?? string.Empty, ["email"] = lead.Email ?? string.Empty,
			["leadScore"] = lead.LeadScore.ToString()
		};
	}

	public static Dictionary<string, string> PreviewVariables(
		AdmissionCrmMessageTemplate template,
		AdmissionInquiryLead? lead,
		Dictionary<string, string>? providedVariables)
	{
		var variables = lead is null ? SampleVariables() : SystemVariables(lead);
		foreach (var sampleVariable in SampleVariables()) variables.TryAdd(sampleVariable.Key, sampleVariable.Value);
		foreach (var variable in DeserializeVariables(template.VariablesJson)) variables.TryAdd(variable, $"{variable} sample");
		foreach (var variable in providedVariables ?? []) variables[variable.Key] = variable.Value;
		return variables;
	}

	public static string? Render(string? template, IReadOnlyDictionary<string, string> variables)
	{
		if (string.IsNullOrWhiteSpace(template)) return template;
		var rendered = template;
		foreach (var variable in variables)
			rendered = rendered.Replace("{{" + variable.Key + "}}", variable.Value, StringComparison.OrdinalIgnoreCase);
		return rendered;
	}

	public static IReadOnlyList<string> FindUnresolved(params string?[] renderedValues) => renderedValues
		.Where(value => !string.IsNullOrWhiteSpace(value))
		.SelectMany(value => Regex.Matches(value!, "\\{\\{\\s*(?<name>[^{}]+?)\\s*\\}\\}").Cast<Match>())
		.Select(match => match.Groups["name"].Value.Trim()).Where(name => !string.IsNullOrWhiteSpace(name))
		.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();

	public static string ResolveRecipient(AdmissionInquiryLead lead, AdmissionCrmMessageChannel channel, string? recipientOverride)
	{
		if (!string.IsNullOrWhiteSpace(recipientOverride)) return recipientOverride.Trim();
		var recipient = channel == AdmissionCrmMessageChannel.Email ? lead.Email : lead.PrimaryContactNo;
		if (string.IsNullOrWhiteSpace(recipient))
			throw new Volo.Abp.BusinessException("AdmissionCrm:MessageRecipientMissing")
				.WithData("LeadId", lead.Id).WithData("Channel", channel);
		return recipient;
	}

	private static IReadOnlyList<string> DeserializeVariables(string variablesJson) =>
		JsonSerializer.Deserialize<List<string>>(variablesJson) ?? [];

	private static Dictionary<string, string> SampleVariables() => new(StringComparer.OrdinalIgnoreCase)
	{
		["studentName"] = "Sample Student", ["parentName"] = "Sample Parent", ["schoolName"] = "Sample School",
		["admissionIntent"] = "Admission inquiry for Grade 1", ["desiredClass"] = "Grade 1", ["primaryContactNo"] = "9999999999",
		["email"] = "parent@example.com", ["leadScore"] = "50", ["examDate"] = "15 July", ["examTime"] = "10:00 AM",
		["room"] = "101", ["statusLink"] = "https://school.example/status", ["ownerName"] = "Admissions Office",
		["nextStepInstructions"] = "Please visit the school reception."
	};
}
