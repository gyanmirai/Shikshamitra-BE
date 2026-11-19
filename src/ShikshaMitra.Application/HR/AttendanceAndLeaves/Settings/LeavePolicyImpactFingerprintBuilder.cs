using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

internal static class LeavePolicyImpactFingerprintBuilder
{
	public static string Build(Guid? tenantId, Guid? actorId, Guid revision,
		LeavePolicyImpactRequest input, LeavePolicyImpactDto result, IReadOnlyList<LeavePolicy> policies,
		LeaveImpactPopulation population, LeaveImpactReferences references, bool complete)
	{
		// Every set is canonicalized here, independently of database enumeration order.
		var json = JsonSerializer.Serialize(new
		{
			AssessmentSchemaVersion = 1,
			Tenant = tenantId, Actor = actorId, Input = input, Revision = revision,
			Measures = result.Measures.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new { x.Key, x.Value.Status, x.Value.Count, x.Value.Scope, x.Value.Definition, x.Value.AvailabilityReason }),
			Policies = policies.OrderBy(x => x.Id).Select(x => new { Snapshot = LeavePolicySnapshots.Capture(x), x.ConcurrencyStamp }),
			Staff = population.Staff.OrderBy(x => x.EmployeeId),
			AssignmentDependencies = population.AssignmentDependencies.OrderBy(x => x, StringComparer.Ordinal),
			Assignments = references.Assignments.OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp }),
			Requests = references.Requests.OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp, x.Status, x.LeavePolicyRuleId }),
			Settlements = references.Settlements.OrderBy(x => x.Id).Select(x => new { x.Id, x.ConcurrencyStamp }),
			PayrollDependencies = references.PayrollDependencies.OrderBy(x => x, StringComparer.Ordinal),
			PayrollPeriods = references.PayrollPeriods.OrderBy(x => x.Month, StringComparer.Ordinal),
			references.UnresolvedPayrollRecordCount, complete
		});
		return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
	}
}
