using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Deterministic ordering shared by initial enrollment, Records and annual cohorts.</summary>
public sealed class StudentRollOrdering
{
	public string Grouping { get; }
	public string NameFormat { get; }
	public string SortBy => fields[0].Field;
	public string SortDirection => fields[0].Descending ? "descending" : "ascending";
	private readonly List<(string Field, bool Descending)> fields = [];
	public StudentRollOrdering(JsonElement? rule)
	{
		Grouping = rule.HasValue ? OnboardingRuleConfiguration.Text(rule.Value, "grouping", "mixed") : "mixed";
		NameFormat = rule.HasValue ? OnboardingRuleConfiguration.Text(rule.Value, "nameFormat", "first-name-first") : "first-name-first";
		if (Grouping is not ("mixed" or "boys-first" or "girls-first") || NameFormat is not ("first-name-first" or "surname-first")) Invalid();
		if (rule.HasValue && rule.Value.TryGetProperty("orderingRules", out var configured) && configured.ValueKind == JsonValueKind.Array)
			foreach (var field in configured.EnumerateArray()) Add(OnboardingRuleConfiguration.Text(field, "field"), OnboardingRuleConfiguration.Text(field, "direction", "ascending"));
		if (fields.Count == 0) Add(rule.HasValue ? OnboardingRuleConfiguration.Text(rule.Value, "arrangeBy", "studentName") : "studentName",
			rule.HasValue ? OnboardingRuleConfiguration.Text(rule.Value, "arrangeType", "ascending") : "ascending");
	}
	private static void Invalid() => throw new BusinessException(StudentRollNumberErrorCodes.InvalidRule);
	private void Add(string field, string direction)
	{
		if (field is not ("studentName" or "grNumber" or "admissionNumber" or "admissionDate") ||
			direction is not ("ascending" or "descending") || fields.Count >= 4 || fields.Any(x => x.Field == field)) Invalid();
		fields.Add((field, direction == "descending"));
	}
	public string Signature => Grouping + ":" + NameFormat + ":" + string.Join("|", fields.Select(x => x.Field + ":" + x.Descending));
	public string? Issue(StudentRollCandidate student)
	{
		if (Grouping != "mixed" && student.Gender is not (GenderEnum.Male or GenderEnum.Female))
			return "Correct the student's saved gender before using gender-based numbering.";
		if (fields.Any(x => x.Field == "studentName") && NameFormat == "surname-first" && string.IsNullOrWhiteSpace(student.LastName))
			return "Add the student's surname before using surname-first numbering.";
		foreach (var field in fields)
			if (string.IsNullOrWhiteSpace(Key(student, field.Field))) return field.Field switch {
				"grNumber" => "Assign a GR number before using GR-number ordering.",
				"admissionNumber" => "Complete the admission number before using admission-number ordering.",
				"admissionDate" => "Review the saved admission date before using admission-date ordering.",
				_ => "Complete the student's saved name before assigning roll numbers." };
		return null;
	}
	public IReadOnlyList<StudentRollCandidate> Order(IEnumerable<StudentRollCandidate> students)
	{
		var result = students.ToList();
		if (result.Any(x => Issue(x) != null)) throw new BusinessException(StudentRollNumberErrorCodes.BatchBlocked);
		result.Sort(Compare); return result;
	}
	private int Compare(StudentRollCandidate left, StudentRollCandidate right)
	{
		var group = Rank(left).CompareTo(Rank(right)); if (group != 0) return group;
		foreach (var field in fields)
		{
			var comparison = NaturalCompare(Key(left, field.Field), Key(right, field.Field));
			if (comparison != 0) return field.Descending ? -comparison : comparison;
		}
		return left.StudentId.CompareTo(right.StudentId);
	}
	private int Rank(StudentRollCandidate student) => Grouping == "mixed" ? 0 :
		student.Gender == (Grouping == "boys-first" ? GenderEnum.Male : GenderEnum.Female) ? 0 : 1;
	private string Key(StudentRollCandidate student, string field) => field switch {
		"studentName" => string.Join(" ", (NameFormat == "surname-first"
			? new[] { student.LastName, student.FirstName, student.MiddleName }
			: new[] { student.FirstName, student.MiddleName, student.LastName }).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())),
		"grNumber" => student.GrNumber?.Trim() ?? "",
		"admissionNumber" => student.AdmissionNumber?.Trim() ?? "",
		"admissionDate" => student.AdmissionDate?.ToUniversalTime().ToString("O") ?? "", _ => "" };
	private static int NaturalCompare(string first, string second)
	{
		// Digit runs compare numerically without overflow; GR-2 precedes GR-10.
		var i = 0; var j = 0;
		while (i < first.Length && j < second.Length)
		{
			if (char.IsAsciiDigit(first[i]) && char.IsAsciiDigit(second[j]))
			{
				var a = i; var b = j;
				while (i < first.Length && char.IsAsciiDigit(first[i])) i++;
				while (j < second.Length && char.IsAsciiDigit(second[j])) j++;
				var left = first[a..i].TrimStart('0'); var right = second[b..j].TrimStart('0');
				var numeric = left.Length.CompareTo(right.Length);
				if (numeric == 0) numeric = StringComparer.Ordinal.Compare(left, right);
				if (numeric != 0) return numeric;
			}
			else { var text = char.ToUpperInvariant(first[i++]).CompareTo(char.ToUpperInvariant(second[j++])); if (text != 0) return text; }
		}
		return (first.Length - i).CompareTo(second.Length - j);
	}
}
