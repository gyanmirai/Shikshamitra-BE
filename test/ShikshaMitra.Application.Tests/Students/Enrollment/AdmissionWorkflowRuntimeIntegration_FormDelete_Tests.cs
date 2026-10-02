using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("stale-stamp")]
	public async Task Form_Delete_Rejects_Missing_Or_Stale_Concurrency_Stamp(string? stamp)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var form = await _formAppService.CreateAsync(new CreateUpdateAdmissionFormDto { Name = "Unused draft" });

		var error = await Should.ThrowAsync<BusinessException>(() => _formAppService.DeleteAsync(form.Id, stamp));
		error.Code.ShouldBe("Admissions.FormConcurrencyConflict");
		(await _formAppService.GetAsync(form.Id)).Status.ShouldBe(AdmissionFormStatus.Draft);
	}

	[Fact]
	public async Task Form_Delete_Rejects_A_Published_Form_Even_Without_Dependencies()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Published inquiry",
			"PUBLISHED_INQUIRY", AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
		form.Publish(TestUserId);
		await WithUnitOfWorkAsync(() => _formRepository.InsertAsync(form, autoSave: true));
		var current = await _formAppService.GetAsync(form.Id);

		var error = await Should.ThrowAsync<BusinessException>(() => _formAppService.DeleteAsync(form.Id, current.ConcurrencyStamp));
		error.Code.ShouldBe(AdmissionFormErrorCodes.DraftRequired);
		(await _formAppService.GetAsync(form.Id)).Status.ShouldBe(AdmissionFormStatus.Published);
	}

	[Fact]
	public async Task Unused_Form_Version_Draft_Can_Be_Deleted_Without_Removing_Published_Version()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var published = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Main admission inquiry",
			"MAIN_INQUIRY", AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
		published.Publish(TestUserId);
		await WithUnitOfWorkAsync(() => _formRepository.InsertAsync(published, autoSave: true));

		var draft = await _formAppService.CreateNewVersionAsync(published.Id);
		(await _formAppService.GetUsageAsync(draft.Id)).CanDelete.ShouldBeTrue();
		await _formAppService.DeleteAsync(draft.Id, draft.ConcurrencyStamp);

		var versions = await _formAppService.GetVersionsAsync(published.Id);
		versions.Select(version => version.Id).ShouldBe(new[] { published.Id });
	}

	[Fact]
	public async Task Form_Delete_Rejects_A_Linked_Draft_With_Dependency_Counts()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var workflow = await CreateDirectWorkflowDraftAsync();
		var offering = await CreateAvailableOfferingAsync(workflow.Id, assertValid: false);
		var draft = await _formAppService.CreateAsync(new CreateUpdateAdmissionFormDto
		{
			Name = "Linked inquiry draft", Purpose = AdmissionFormPurpose.Inquiry
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.ConfigureReferences(workflow.Id, draft.Id, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, autoSave: true);
		});
		var current = await _formAppService.GetAsync(draft.Id);

		var error = await Should.ThrowAsync<BusinessException>(() => _formAppService.DeleteAsync(draft.Id, current.ConcurrencyStamp));
		error.Code.ShouldBe(AdmissionFormErrorCodes.FormInUse);
		error.Data["OfferingCount"].ShouldBe(1);
		error.Data["ApplicationCount"].ShouldBe(0);
		error.Data["PublicLinkCount"].ShouldBe(0);
		(await _formAppService.GetAsync(draft.Id)).Status.ShouldBe(AdmissionFormStatus.Draft);
	}
}
