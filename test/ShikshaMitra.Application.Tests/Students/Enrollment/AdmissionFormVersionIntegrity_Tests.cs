using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionFormPurpose.Inquiry)]
	[InlineData(AdmissionFormPurpose.Registration)]
	public Task Unchanged_form_versions_can_be_saved_but_only_a_change_from_the_latest_can_publish(AdmissionFormPurpose purpose) =>
		RunCertificationScenarioAsync(async () =>
		{
			var first = await _formAppService.CreateAsync(new() { Name = $"{purpose} version publication", Purpose = purpose });
			first = await _formAppService.PublishAsync(first.Id, first.ConcurrencyStamp);
			var draft = await _formAppService.CreateNewVersionAsync(first.Id);
			var input = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(draft))!;
			draft = await _formAppService.UpdateAsync(draft.Id, input);
			(await _formAppService.ValidateAsync(draft.Id)).Errors.ShouldBe(["Admissions.FormVersionUnchanged:1"]);
			var failure = await Should.ThrowAsync<BusinessException>(() => _formAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp));
			failure.Code.ShouldBe("Admissions.FormVersionUnchanged");
			failure.Data["Version"].ShouldBe(1);
			var retained = await _formAppService.GetAsync(draft.Id);
			retained.Status.ShouldBe(AdmissionFormStatus.Draft);
			retained.ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);

			input.ConcurrencyStamp = retained.ConcurrencyStamp;
			if (purpose == AdmissionFormPurpose.Inquiry)
				input.Sections.SelectMany(section => section.Fields).First().Label = "Student given name";
			else
				input.PrintSettings!.ShowSignature = !input.PrintSettings.ShowSignature;
			draft = await _formAppService.UpdateAsync(draft.Id, input);
			var second = await _formAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
			second.Version.ShouldBe(2);

			// History restores an older definition as a draft with a new version number.
			var rollback = await _formAppService.CreateNewVersionAsync(first.Id);
			rollback.ParentVersionId.ShouldBe(first.Id);
			(await _formAppService.CompareVersionsAsync(first.Id, rollback.Id)).Changes.ShouldBeEmpty();
			(await _formAppService.ValidateAsync(rollback.Id)).IsValid.ShouldBeTrue();
			rollback = await _formAppService.PublishAsync(rollback.Id, rollback.ConcurrencyStamp);
			rollback.Version.ShouldBe(3);
			rollback.Status.ShouldBe(AdmissionFormStatus.Published);
			(await _formAppService.GetAsync(first.Id)).Status.ShouldBe(AdmissionFormStatus.Published);
			(await _formAppService.GetAsync(second.Id)).Status.ShouldBe(AdmissionFormStatus.Published);

			var fromFirst = await _formAppService.CreateNewVersionAsync(first.Id);
			(await _formAppService.ValidateAsync(fromFirst.Id)).Errors.ShouldBe(["Admissions.FormVersionUnchanged:3"]);
			var archived = await _formAppService.ArchiveAsync(rollback.Id, rollback.ConcurrencyStamp);
			(await _formAppService.ValidateAsync(fromFirst.Id)).IsValid.ShouldBeTrue();
			var fourth = await _formAppService.PublishAsync(fromFirst.Id, fromFirst.ConcurrencyStamp);
			fourth.Version.ShouldBe(4);
			var restored = await _formAppService.RestoreAsync(archived.Id, archived.ConcurrencyStamp);
			restored.Id.ShouldBe(rollback.Id);
			restored.Status.ShouldBe(AdmissionFormStatus.Published);
		});

	[Fact]
	public Task Form_version_conflicts_identify_the_existing_draft_without_rewriting_history() => RunCertificationScenarioAsync(async () =>
	{
		var source = await _formAppService.CreateAsync(new() { Name = "Version integrity Inquiry", Purpose = AdmissionFormPurpose.Inquiry });
		source = await _formAppService.PublishAsync(source.Id, source.ConcurrencyStamp);
		var draft = await _formAppService.CreateNewVersionAsync(source.Id);
		var conflict = await Should.ThrowAsync<BusinessException>(() => _formAppService.CreateNewVersionAsync(source.Id));
		conflict.Code.ShouldBe("Admissions.FormDraftAlreadyExists");
		conflict.Data["DraftId"].ShouldBe(draft.Id);
		var invalidSource = await Should.ThrowAsync<BusinessException>(() => _formAppService.CreateNewVersionAsync(draft.Id));
		invalidSource.Code.ShouldBe("Admissions.FormVersionSourceRequired");
		var versions = await _formAppService.GetVersionsAsync(source.Id);
		versions.Count.ShouldBe(2);
		versions.Single(item => item.Id == source.Id).Status.ShouldBe(AdmissionFormStatus.Published);
		(await _formAppService.CompareVersionsAsync(source.Id, draft.Id)).Changes.ShouldBeEmpty();
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Form_version_cannot_leave_its_original_family_when_an_api_changes_its_code(bool legacyWithoutParent) => RunCertificationScenarioAsync(async () =>
	{
		var source = await _formAppService.CreateAsync(new() { Name = "Stable Inquiry family", Purpose = AdmissionFormPurpose.Inquiry });
		source = await _formAppService.PublishAsync(source.Id, source.ConcurrencyStamp);
		AdmissionFormDto draft;
		if (legacyWithoutParent)
		{
			var legacy = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, source.Name, source.Code,
				source.LayoutType, version: 2, purpose: source.Purpose);
			await WithUnitOfWorkAsync(() => _formRepository.InsertAsync(legacy, autoSave: true));
			draft = await _formAppService.GetAsync(legacy.Id);
		}
		else draft = await _formAppService.CreateNewVersionAsync(source.Id);
		var update = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(draft))!;
		update.Code = $"DETACHED_{Guid.NewGuid():N}";
		var failure = await Should.ThrowAsync<BusinessException>(() => _formAppService.UpdateAsync(draft.Id, update));
		failure.Code.ShouldBe("Admissions.FormVersionCodeImmutable");
		var retained = await _formAppService.GetAsync(draft.Id);
		retained.Code.ShouldBe(source.Code);
		retained.ParentVersionId.ShouldBe(legacyWithoutParent ? null : source.Id);
		retained.Version.ShouldBe(2);
		JsonSerializer.Serialize((await _formAppService.GetAsync(source.Id)).Sections).ShouldBe(JsonSerializer.Serialize(source.Sections));
	});
}
