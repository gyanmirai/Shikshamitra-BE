using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.Attendance;

public sealed record AttendancePolicySnapshot(AttendancePolicy Policy, AttendancePolicyOptions Options, Guid? VersionId,
	Guid? ScopeAcademicYearId, bool IsStarter, int VersionNumber = 0);

public class AttendancePolicyResolver : DomainService
{
	public Task<AttendancePolicySnapshot> ResolveAsync(Guid? yearId, DateTime date, AttendancePolicyScope? scope = null)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		var policy = new AttendancePolicy();
		return Task.FromResult(new AttendancePolicySnapshot(policy, policy.Defaults, null, null, true));
	}
}
