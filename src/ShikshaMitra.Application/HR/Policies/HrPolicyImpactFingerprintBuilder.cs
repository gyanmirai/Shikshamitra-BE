using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyImpactFingerprintBuilder
{
	public static string Build(HrPolicyImpactPreviewDto preview)
	{
		var payload = new
		{
			LeaveFingerprint = preview.LeaveImpact?.Fingerprint,
			preview.AffectedEmployeeCount,
			preview.AffectedAssignmentCount,
			Warnings = preview.Warnings
				.Select(x => new { Severity = NormalizeToken(x.Severity), x.Code, x.Message })
				.OrderBy(x => x.Severity)
				.ThenBy(x => x.Code, StringComparer.Ordinal)
				.ThenBy(x => x.Message, StringComparer.Ordinal)
				.ToList(),
			AffectedEmployees = preview.AffectedEmployees
				.Select(x => new { x.EmployeeId, x.EmployeeCode, x.EmployeeName, x.Department, x.RoleId, x.ShiftId, x.EmployeeType })
				.OrderBy(x => x.EmployeeId)
				.ThenBy(x => x.EmployeeCode, StringComparer.Ordinal)
				.ToList(),
			LockedPayrollMonths = preview.LockedPayrollMonths
				.Select(x => new { x.Year, x.Month, x.Label, Status = NormalizeToken(x.Status), x.Count })
				.OrderBy(x => x.Year)
				.ThenBy(x => x.Month)
				.ThenBy(x => x.Status, StringComparer.Ordinal)
				.ToList(),
			AffectedPayrollMonths = preview.AffectedPayrollMonths
				.Select(x => new { x.Year, x.Month, x.Label, Status = NormalizeToken(x.Status), x.Count })
				.OrderBy(x => x.Year)
				.ThenBy(x => x.Month)
				.ThenBy(x => x.Status, StringComparer.Ordinal)
				.ToList(),
			AffectedAttendanceEvaluationMonths = preview.AffectedAttendanceEvaluationMonths
				.Select(x => new { x.Year, x.Month, x.Label, Status = NormalizeToken(x.Status), x.Count })
				.OrderBy(x => x.Year)
				.ThenBy(x => x.Month)
				.ThenBy(x => x.Status, StringComparer.Ordinal)
				.ToList(),
			EffectiveAssignmentConflicts = preview.EffectiveAssignmentConflicts
				.Select(x => new
				{
					x.EmployeeId,
					x.Date,
					WinnerAssignmentId = x.WinningAssignment?.AssignmentId,
					WinnerTargetType = NormalizeToken(x.WinningAssignment?.TargetType),
					WinnerTargetKey = x.WinningAssignment?.TargetKey,
					WinnerPayrollPolicyId = x.WinningAssignment?.PayrollPolicyId,
					WinnerAttendancePolicyId = x.WinningAssignment?.AttendancePolicyId,
					Candidates = x.CandidateAssignments
						.Select(candidate => new
						{
							candidate.AssignmentId,
							TargetType = NormalizeToken(candidate.TargetType),
							candidate.TargetKey,
							candidate.PayrollPolicyId,
							candidate.AttendancePolicyId,
							candidate.EffectiveFrom,
							candidate.EffectiveTo,
							candidate.Priority,
							Status = NormalizeToken(candidate.Status),
							SkipReason = NormalizeToken(candidate.SkipReason)
						})
						.OrderBy(candidate => candidate.AssignmentId)
						.ThenBy(candidate => candidate.TargetType, StringComparer.Ordinal)
						.ThenBy(candidate => candidate.TargetKey, StringComparer.Ordinal)
						.ToList()
				})
				.OrderBy(x => x.EmployeeId)
				.ThenBy(x => x.Date)
				.ToList()
		};
		var json = JsonSerializer.Serialize(payload);
		var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
		return Convert.ToHexString(hash);
	}

	private static string NormalizeToken(string? value) =>
		(value ?? string.Empty).Trim().ToLowerInvariant();
}
