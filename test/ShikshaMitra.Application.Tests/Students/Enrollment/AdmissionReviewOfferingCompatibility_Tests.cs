using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{

	private async Task SetReviewOfferingScopeAsync(Guid reviewer, AdmissionOfferingDto? offering)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.DeleteAsync(item => item.UserId == reviewer, autoSave: true);
			if (offering == null) return;
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer,
				academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), autoSave: true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(reviewer);
	}
}
