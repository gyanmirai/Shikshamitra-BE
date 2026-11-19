using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("stage")]
	[InlineData("medium")]
	[InlineData("board")]
	[InlineData("year")]
	public Task Onboarding_list_honors_disabled_soft_delete_filters_like_canonical_command_mapping(string ancestor) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false, emailEnabled: false);
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(fixture.Case.StudentId);
				if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
				{
					student.AssignGrNumber($"FILTER-{Guid.NewGuid():N}"[..24], null, "Existing permanent identifier");
					await _studentRepository.UpdateAsync(student, true);
				}
			});
			var warmed = await service.GetAsync(fixture.Case.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				switch (ancestor)
				{
					case "stage": await db.Set<EducationMediumStage>().Where(item => item.Id == warmed.IntendedStageId)
						.ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "medium": await db.Set<EducationMedium>().Where(item => item.Id == warmed.IntendedMediumId)
						.ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "board": await db.Set<EducationBoard>().Where(item => item.Id == warmed.IntendedBoardId)
						.ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
					case "year": await db.Set<AcademicYear>().Where(item => item.Id == warmed.AcademicYearId)
						.ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true)); break;
				}
			});
			using var deletedScope = GetRequiredService<IDataFilter>().Disable<ISoftDelete>();
			var expected = await service.AssignGrNumberAsync(warmed.Id, new() { ConcurrencyStamp = warmed.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid() });
			expected.IntendedBoardName.ShouldNotBeNullOrWhiteSpace();
			expected.IntendedMediumName.ShouldNotBeNullOrWhiteSpace();
			expected.IntendedStandardName.ShouldNotBeNullOrWhiteSpace();
			expected.Events = [];
			expected.CapturedStudentFields = [];
			expected.PortalAccountOwners = null;
			await WithUnitOfWorkAsync(async () =>
			{
				var actual = (await service.GetListAsync(new() { StandardId = warmed.IntendedStandardId, MaxResultCount = 10 }))
					.Items.Single(item => item.Id == warmed.Id);
				JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected),
					$"With soft-delete filtering disabled, deleted {ancestor} ancestors must retain exactly the canonical command mapper's labels and all list fields.");
			});
		});
}
