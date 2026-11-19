using System;
using System.Collections.Generic;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

namespace ShikshaMitra.Students.Records;

internal sealed class StudentComplianceReportRow
{
	public string Name { get; set; } = "";
	public string? GrNumber { get; set; }
	public int? GrNo { get; set; }
	public EducationBoardType? Board { get; set; }
	public string? Medium { get; set; }
	public string? Class { get; set; }
	public string? Division { get; set; }
	public bool HasCompliance { get; set; }
	public string? AadhaarLast4 { get; set; }
	public IdentityVerificationStatus AadhaarStatus { get; set; }
	public string? ApaarId { get; set; }
	public IdentityVerificationStatus ApaarStatus { get; set; }
	public ConsentStatus Consent { get; set; }
	public string? Pen { get; set; }
	public string? UdiseCode { get; set; }
	public UdiseSyncStatus UdiseStatus { get; set; }
	public DateTimeOffset? UdiseExported { get; set; }
	public bool Rte { get; set; }
	public ScholarshipStatus Scholarship { get; set; }
	public string? Scheme { get; set; }
	public string? Application { get; set; }
	public string? CasteCertificate { get; set; }
	public IdentityVerificationStatus CasteStatus { get; set; }
}

internal static class StudentComplianceReportProjection
{
	public static List<string> Columns(string type)
	{
		var columns = new List<string> { "Student", "GR number", "Board", "Medium", "Class", "Division" };
		if (type == "EligibilityTracking")
			columns.AddRange(["RTE recorded", "Scholarship status", "Scholarship scheme", "Scholarship application (masked)", "Caste certificate (masked)", "Caste certificate verification"]);
		else
		{
			columns.AddRange(["Aadhaar (masked)", "Aadhaar verification", "APAAR (masked)", "APAAR consent", "APAAR verification"]);
			if (type != "IdentityExceptions")
				columns.AddRange(["PEN (masked)", "UDISE code (masked)", "UDISE sync status", "Last UDISE export"]);
		}
		return columns;
	}

	public static Dictionary<string, string?> Project(StudentComplianceReportRow row, string type, bool hideIdentity, bool hideCompliance)
	{
		var result = new Dictionary<string, string?>
		{
			["Student"] = hideIdentity ? "Restricted student" : row.Name.Trim(),
			["GR number"] = hideIdentity ? "Restricted" : row.GrNumber ?? row.GrNo?.ToString(),
			["Board"] = hideIdentity ? "Restricted" : row.Board?.ToString(),
			["Medium"] = hideIdentity ? "Restricted" : row.Medium,
			["Class"] = hideIdentity ? "Restricted" : row.Class,
			["Division"] = hideIdentity ? "Restricted" : row.Division
		};
		if (type == "EligibilityTracking")
		{
			result["RTE recorded"] = row.HasCompliance ? row.Rte ? "Yes" : "No" : "Not recorded";
			result["Scholarship status"] = row.HasCompliance ? row.Scholarship.ToString() : "Not recorded";
			result["Scholarship scheme"] = row.Scheme;
			result["Scholarship application (masked)"] = Mask(row.Application);
			result["Caste certificate (masked)"] = Mask(row.CasteCertificate);
			result["Caste certificate verification"] = row.CasteStatus.ToString();
		}
		else
		{
			result["Aadhaar (masked)"] = string.IsNullOrWhiteSpace(row.AadhaarLast4) ? null : $"XXXX-XXXX-{row.AadhaarLast4}";
			result["Aadhaar verification"] = row.AadhaarStatus.ToString();
			result["APAAR (masked)"] = Mask(row.ApaarId);
			result["APAAR consent"] = row.Consent.ToString();
			result["APAAR verification"] = row.ApaarStatus.ToString();
			if (type != "IdentityExceptions")
			{
				result["PEN (masked)"] = Mask(row.Pen);
				result["UDISE code (masked)"] = Mask(row.UdiseCode);
				result["UDISE sync status"] = row.UdiseStatus.ToString();
				result["Last UDISE export"] = row.UdiseExported?.ToString("O");
			}
		}
		if (hideCompliance)
			foreach (var column in Columns(type).GetRange(6, Columns(type).Count - 6)) result[column] = "Restricted";
		return result;
	}

	private static string? Mask(string? value) => string.IsNullOrWhiteSpace(value) ? null :
		value.Length <= 4 ? "XXXX" : $"XXXX-{value[^4..]}";
}
