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
		const string advanced = """
			{"paper":{"orientation":"landscape"},"header":{"title":"Saved print title","layout":{"schoolName":{"x":12.5,"y":4,"width":60,"height":6},"photo":{"x":80,"y":10,"width":20,"height":90,"lockAspectRatio":true}}},"sections":{"DETAILS":{"columns":2}},"content":{"translations":{"en":{"declaration":"Saved declaration"}}}}
			""";
		var input = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>("""
			{"name":"Printable Registration","printSettings":{"layout":"spacious","paperSize":"A4","showSchoolHeader":false,"showPhoto":true,"showSignature":false}}
			""", options)!;
		input.PrintSettings!.Template = template;
		input.PrintSettings.Advanced = JsonSerializer.Deserialize<AdmissionFormPrintAdvancedSettingsDto>(advanced, options);
		var created = await _formAppService.CreateAsync(input);
		var loaded = await _formAppService.GetAsync(created.Id);
		var expected = JsonNode.Parse("""{"layout":"spacious","paperSize":"A4","showSchoolHeader":false,"showPhoto":true,"showSignature":false}""");
		expected!["template"] = template;
		expected["advanced"] = JsonNode.Parse(advanced);
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
		oldClientUpdate["printSettings"]!.AsObject().Remove("advanced");
		updated = await _formAppService.UpdateAsync(created.Id, oldClientUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(updated, options)!["printSettings"], expected).ShouldBeTrue();
		var invalidUpdate = JsonSerializer.SerializeToNode(updated, options)!.AsObject();
		invalidUpdate["name"] = "Must not mutate the saved form";
		invalidUpdate["printSettings"]!["template"] = "unsupported";
		(await Should.ThrowAsync<BusinessException>(() => _formAppService.UpdateAsync(created.Id,
			invalidUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!))).Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		(await _formAppService.GetAsync(created.Id)).Name.ShouldBe(updated.Name);
		invalidUpdate["printSettings"]!["template"] = template;
		invalidUpdate["printSettings"]!["advanced"]!["style"] = new JsonObject { ["borderColor"] = "invalid-colour" };
		(await Should.ThrowAsync<BusinessException>(() => _formAppService.UpdateAsync(created.Id,
			invalidUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!))).Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		(await _formAppService.GetAsync(created.Id)).Name.ShouldBe(updated.Name);
		var clone = await _formAppService.CloneAsync(created.Id, new() { Name = "Printable clone" });
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(clone, options)!["printSettings"], expected).ShouldBeTrue();
		var published = await _formAppService.PublishAsync(updated.Id, updated.ConcurrencyStamp);
		var next = await _formAppService.CreateNewVersionAsync(published.Id);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(next, options)!["printSettings"], expected).ShouldBeTrue();
		(await _formAppService.CompareVersionsAsync(published.Id, next.Id)).Changes.ShouldBeEmpty();
		(await _formAppService.ValidateAsync(next.Id)).Errors.ShouldContain($"{AdmissionFormErrorCodes.VersionUnchanged}:{published.Version}");
		var newVersionUpdate = JsonSerializer.SerializeToNode(next, options)!;
		newVersionUpdate["printSettings"]!["advanced"]!["header"]!["layout"]!["schoolName"]!["x"] = 18;
		next = await _formAppService.UpdateAsync(next.Id, newVersionUpdate.Deserialize<CreateUpdateAdmissionFormDto>(options)!);
		(await _formAppService.CompareVersionsAsync(published.Id, next.Id)).Changes.ShouldBe(["Print settings changed"]);
		next = await _formAppService.PublishAsync(next.Id, next.ConcurrencyStamp);
		next.Status.ShouldBe(AdmissionFormStatus.Published);
		var currentPublished = await _formAppService.GetAsync(published.Id);
		JsonNode.DeepEquals(JsonSerializer.SerializeToNode(currentPublished, options)!["printSettings"], expected).ShouldBeTrue();
	});

	[Fact]
	public Task Registration_print_read_exposes_the_existing_application_number_after_reopen() => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var definition = await _formRepository.GetAsync(formId);
			AdmissionFormMetadataStore.WriteExperience(definition, JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>("""
				{"printSettings":{"advanced":{"header":{"board":true,"medium":true,"class":true,"academicYear":true}}}}
				""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
			await _formRepository.UpdateAsync(definition, true);
		});
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
		var academic = JsonSerializer.SerializeToNode(reopened, new JsonSerializerOptions(JsonSerializerDefaults.Web))!["printAcademicDetails"];
		academic.ShouldNotBeNull();
		academic!["board"]!.GetValue<string>().ShouldBe((await _boardRepository.GetAsync(offering.BoardId)).Type.ToString());
		academic["medium"]!.GetValue<string>().ShouldBe((await _mediumRepository.GetAsync(offering.MediumId)).Name);
		academic["class"]!.GetValue<string>().ShouldBe((await _standardRepository.GetAsync(offering.StandardId)).Name);
		var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var year = await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.TenantSettings.AcademicYears.AcademicYear, System.Guid>>().GetAsync(cycle.AcademicYearId);
		academic["academicYear"]!.GetValue<string>().ShouldBe(year.Name);
		(await _applicationAppService.GetAsync(application.Id)).ApplicationNumber.ShouldBe(application.ApplicationNumber);
	});
}
