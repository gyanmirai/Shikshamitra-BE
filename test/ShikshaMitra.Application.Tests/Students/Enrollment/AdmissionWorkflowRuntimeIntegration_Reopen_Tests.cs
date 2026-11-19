using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Reopen_restores_same_published_version_with_concurrency_and_one_audit_entry()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await CreateDirectPublicationDraftAsync();
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var archived = await _workflowAppService.ArchiveAsync(published.Id, published.ConcurrencyStamp);
		(await Should.ThrowAsync<BusinessException>(() => _workflowAppService.ReopenAsync(archived.Id, "stale")))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.ConcurrencyConflict);
		var reopened = await _workflowAppService.ReopenAsync(archived.Id, archived.ConcurrencyStamp);
		reopened.Id.ShouldBe(published.Id);
		reopened.Version.ShouldBe(published.Version);
		reopened.Status.ShouldBe(AdmissionWorkflowStatus.Published);
		reopened.PublishedAt.ShouldBe(published.PublishedAt);
		reopened.Stages.Select(stage => stage.Id).ShouldBe(published.Stages.Select(stage => stage.Id));
		(await _workflowAppService.ReopenAsync(reopened.Id, reopened.ConcurrencyStamp)).Id.ShouldBe(reopened.Id);
		(await _workflowAppService.GetGovernanceHistoryAsync(reopened.Id)).Count(item => item.Action == AdmissionWorkflowGovernanceAction.Reopened).ShouldBe(1);
		using var otherTenant = _currentTenant.Change(Guid.NewGuid());
		await Should.ThrowAsync<EntityNotFoundException>(() => _workflowAppService.ReopenAsync(reopened.Id, reopened.ConcurrencyStamp));
	}

	[Fact]
	public async Task Reopen_does_not_replace_a_newer_published_version_or_create_a_draft()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await CreateDirectPublicationDraftAsync();
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var archived = await _workflowAppService.ArchiveAsync(published.Id, published.ConcurrencyStamp);
		var next = await _workflowAppService.CreateNewVersionAsync(archived.Id);
		var current = await _workflowAppService.PublishAsync(next.Id, next.ConcurrencyStamp);
		var restored = await _workflowAppService.ReopenAsync(archived.Id, archived.ConcurrencyStamp);
		restored.Status.ShouldBe(AdmissionWorkflowStatus.Superseded);
		(await _workflowAppService.GetAsync(current.Id)).Status.ShouldBe(AdmissionWorkflowStatus.Published);
		var versions = await _workflowAppService.GetVersionHistoryAsync(restored.Id);
		versions.Count.ShouldBe(2);
		versions.Count(item => item.Status == AdmissionWorkflowStatus.Published).ShouldBe(1);
	}

	[Fact]
	public async Task Reopen_preserves_the_superseded_status_saved_by_archive_history()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await CreateDirectPublicationDraftAsync();
		var first = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var next = await _workflowAppService.CreateNewVersionAsync(first.Id);
		var current = await _workflowAppService.PublishAsync(next.Id, next.ConcurrencyStamp);
		first = await _workflowAppService.GetAsync(first.Id);
		var archivedFirst = await _workflowAppService.ArchiveAsync(first.Id, first.ConcurrencyStamp);
		await _workflowAppService.ArchiveAsync(current.Id, current.ConcurrencyStamp);
		(await _workflowAppService.ReopenAsync(archivedFirst.Id, archivedFirst.ConcurrencyStamp)).Status.ShouldBe(AdmissionWorkflowStatus.Superseded);
	}
}
