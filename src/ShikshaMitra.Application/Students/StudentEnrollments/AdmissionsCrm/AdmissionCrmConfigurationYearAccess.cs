using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationYearAccess(
	StudentScopeAccessService scopeAccess,
	AdmissionCrmConfigurationSnapshotReader snapshotReader) : ITransientDependency
{
	public async Task<AcademicYear> EnsureAsync(Guid academicYearId, string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (!await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId))
			throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired")
				.WithData("AcademicYearId", academicYearId).WithData("Operation", operation);
		return await snapshotReader.GetAcademicYearAsync(academicYearId);
	}
}
