using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_placement_sort_pages_by_canonical_visible_class_labels_after_parent_soft_delete() =>
		RunCertificationScenarioAsync(async () =>
		{
			var service = GetRequiredService<IStudentOnboardingAppService>();
			async Task<(StudentOnboardingCaseDto Item, Guid StageId, Guid StandardId)> PrepareAsync(string name, string className)
			{
				var stageId = await WithUnitOfWorkAsync(async () =>
				{
					var templateId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
					var template = await _stageRepository.GetAsync(templateId);
					var stage = await GetRequiredService<EducationMediumStageManager>().CreateAsync(template.EducationMediumId, $"Placement sort {name}");
					return (await _stageRepository.InsertAsync(stage, true)).Id;
				});
				var fixture = await CreateCertificationFixtureAsync(name, academicStageId: stageId);
				var finalized = await FinalizeAsync(fixture.Application, true, $"placement-sort-{name}");
				await WithUnitOfWorkAsync(async () =>
				{
					var standard = await _standardRepository.GetAsync(fixture.Offering.StandardId);
					standard.Name = className;
					await _standardRepository.UpdateAsync(standard, true);
					var student = await _studentRepository.GetAsync(finalized.StudentId);
					student.AssignGrNumber($"SORT-{student.Id:N}"[..24], null, "Existing permanent identifier");
					await _studentRepository.UpdateAsync(student, true);
				});
				var item = await service.GetAsync(finalized.OnboardingCaseId);
				item.IntendedStageId.ShouldBe(stageId);
				item.IntendedStandardId.ShouldBe(fixture.Offering.StandardId);
				return (item, stageId, fixture.Offering.StandardId);
			}
			var visible = await PrepareAsync("PlacementVisible", "AA visible class");
			var hidden = await PrepareAsync("PlacementHidden", "ZZ hidden class");
			visible.StageId.ShouldNotBe(hidden.StageId);
			visible.StandardId.ShouldNotBe(hidden.StandardId);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await db.Set<EducationMediumStage>().Where(stage => stage.Id == hidden.StageId)
					.ExecuteUpdateAsync(update => update.SetProperty(stage => stage.IsDeleted, true));
			});
			var visibleCanonical = await service.AssignGrNumberAsync(visible.Item.Id, new() { ConcurrencyStamp = visible.Item.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid() });
			var hiddenCanonical = await service.AssignGrNumberAsync(hidden.Item.Id, new() { ConcurrencyStamp = hidden.Item.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid() });
			visibleCanonical.IntendedStandardName.ShouldBe("AA visible class");
			hiddenCanonical.IntendedStandardName.ShouldBe(string.Empty);
			var expectedFirst = new[] { visibleCanonical, hiddenCanonical }.OrderBy(item => item.IntendedStandardName)
				.ThenBy(item => item.DivisionName).ThenBy(item => item.Id).First();
			expectedFirst.Id.ShouldBe(hidden.Item.Id);
			var page = await service.GetListAsync(new() { Sorting = "Placement asc", MaxResultCount = 1 });
			page.TotalCount.ShouldBe(2);
			page.Items.Single().Id.ShouldBe(expectedFirst.Id,
				"Placement sorting must use the same required-parent class visibility as the canonical DTO before applying the page boundary.");
		});
}
