using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("boxed-grid")]
	[InlineData("structured-classic")]
	public Task Registration_print_settings_survive_create_get_update_clone_and_new_version(string template) => RunCertificationScenarioAsync(async () =>
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var input = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>("""
			{"name":"Printable Registration","printSettings":{"layout":"spacious","paperSize":"A4","showSchoolHeader":false,"showPhoto":true,"showSignature":false}}
			""", options)!;
		input.PrintSettings!.Template = template;
		var created = await _formAppService.CreateAsync(input);
		var loaded = await _formAppService.GetAsync(created.Id);
		var expected = JsonNode.Parse("""{"layout":"spacious","paperSize":"A4","showSchoolHeader":false,"showPhoto":true,"showSignature":false}""");
		expected!["template"] = template;
		var loadedSettings = JsonSerializer.SerializeToNode(loaded, options)!["printSettings"];
		loadedSettings.ShouldNotBeNull();
		JsonNode.DeepEquals(loadedSettings, expected).ShouldBeTrue();
		var legacyUpdate = JsonSerializer.SerializeToNode(loaded, options)!.AsObject();
		legacyUpdate.Remove("printSettings");
		legacyUpdate["name"] = "Printable Registration renamed";
		var updated = await _formAppService.UpdateAsync(created.Id, legacyUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(updated, options)!["printSettings"], expected).ShouldBeTrue();
		var oldClientUpdate = JsonSerializer.SerializeToNode(updated, options)!.AsObject();
		oldClientUpdate["printSettings"]!.AsObject().Remove("template");
		updated = await _formAppService.UpdateAsync(created.Id, oldClientUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(updated, options)!["printSettings"], expected).ShouldBeTrue();
		var invalidUpdate = JsonSerializer.SerializeToNode(updated, options)!.AsObject();
		invalidUpdate["name"] = "Must not mutate the saved form";
		invalidUpdate["printSettings"]!["template"] = "unsupported";
		(await Should.ThrowAsync<BusinessException>(() => _formAppService.UpdateAsync(created.Id,
			invalidUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!))).Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		(await _formAppService.GetAsync(created.Id)).Name.ShouldBe(updated.Name);
		var clone = await _formAppService.CloneAsync(created.Id, new() { Name = "Printable clone" });
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(clone, options)!["printSettings"], expected).ShouldBeTrue();
		var published = await _formAppService.PublishAsync(updated.Id, updated.ConcurrencyStamp);
		var next = await _formAppService.CreateNewVersionAsync(published.Id);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(next, options)!["printSettings"], expected).ShouldBeTrue();
		(await _formAppService.CompareVersionsAsync(published.Id, next.Id)).Changes.ShouldBeEmpty();
	});

	[Fact]
	public Task Registration_print_read_exposes_the_existing_application_number_after_reopen() => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		offering = await WithUnitOfWorkAsync(async () =>
		{
			var current = await _offeringRepository.GetAsync(offering.Id);
			current.ConfigureReferences(workflow.Id, current.InquiryFormDefinitionId, formId);
			return await _offeringRepository.UpdateAsync(current, true);
		});
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var inquiry = await inquiries.CreateAsync(PrimaryContactInquiryInput(offering, "father"));
		inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		application.ApplicationNumber.ShouldNotBeNullOrWhiteSpace();
		(await _applicationAppService.GetFormAsync(application.Id)).ApplicationNumber.ShouldBe(application.ApplicationNumber);
		var reopened = await _applicationAppService.GetFormAsync(application.Id);
		reopened.ApplicationNumber.ShouldBe(application.ApplicationNumber);
		(await _applicationAppService.GetAsync(application.Id)).ApplicationNumber.ShouldBe(application.ApplicationNumber);
	});
}
