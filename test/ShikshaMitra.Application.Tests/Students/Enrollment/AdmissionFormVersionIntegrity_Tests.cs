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
