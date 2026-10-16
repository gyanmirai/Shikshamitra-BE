using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private async Task EnsureSettingsTargetScopeAsync(Guid academicYearId, Guid? standardId, string operation)
	{
		var access = LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>();
		if (standardId.HasValue)
		{
			// Class-wide configuration affects every division and must belong to the supplied year.
			await access.EnsureCanAccessPlacementAsync(academicYearId, standardId.Value, null, operation);
			return;
		}

		await access.EnsureSettingsScopeAsync(academicYearId, writing: true, operation);
	}

	private async Task<Guid[]?> GetReadableSettingsStandardIdsAsync(Guid academicYearId, string operation)
	{
		var access = LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>();
		await access.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		if (scope.HasAcademicYearWideScope(academicYearId)) return null;

		var divisions = await access.ApplyCurrentUserDivisionScopeAsync(await divisionRepository.GetQueryableAsync(), academicYearId);
		var standards = await standardRepository.GetQueryableAsync();
		var stages = await mediumStageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		// A division-scoped reader may inspect inherited class configuration, but not sibling classes.
		var query = from standard in standards
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where board.AcademicYearId == academicYearId && standard.TenantId == CurrentTenant.Id &&
				stage.TenantId == CurrentTenant.Id && medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id &&
				(scope.StandardIds.Contains(standard.Id) || scope.WildcardDivisionStandardIds.Contains(standard.Id) ||
				 scope.WildcardStandardMediumStageIds.Contains(stage.Id) || scope.WildcardMediumStageMediumIds.Contains(medium.Id) ||
				 scope.WildcardMediumBoardIds.Contains(board.Id) || divisions.Any(division => division.StandardId == standard.Id))
			select standard.Id;
		return (await AsyncExecuter.ToListAsync(query)).ToArray();
	}
}
