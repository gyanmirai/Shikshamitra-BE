using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.Students.Setup;

public partial class StudentSetupGuideApi_Tests
{
	[Fact]
	public async Task Published_stages_and_active_links_follow_real_selected_class_offerings()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school); using var client = As(school);
		await InAsync(school.TenantId, async services => {
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(school.MediumId);
			var period = new AdmissionCycle(Guid.NewGuid(), school.TenantId, "Intake", "INTAKE", school.YearId, new(2026, 1, 1), new(2027, 1, 1));
			period.Publish(school.ActorId, DateTime.UtcNow);
			await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(period, true);
			var form = new AdmissionFormDefinition(Guid.NewGuid(), school.TenantId, "Inquiry", "INQUIRY", AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
			form.Publish(school.ActorId);
			await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, true);
			var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), school.TenantId, "Journey", "JOURNEY");
			workflow.Publish(school.ActorId);
			await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
			var offering = new AdmissionOffering(Guid.NewGuid(), school.TenantId, period.Id, null, medium.EducationBoardId, school.MediumId, classes.Stage, classes.First, "CLASS-1", true, null, null);
			offering.ConfigureReferences(workflow.Id, form.Id, null);
			await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering, true);
			await services.GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().InsertAsync(new(Guid.NewGuid(), school.TenantId, period.Id, null, "Public intake", new string('a', 64), AdmissionInquirySource.Website, null, DateTime.UtcNow.AddDays(1), form.Id), true);
		});
		var configured = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.First}");
		foreach (var key in new[] { "admission-stages", "admission-offerings", "public-inquiry-links" })
			Setting(configured, key).GetProperty("configurationState").GetString().ShouldBe("configured");
		var otherClass = await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.Second}");
		Setting(otherClass, "admission-offerings").GetProperty("configurationState").GetString().ShouldBe("empty");
		Setting(otherClass, "public-inquiry-links").GetProperty("configurationState").GetString().ShouldBe("empty");
	}

	[Fact]
	public async Task Draft_and_archived_admission_configuration_do_not_count_and_periods_follow_selected_year()
	{
		var school = await SeedAsync(); var anotherYear = await AddYearAsync(school); using var client = As(school);
		await InAsync(school.TenantId, async services => {
			var period = new AdmissionCycle(Guid.NewGuid(), school.TenantId, "Intake", "INTAKE", school.YearId, new(2026, 1, 1), new(2027, 1, 1));
			await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(period, true);
			await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), school.TenantId, "Draft", "DRAFT", AdmissionFormLayoutType.SinglePage), true);
			var archived = new AdmissionFormDefinition(Guid.NewGuid(), school.TenantId, "Archived", "ARCHIVED", AdmissionFormLayoutType.SinglePage);
			archived.Publish(school.ActorId); archived.Archive();
			await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(archived, true);
		});
		var empty = await ReadAsync(client, StateUrl(school));
		Setting(empty, "admission-periods").GetProperty("configurationState").GetString().ShouldBe("empty");
		Setting(empty, "admission-forms").GetProperty("configurationState").GetString().ShouldBe("empty");
		await InAsync(school.TenantId, async services => {
			var periods = services.GetRequiredService<IRepository<AdmissionCycle, Guid>>();
			var period = (await periods.GetListAsync()).Single(); period.Publish(school.ActorId, DateTime.UtcNow); await periods.UpdateAsync(period, true);
			var forms = services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
			var form = (await forms.GetListAsync()).Single(x => x.Status == AdmissionFormStatus.Draft); form.Publish(school.ActorId); await forms.UpdateAsync(form, true);
		});
		var configured = await ReadAsync(client, StateUrl(school));
		Setting(configured, "admission-periods").GetProperty("configurationState").GetString().ShouldBe("configured");
		Setting(configured, "admission-forms").GetProperty("configurationState").GetString().ShouldBe("configured");
		Setting(await ReadAsync(client, StateUrl(school with { YearId = anotherYear })), "admission-periods").GetProperty("configurationState").GetString().ShouldBe("empty");
	}

	[Fact]
	public async Task Effective_records_policy_reports_published_school_and_year_inheritance()
	{
		var school = await SeedAsync(); using var client = As(school);
		async Task PublishAsync(Guid? yearId) => await InAsync(school.TenantId, async services => {
			var policy = ShikshaMitra.Students.StudentRecords.StudentRecordsPolicy.Starter().ToJson();
			var configuration = new ShikshaMitra.Students.StudentRecords.StudentRecordsConfiguration(Guid.NewGuid(), school.TenantId, yearId, policy);
			var version = new ShikshaMitra.Students.StudentRecords.StudentRecordsPolicyVersion(Guid.NewGuid(), school.TenantId, configuration.Id, yearId, 1, policy);
			configuration.Publish(version.Id);
			await services.GetRequiredService<IRepository<ShikshaMitra.Students.StudentRecords.StudentRecordsConfiguration, Guid>>().InsertAsync(configuration, true);
			await services.GetRequiredService<IRepository<ShikshaMitra.Students.StudentRecords.StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, true);
		});
		await PublishAsync(null);
		Setting(await ReadAsync(client, StateUrl(school)), "records-policy").GetProperty("configurationSource").GetString().ShouldBe("school");
		await PublishAsync(school.YearId);
		Setting(await ReadAsync(client, StateUrl(school)), "records-policy").GetProperty("configurationSource").GetString().ShouldBe("academic-year");
	}

	[Fact]
	public async Task Starter_policy_is_configured_and_numbering_requires_published_selected_class_coverage()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school); using var client = As(school);
		var initial = await ReadAsync(client, StateUrl(school));
		Setting(initial, "records-policy").GetProperty("configurationState").GetString().ShouldBe("configured");
		Setting(initial, "records-policy").GetProperty("configurationSource").GetString().ShouldBe("school-default");
		Setting(initial, "gr-number").GetProperty("configurationState").GetString().ShouldBe("context-required");
		var url = StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.First}";
		Setting(await ReadAsync(client, url), "gr-number").GetProperty("configurationState").GetString().ShouldBe("empty");
		await InAsync(school.TenantId, async services => {
			var rule = new { id = "school-rule", isActive = true, boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "selected", standardIds = new[] { classes.First }, divisionMode = "all", padding = 4, startingNumber = 1 };
			var json = System.Text.Json.JsonSerializer.Serialize(new { readinessAccess = new { }, onboardingProfiles = new object[] { }, grNumber = new { configurations = new[] { rule } }, rollNumber = new { configurations = new[] { rule } }, divisionChange = new { configurations = new object[] { } } });
			await services.GetRequiredService<IRepository<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingSettings, Guid>>().InsertAsync(new(Guid.NewGuid(), school.TenantId, null, ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingSettingsConsts.CurrentSchemaVersion, json), true);
		});
		var configured = await ReadAsync(client, url);
		foreach (var key in new[] { "gr-number", "roll-number" }) {
			Setting(configured, key).GetProperty("configurationState").GetString().ShouldBe("configured");
			Setting(configured, key).GetProperty("configurationSource").GetString().ShouldBe("school");
			Setting(await ReadAsync(client, StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.Second}"), key).GetProperty("configurationState").GetString().ShouldBe("empty");
		}
	}

	[Fact]
	public async Task Class_scope_allows_its_inherited_medium_settings_but_rejects_sibling_classes()
	{
		var school = await SeedAsync(); var classes = await AddClassesAsync(school);
		await InAsync(school.TenantId, async services => {
			var scope = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scope.DeleteAsync(x => x.UserId == school.ActorId, true);
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(school.MediumId);
			await scope.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), school.TenantId, userId: school.ActorId, academicYearId: school.YearId,
				educationBoardId: medium.EducationBoardId, educationMediumId: school.MediumId, educationMediumStageId: classes.Stage, standardId: classes.First), true);
		});
		using var client = As(school);
		(await client.GetAsync(StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.First}")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await client.GetAsync(StateUrl(school) + $"?academicMediumId={school.MediumId}&standardId={classes.Second}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await client.GetAsync(StateUrl(school) + $"?standardId={classes.First}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private async Task<(Guid Stage, Guid First, Guid Second)> AddClassesAsync(School school)
	{
		(Guid, Guid, Guid) result = default;
		await InAsync(school.TenantId, async services => {
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(school.MediumId, "Primary");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var manager = services.GetRequiredService<StandardManager>();
			var first = await manager.CreateAsync(stage.Id, "Class 1", 5, null);
			var second = await manager.CreateAsync(stage.Id, "Class 2", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(first, true);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(second, true);
			result = (stage.Id, first.Id, second.Id);
		}); return result;
	}
}
