using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("Main Workflow")]
	[InlineData("  main workflow  ")]
	public async Task Workflow_Name_Should_Reject_Duplicate_Independent_Families_Including_Archived(string name)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var archived = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, "Main Workflow", "ARCHIVED_MAIN");
		archived.Publish(TestUserId);
		archived.Archive();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>()
			.InsertAsync(archived, autoSave: true));

		var createError = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.CreateAsync(
			new CreateUpdateAdmissionWorkflowDto { Name = name }));
		createError.Code.ShouldBe("Admissions.WorkflowNameAlreadyExists");
		var cloneError = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.CloneAsync(archived.Id,
			new CloneAdmissionWorkflowDto { Name = name }));
		cloneError.Code.ShouldBe("Admissions.WorkflowNameAlreadyExists");
	}

	[Fact]
	public async Task Workflow_Name_Should_Reject_Rename_To_Another_Family_And_Allow_Self_Edit()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Main Workflow" });
		var second = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Other Workflow" });
		var unchanged = await _workflowAppService.UpdateAsync(first.Id, ToUpdateInput(first));
		unchanged.Name.ShouldBe(first.Name);
		var input = ToUpdateInput(second);
		input.Name = " main workflow ";
		var error = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.UpdateAsync(second.Id, input));
		error.Code.ShouldBe("Admissions.WorkflowNameAlreadyExists");
		(await _workflowAppService.GetAsync(second.Id)).Name.ShouldBe("Other Workflow");
	}

	[Fact]
	public async Task Workflow_Name_Should_Allow_The_Same_Name_In_Another_Tenant()
	{
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		using (var tenant = _currentTenant.Change(TestTenantId))
			await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Main Workflow" });
		using (var tenant = _currentTenant.Change(Guid.NewGuid()))
			(await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Main Workflow" }))
				.Name.ShouldBe("Main Workflow");
	}

	[Fact]
	public async Task Workflow_Name_Should_Preserve_Names_Within_Version_History()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var published = await CreateAndPublishCurrentWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(published.Id);
		draft.Name.ShouldBe(published.Name);
		draft = await _workflowAppService.UpdateAsync(draft.Id, ToUpdateInput(draft));
		(await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp)).Name.ShouldBe(published.Name);
		var rollback = await _workflowAppService.CreateRollbackDraftAsync(published.Id,
			new RollbackAdmissionWorkflowDto { ChangeReason = "Restore the original journey" });
		rollback.Name.ShouldBe(published.Name);
		(await _workflowAppService.UpdateAsync(rollback.Id, ToUpdateInput(rollback))).Name.ShouldBe(published.Name);
	}

	[Fact]
	public async Task Workflow_Name_Should_Allow_Reusing_A_Deleted_Draft_Name()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Removed draft" });
		await _workflowAppService.DeleteAsync(draft.Id, draft.ConcurrencyStamp);
		(await _workflowAppService.CreateAsync(new CreateUpdateAdmissionWorkflowDto { Name = "Removed draft" }))
			.Name.ShouldBe("Removed draft");
	}

	[Fact]
	public async Task Workflow_Name_Should_Block_Reopening_A_Legacy_Duplicate_As_Published()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var published = await CreateAndPublishCurrentWorkflowAsync();
		var archived = await _workflowAppService.ArchiveAsync(published.Id, published.ConcurrencyStamp);
		var legacy = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, published.Name, "LEGACY_OTHER_FAMILY");
		legacy.Publish(TestUserId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>()
			.InsertAsync(legacy, autoSave: true));
		var error = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.ReopenAsync(archived.Id, archived.ConcurrencyStamp));
		error.Code.ShouldBe("Admissions.WorkflowNameAlreadyExists");
		(await _workflowAppService.GetAsync(archived.Id)).Status.ShouldBe(AdmissionWorkflowStatus.Archived);
	}

	[Fact]
	public async Task Workflow_Name_Should_Block_Publication_Of_A_Legacy_Duplicate_Family()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var published = await CreateAndPublishCurrentWorkflowAsync();
		var draft = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, published.Name, "LEGACY_DUPLICATE");
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>()
			.InsertAsync(draft, autoSave: true));
		var error = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp));
		error.Code.ShouldBe("Admissions.WorkflowNameAlreadyExists");
	}
}
