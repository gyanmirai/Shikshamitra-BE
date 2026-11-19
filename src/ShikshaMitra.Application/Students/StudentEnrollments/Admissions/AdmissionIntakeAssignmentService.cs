using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// Compatibility service for historical callers. Ownership is no longer assigned.
[RemoteService(false)]
public class AdmissionIntakeAssignmentService : ApplicationService, ITransientDependency
{
	public Task<Guid?> ResolveAsync(AdmissionOffering offering) => Task.FromResult<Guid?>(null);
	public Task<Guid?> ResolveForCycleAsync(Guid assignmentProfileId, Guid cycleId) => Task.FromResult<Guid?>(null);
	public Task<AdmissionCounsellorAssignmentRules> GetRulesAsync(Guid offeringId) => throw Retired();
	public Task EnsureManualTransferAsync(Guid offeringId, Guid counsellorId, Guid inquiryId) => throw Retired();
	private static BusinessException Retired() => new("Admissions.StaffAssignmentRetired", "Staff handling is recorded automatically in the activity history.");
}
