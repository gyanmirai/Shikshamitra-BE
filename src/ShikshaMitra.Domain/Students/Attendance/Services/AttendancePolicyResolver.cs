using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.Attendance;

public sealed record AttendancePolicySnapshot(AttendancePolicy Policy, AttendancePolicyOptions Options, Guid? VersionId,
	Guid? ScopeAcademicYearId, bool IsStarter, int VersionNumber = 0);

public class AttendancePolicyResolver(IRepository<StudentAttendancePolicyVersion, Guid> versions) : DomainService
{
	public async Task<AttendancePolicySnapshot> ResolveAsync(Guid? yearId, DateTime date, AttendancePolicyScope? scope = null)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		var query = await versions.GetQueryableAsync();
		var version = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			(x.AcademicYearId == yearId || x.AcademicYearId == null) && x.EffectiveFrom <= date.Date)
			.OrderByDescending(x => x.AcademicYearId.HasValue).ThenByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.VersionNumber));
		var policy = version == null ? new AttendancePolicy() : AttendancePolicy.FromJson(version.PolicyJson);
		return new(policy, policy.Resolve(scope), version?.Id, version?.AcademicYearId, version == null, version?.VersionNumber ?? 0);
	}
}
