using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Policies;

// Reuses the operational assignment winner, including actual group membership.
// Leave policy dates deliberately do not change usability: today they bound accrual only.
public class LeavePolicyImpactResolutionAdapter(HrPolicyAssignmentResolver assignments) : DomainService
{
	public IDisposable BeginReadBatch() => assignments.BeginReadBatch();

	public async Task<LeaveImpactResolution> ResolveAsync(Employee employee, Guid year, DateTime date,
		IReadOnlyList<LeavePolicy> policies, Guid? proposedDefaultId = null,
		Guid? proposedPolicyId = null, HrPolicyStatus? proposedStatus = null)
	{
		var resolution = await assignments.ResolveForEmployeeAsync(employee, year, date.Date, default);
		var explicitPolicy = resolution.Assignment?.LeavePolicyId;
		var defaults = policies.Where(x => x.AcademicYearId == year && x.Status == HrPolicyStatus.Active && x.IsActive && x.IsDefault).ToList();
		var id = explicitPolicy ?? proposedDefaultId ?? (defaults.Count == 1 ? defaults[0].Id : (Guid?)null);
		var policy = policies.FirstOrDefault(x => x.Id == id);
		var hasProposal = id.HasValue && id == proposedPolicyId && proposedStatus.HasValue;
		var status = hasProposal ? proposedStatus : policy?.Status;
		var isActive = hasProposal ? status == HrPolicyStatus.Active : policy?.IsActive == true;
		return new(id, id.HasValue && status == HrPolicyStatus.Active && isActive &&
			(policy == null ? id == proposedPolicyId : policy.AcademicYearId == year),
			explicitPolicy.HasValue ? resolution.Assignment!.TargetType.ToString().ToLowerInvariant() : "default",
			resolution.Assignment?.Id);
	}
}

public sealed record LeaveImpactResolution(Guid? PolicyId, bool Usable, string Source, Guid? AssignmentId);
