using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Academic_form_choices_use_Institute_Setup_IDs_and_parent_membership()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Dynamic form academic options"), autoSave: true);
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var standard = await _standardRepository.GetAsync(offering.StandardId);
			var division = new Division(Guid.NewGuid(), TestTenantId, "Lotus", standard.Id, 30, "");
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, autoSave: true);

			var classes = await _formAppService.GetOptionsAsync("Standards", academicYearId: cycle.AcademicYearId);
			classes.ShouldContain(option => option.Value == standard.Id.ToString() && option.Label == standard.Name);
			using var configuredClass = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(
				classes.Single(option => option.Value == standard.Id.ToString())));
			configuredClass.RootElement.GetProperty("BoardId").GetGuid().ShouldBe(offering.BoardId);
			configuredClass.RootElement.GetProperty("BoardLabel").GetString().ShouldNotBeNullOrWhiteSpace();
			(await _formAppService.GetOptionsAsync("StandardsByMedium", offering.MediumId, cycle.AcademicYearId))
				.ShouldContain(option => option.Value == standard.Id.ToString() && option.BoardId == null);
			(await _formAppService.GetOptionsAsync("StandardsByMedium", Guid.NewGuid(), cycle.AcademicYearId)).ShouldBeEmpty();
			(await _formAppService.GetOptionsAsync("Standards", academicYearId: Guid.NewGuid())).ShouldBeEmpty();
			(await _formAppService.GetOptionsAsync("Standards")).ShouldBeEmpty();
			var divisions = await _formAppService.GetOptionsAsync("DivisionsByStandard", standard.Id, cycle.AcademicYearId);
			divisions.ShouldContain(option => option.Value == division.Id.ToString() && option.Label == "Lotus");
			(await _formAppService.GetOptionsAsync("DivisionsByStandard", Guid.NewGuid(), cycle.AcademicYearId)).ShouldBeEmpty();
			(await _formAppService.GetOptionsAsync("DivisionsByStandard", academicYearId: cycle.AcademicYearId)).ShouldBeEmpty();
			(await _formAppService.GetDataSourcesAsync()).Select(source => source.Key)
				.ShouldContain("DivisionsByStandard");

			await WithUnitOfWorkAsync(async () =>
			{
			var publicOptions = GetRequiredService<AdmissionInquiryAcademicOptionsService>();
			var publicClasses = await publicOptions.ForPublicOfferingAsync("Standards", offering);
			publicClasses.Count.ShouldBe(1);
			publicClasses.ShouldContain(option => option.Value == standard.Id.ToString() && option.Label == standard.Name && option.BoardId == offering.BoardId);
			publicClasses[0].BoardLabel.ShouldNotBeNullOrWhiteSpace();
			(await publicOptions.ForPublicOfferingAsync("DivisionsByStandard", offering, Guid.NewGuid())).ShouldBeEmpty();
			(await publicOptions.ForPublicOfferingAsync("DivisionsByStandard", offering, standard.Id))
				.ShouldContain(option => option.Value == division.Id.ToString());
			var legacyField = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
				"CLASS_INTERESTED", "Class interested in", AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom,
				"CLASS_INTERESTED", true, false, 0, null, null, null, null, null, "[\"Class 1\",\"Class 2\"]");
			await publicOptions.ValidateAsync([legacyField], new System.Collections.Generic.Dictionary<string, string>
				{ [legacyField.Code] = standard.Id.ToString() }, offering, cycle.AcademicYearId, true);
			await Should.ThrowAsync<BusinessException>(() => publicOptions.ValidateAsync([legacyField],
				new System.Collections.Generic.Dictionary<string, string> { [legacyField.Code] = "Class 1" }, offering, cycle.AcademicYearId, true));
			legacyField.OptionsJson.ShouldBe("[\"Class 1\",\"Class 2\"]");
			});
		}
	}
}
