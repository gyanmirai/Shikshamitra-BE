using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_create_and_edit_normalize_known_email_snapshots() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await AddNormalizationEmailFieldAsync(offering.InquiryFormDefinitionId!.Value);
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering);
		input.FormValues.Add(new() { FieldCode = "QA_EMAIL", ValueJson = "\"Mixed+Create@Example.COM\"" });
		var created = await inquiries.CreateAsync(input);
		var persisted = await WithUnitOfWorkAsync(() => inquiries.GetAsync(created.Id));
		System.Text.Json.JsonSerializer.Deserialize<string>(persisted.FormValues.Single(value => value.FieldCode == "QA_EMAIL").ValueJson!)
			.ShouldBe("mixed+create@example.com");
		var update = InquiryStatusUpdate(persisted, AdmissionInquiryStatus.Interested);
		update.FormValues = persisted.FormValues;
		update.FormValues.Single(value => value.FieldCode == "QA_EMAIL").ValueJson = "\"Mixed+Edit@Example.COM\"";
		await inquiries.UpdateAsync(created.Id, update);
		var edited = await WithUnitOfWorkAsync(() => inquiries.GetAsync(created.Id));
		System.Text.Json.JsonSerializer.Deserialize<string>(edited.FormValues.Single(value => value.FieldCode == "QA_EMAIL").ValueJson!)
			.ShouldBe("mixed+edit@example.com");
		edited.FormValues.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME").ValueJson.ShouldBe("\"Aanya\"");
	});

	[Fact]
	public Task Registration_form_insert_and_update_normalize_known_email_snapshots() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await AddNormalizationEmailFieldAsync(offering.ApplicationFormDefinitionId!.Value);
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync("Email QA")).Id, offering.Id);
		foreach (var email in new[] { "Insert@Example.COM", "Updated@Example.COM" })
		{
			var current = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = current.ConcurrencyStamp,
				Values = [new() { FieldCode = "QA_EMAIL", ValueJson = System.Text.Json.JsonSerializer.Serialize(email) }]
			});
			var persisted = await WithUnitOfWorkAsync(() => _applicationAppService.GetFormAsync(application.Id));
			persisted.Values.Single(value => value.FieldCode == "QA_EMAIL").ValueJson
				.ShouldBe(System.Text.Json.JsonSerializer.Serialize(email.ToLowerInvariant()));
		}
	});

	private Task AddNormalizationEmailFieldAsync(Guid formId) => WithUnitOfWorkAsync(async () =>
	{
		var sections = GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>();
		var section = (await sections.GetListAsync(item => item.FormDefinitionId == formId)).First();
		await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(
			Guid.NewGuid(), formId, section.Id, "QA_EMAIL", "QA email", AdmissionFormFieldType.Email,
			AdmissionFormBindingType.Custom, null, false, false, 99, null, null, null, null, null), true);
	});
}
