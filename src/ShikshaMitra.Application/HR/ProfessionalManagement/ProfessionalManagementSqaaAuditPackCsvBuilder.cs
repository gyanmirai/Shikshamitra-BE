using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class ProfessionalManagementSqaaAuditPackCsvBuilder
{
	public static string Build(PerformanceSqaaAuditPackDto auditPack)
	{
		var lines = new List<string>
		{
			"SQAAF Audit Pack",
			$"Generated At,{auditPack.GeneratedAt:O}",
			$"Overall Readiness,{auditPack.OverallReadinessPercentage}%",
			$"Total Required Evidence,{auditPack.TotalRequiredEvidence}",
			$"Total Submitted Evidence,{auditPack.TotalEvidenceSubmitted}",
			$"Total Missing Evidence,{auditPack.TotalMissingEvidence}",
			string.Empty,
			"Domain,Readiness %,Templates,Assigned Reviews,Mapped Criteria,Required Evidence,Submitted Evidence,Missing Evidence"
		};

		lines.AddRange(auditPack.Domains.Select(domain =>
			string.Join(",", [
				EscapeCsv(domain.Domain.ToString()),
				domain.ReadinessPercentage.ToString(),
				domain.TemplateCount.ToString(),
				domain.AssignedReviewCount.ToString(),
				domain.MappedCriteria.ToString(),
				domain.EvidenceRequiredCriteria.ToString(),
				domain.EvidenceSubmitted.ToString(),
				domain.MissingEvidence.ToString()
			])));

		lines.Add(string.Empty);
		lines.Add("Missing Evidence");
		lines.Add("Domain,Cycle,Staff Code,Staff,Template,Criterion");
		lines.AddRange(auditPack.MissingEvidenceItems.Select(item =>
			string.Join(",", [
				EscapeCsv(item.Domain.ToString()),
				EscapeCsv(item.CycleName),
				EscapeCsv(item.EmployeeIdentifier),
				EscapeCsv(item.EmployeeName),
				EscapeCsv(item.TemplateName),
				EscapeCsv(item.CriterionName)
			])));

		return string.Join(Environment.NewLine, lines);
	}

	private static string EscapeCsv(string? value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		var escaped = value.Replace("\"", "\"\"");
		return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r')
			? $"\"{escaped}\""
			: escaped;
	}
}
