using System;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmAcademicYearScopeExtensions
{
	internal static async Task EnsureAcademicYearWideConfigurationAccessAsync(
		this StudentScopeAccessService scopeAccess,
		Guid academicYearId,
		string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (!await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId))
		{
			throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}
	}
}
