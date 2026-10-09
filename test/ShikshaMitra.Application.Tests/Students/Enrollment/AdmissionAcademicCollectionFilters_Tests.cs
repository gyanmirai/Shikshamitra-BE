using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Academic_collection_filters_apply_before_admissions_counts_and_paging() =>
		RunCertificationScenarioAsync(async () =>
		{
			var first = await CreateAcademicFilterFixtureAsync();
			var second = await CreateAcademicFilterFixtureAsync();
			var excluded = await CreateAcademicFilterFixtureAsync();
			var firstClass = first.Offering.StandardId;
			var secondClass = second.Offering.StandardId;
			var classes = new[] { firstClass, secondClass }.ToList();
			var applications = await _applicationAppService.GetListAsync(new()
			{
				BoardId = first.Offering.BoardId, MediumId = first.Offering.MediumId,
				StandardIds = classes, MaxResultCount = 1, SkipCount = 1
			});
			applications.TotalCount.ShouldBe(2);
			applications.Items.Count.ShouldBe(1);
			applications.Items.Single().Id.ShouldNotBe(excluded.ApplicationId);
			var inquiries = await GetRequiredService<IAdmissionInquiryAppService>().GetListAsync(new()
			{
				StandardIds = classes, MaxResultCount = 1, SkipCount = 1
			});
			inquiries.TotalCount.ShouldBe(2);
			inquiries.Items.Count.ShouldBe(1);
			var offerings = await GetRequiredService<IAdmissionOfferingAppService>().GetListAsync(new()
			{
				StandardIds = classes, MaxResultCount = 1, SkipCount = 1
			});
			offerings.TotalCount.ShouldBe(2);
			offerings.Items.Single().Id.ShouldNotBe(excluded.Offering.Id);
			var pipeline = await _applicationAppService.GetPipelineAsync(new()
			{
				StandardIds = classes, MyCases = false, MaxResultCount = 1, SkipCount = 1
			});
			pipeline.TotalCount.ShouldBe(2);
			pipeline.Items.Single().Id.ShouldNotBe(excluded.ApplicationId);
			(await _applicationAppService.GetListAsync(new() { StandardId = firstClass })).TotalCount.ShouldBe(1);
			(await _applicationAppService.GetListAsync(new()
			{
				StandardIds = classes, BoardId = Guid.NewGuid()
			})).TotalCount.ShouldBe(0);
		});

	[Fact]
	public Task Academic_collection_filters_apply_before_onboarding_counts_and_paging() =>
		RunCertificationScenarioAsync(async () =>
		{
			var cases = new StudentOnboardingCase[3];
			var divisions = new Guid[2];
			for (var index = 0; index < cases.Length; index++)
			{
				var fixture = await CreateAcademicFilterFixtureAsync();
				var student = await CreateExistingStudentAsync(fixture.Offering, $"AcademicFilter{index}", "Patel");
				var classId = fixture.Offering.StandardId;
				cases[index] = await WithUnitOfWorkAsync(async () =>
				{
					var cycle = await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId);
					var item = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId,
						fixture.ApplicationId, fixture.ApplicantId, student.Id, fixture.Offering.Id,
						cycle.AcademicYearId, fixture.Offering.BoardId, fixture.Offering.MediumId,
						fixture.Offering.StageId, classId, $"academic-filter-{index}", null, DateTime.UtcNow.AddDays(7));
					if (index < 2)
					{
						var division = await GetRequiredService<DivisionManager>().CreateAsync(classId, "A", 40);
						await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
						divisions[index] = division.Id;
						item.RecordPlacement(division.Id, Guid.NewGuid(), DateTime.UtcNow);
					}
					await _onboardingCaseRepository.InsertAsync(item, true);
					return item;
				});
			}
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var classes = cases.Take(2).Select(item => item.IntendedStandardId).ToList();
			var page = await service.GetListAsync(new()
			{
				BoardId = cases[0].IntendedBoardId, MediumId = cases[0].IntendedMediumId,
				StandardIds = classes, DivisionIds = divisions.ToList(), MaxResultCount = 1, SkipCount = 1
			});
			page.TotalCount.ShouldBe(2);
			page.Items.Single().Id.ShouldNotBe(cases[2].Id);
			(await service.GetListAsync(new() { StandardId = classes[0], DivisionId = divisions[0] }))
				.TotalCount.ShouldBe(1);
			(await service.GetListAsync(new() { StandardIds = classes, DivisionId = Guid.NewGuid() }))
				.TotalCount.ShouldBe(0);
			(await service.GetListAsync(new() { StandardIds = classes, MediumId = Guid.NewGuid() }))
				.TotalCount.ShouldBe(0);
		});

	// Seed list read models directly: these tests exercise academic filtering, not admission commands.
	private async Task<AcademicFilterFixture> CreateAcademicFilterFixtureAsync()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId,
			"Academic filter workflow", $"FILTER_{Guid.NewGuid():N}"[..24]);
		workflow.Publish(TestUserId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>()
			.InsertAsync(workflow, true));
		var offering = await CreateAvailableOfferingAsync(workflow.Id, assertValid: false);
		var applicant = await CreateApplicantAsync("AcademicFilter");
		return await WithUnitOfWorkAsync(async () =>
		{
			var standard = await _standardManager.CreateAsync(offering.StageId, $"Filter {Guid.NewGuid():N}"[..30], 12, null);
			await _standardRepository.InsertAsync(standard, true);
			var storedOffering = await _offeringRepository.GetAsync(offering.Id);
			storedOffering.UpdateIdentity(offering.CampusId, offering.BoardId, offering.MediumId,
				offering.StageId, standard.Id, identityLocked: false);
			await _offeringRepository.UpdateAsync(storedOffering, true);
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"FILTER-{Guid.NewGuid():N}",
				offering.AdmissionCycleId, offering.Id, "AcademicFilter Patel", "9876543210", null,
				AdmissionInquirySource.Website, null);
			inquiry.LinkIdentity(applicant.FamilyId, applicant.Id);
			inquiry.SetStatus(AdmissionInquiryStatus.ApplicationStarted);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
			var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"FILTER-{Guid.NewGuid():N}",
				applicant.Id, inquiry.Id, offering.AdmissionCycleId, offering.Id, workflow.Id,
				offering.ApplicationFormDefinitionId!.Value);
			var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "FORM",
				"Application form", AdmissionWorkflowStageType.Form, 0, true, false, null, null, true, true);
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(definition, true);
			await _applicationRepository.InsertAsync(application, true);
			var runtime = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), TestTenantId, application.Id, workflow.Id);
			await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().InsertAsync(runtime, true);
			var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, runtime.Id,
				definition.Id, "FORM", "Application form", AdmissionWorkflowStageType.Form, 0, true);
			await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage, true);
			application.SetCurrentStage(stage.Id);
			await _applicationRepository.UpdateAsync(application, true);
			return new AcademicFilterFixture(storedOffering, applicant.Id, application.Id);
		});
	}

	private sealed record AcademicFilterFixture(AdmissionOffering Offering, Guid ApplicantId, Guid ApplicationId);
}
