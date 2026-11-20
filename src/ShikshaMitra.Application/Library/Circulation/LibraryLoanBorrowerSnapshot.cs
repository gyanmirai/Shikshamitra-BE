using System;
using System.Text.Json;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;

namespace ShikshaMitra.Library.Circulation;

internal static class LibraryLoanBorrowerSnapshot
{
	public static LibraryLoanLookupBorrowerDto? Read(BookIssue loan, Guid? canonicalMemberId)
	{
		if (string.IsNullOrWhiteSpace(loan.BorrowerSnapshotJson) || loan.BorrowerSnapshotJson.Length > 16384) return null;
		try
		{
			using var document = JsonDocument.Parse(loan.BorrowerSnapshotJson);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object || !Property(root, "Version", out var version) || version.ValueKind != JsonValueKind.Number ||
				!version.TryGetInt32(out var schema) || schema != 1 || !Property(root, "Id", out var identity) ||
				identity.ValueKind != JsonValueKind.String || !identity.TryGetGuid(out var id) || id == Guid.Empty ||
				id != loan.MemberId && id != canonicalMemberId || !Property(root, "Role", out var role) || role.ValueKind != JsonValueKind.Number ||
				!role.TryGetInt32(out var recordedRole) || recordedRole != loan.MemberRole ||
				!GuidPropertyMatches(root, "AcademicYearId", loan.AcademicYearId)) return null;
			if (Property(root, "AcademicContext", out var context) && context.ValueKind != JsonValueKind.Null &&
				(context.ValueKind != JsonValueKind.Object || !GuidPropertyMatches(context, "AcademicBoardId", loan.BorrowerBoardId) ||
				 !GuidPropertyMatches(context, "AcademicMediumId", loan.BorrowerMediumId) || !GuidPropertyMatches(context, "AcademicMediumStageId", loan.BorrowerStageId) ||
				 !GuidPropertyMatches(context, "StandardId", loan.BorrowerStandardId) || !GuidPropertyMatches(context, "DivisionId", loan.BorrowerDivisionId))) return null;
			return new() { MemberId = id, Role = recordedRole, MemberName = Text(root, "MemberName"),
				MemberIdentifier = Text(root, "MemberIdentifier"), ClassDivision = Text(root, "ClassDivision"), DisplaySource = "LoanSnapshot" };
		}
		catch (JsonException) { return null; }
	}

	private static bool GuidPropertyMatches(JsonElement root, string name, Guid? expected)
	{
		if (!Property(root, name, out var value)) return !expected.HasValue;
		if (value.ValueKind == JsonValueKind.Null) return !expected.HasValue;
		return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) && id == expected;
	}
	private static string? Text(JsonElement root, string name) => Property(root, name, out var value) && value.ValueKind == JsonValueKind.String &&
		value.GetString() is { Length: > 0 and <= 500 } text ? text : null;
	private static bool Property(JsonElement root, string name, out JsonElement value)
	{
		foreach (var property in root.EnumerateObject())
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
		value = default;
		return false;
	}
}
