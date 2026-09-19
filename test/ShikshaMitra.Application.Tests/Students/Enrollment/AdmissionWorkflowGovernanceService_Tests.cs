using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowGovernanceService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IRepository<IdentityUser, Guid> _users;
	private readonly IRepository<IdentityRole, Guid> _roles;
	private readonly IRepository<PermissionGrant, Guid> _permissionGrants;
	private readonly AdmissionWorkflowGovernanceService _service;
	private readonly ICurrentTenant _currentTenant;
	private readonly ICurrentPrincipalAccessor _principalAccessor;

	public AdmissionWorkflowGovernanceService_Tests()
	{
		_users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_permissionGrants = GetRequiredService<IRepository<PermissionGrant, Guid>>();
		_service = GetRequiredService<AdmissionWorkflowGovernanceService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
	}

	[Fact]
	public async Task Approver_lookup_and_assignment_require_the_approve_permission()
	{
		var tenantId = Guid.NewGuid();
		var makerId = Guid.NewGuid();
		var directApproverId = Guid.NewGuid();
		var roleApproverId = Guid.NewGuid();
		var ineligibleUserId = Guid.NewGuid();
		var suffix = Guid.NewGuid().ToString("N")[..8];
		using var tenant = _currentTenant.Change(tenantId);
		using var principal = _principalAccessor.Change(Principal(makerId));

		await WithUnitOfWorkAsync(async () =>
		{
			var role = new IdentityRole(Guid.NewGuid(), $"workflow-checker-{suffix}", tenantId);
			await _roles.InsertAsync(role, autoSave: true);
			await InsertUserAsync(makerId, $"maker-{suffix}");
			await InsertUserAsync(directApproverId, $"direct-checker-{suffix}");
			await InsertUserAsync(ineligibleUserId, $"viewer-{suffix}");
			var roleApprover = await InsertUserAsync(roleApproverId, $"role-checker-{suffix}");
			roleApprover.AddRole(role.Id);
			await _users.UpdateAsync(roleApprover, autoSave: true);

			await GrantAsync("U", makerId.ToString("D"));
			await GrantAsync("U", directApproverId.ToString("D"));
			await GrantAsync("R", role.Name);

			var approvers = await _service.GetApproversAsync(suffix);

			approvers.Select(item => item.Id).ShouldContain(directApproverId);
			approvers.Select(item => item.Id).ShouldContain(roleApproverId);
			approvers.Select(item => item.Id).ShouldNotContain(makerId);
			approvers.Select(item => item.Id).ShouldNotContain(ineligibleUserId);
			await Should.NotThrowAsync(() => _service.EnsureApproverAsync(directApproverId));
			(await Should.ThrowAsync<BusinessException>(() => _service.EnsureApproverAsync(ineligibleUserId)))
				.Code.ShouldBe(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		});
	}

	private async Task<IdentityUser> InsertUserAsync(Guid id, string userName)
	{
		var user = new IdentityUser(id, userName, $"{userName}@test.local", _currentTenant.Id);
		return await _users.InsertAsync(user, autoSave: true);
	}

	private Task GrantAsync(string providerName, string providerKey) =>
		_permissionGrants.InsertAsync(new PermissionGrant(
			Guid.NewGuid(),
			ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve,
			providerName,
			providerKey,
			_currentTenant.Id), autoSave: true);

	private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
		[new Claim(AbpClaimTypes.UserId, userId.ToString())], "Test"));
}
