using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewPeopleService(IRepository<IdentityUser, Guid> users, IRepository<IdentityRole, Guid> roles,
	IUserScopeService scopes, IRepository<AdmissionOffering, Guid> offerings, IRepository<AdmissionCycle, Guid> cycles,
	IRepository<Division, Guid> divisions, IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<bool> CanReviewAsync(AdmissionReviewState state, string permission) => state.Context.CanMutate &&
		await permissions.IsGrantedAsync(permission) &&
		(!state.AssignedReviewerId.HasValue || state.AssignedReviewerId == CurrentUser.Id) &&
		(string.IsNullOrWhiteSpace(state.Context.Policy?.ReviewerRole) || CurrentUser.IsInRole(state.Context.Policy.ReviewerRole));
	public async Task<bool> CanDecideAsync(AdmissionReviewState state) => state.Context.CanMutate &&
		await permissions.IsGrantedAsync(Permissions.Decide) && await permissions.IsGrantedAsync(ApplicationPermissions.Decide) &&
		(string.IsNullOrWhiteSpace(state.Context.Policy?.ApproverRole) || CurrentUser.IsInRole(state.Context.Policy.ApproverRole)) &&
		(state.Context.Policy?.RequireIndependentApprover != true || state.Latest?.RecordedBy != CurrentUser.Id);
	public async Task EnsureAssigneeAsync(Guid? userId, AdmissionReviewContext context)
	{
		if (!userId.HasValue) return;
		var user = await users.FindAsync(userId.Value, includeDetails: true);
		if (user?.IsActive != true || !await HasRoleAsync(user, context.Policy?.ReviewerRole) || !await HasOfferingAccessAsync(user.Id, context.Application.AdmissionOfferingId))
			throw new BusinessException(AdmissionReviewErrorCodes.ReviewerNotAllowed);
	}
	public async Task<bool> CanAssignHierarchyAsync(Guid userId, string? role, AdmissionCycle cycle, AdmissionOfferingWriteDto input)
	{
		var user = await users.FindAsync(userId, includeDetails: true);
		if (user?.IsActive != true || !await HasRoleAsync(user, role)) return false;
		// Reuse the same scope expression for the not-yet-persisted offering; only this standard's divisions are loaded.
		var candidate = new AdmissionOffering(Guid.Empty, CurrentTenant.Id, cycle.Id, null, input.BoardId, input.MediumId,
			input.StageId, input.StandardId, "REVIEW_SCOPE", true, null, null);
		var standardDivisions = await divisions.GetListAsync(item => item.StandardId == input.StandardId);
		return AdmissionOfferingScopeQuery.Apply(new[] { candidate }.AsQueryable(), new[] { cycle }.AsQueryable(),
			standardDivisions.AsQueryable(), await scopes.GetUserScopeAsync(userId)).Any();
	}
	public async Task<List<AdmissionWorkflowValidationIssueDto>> ValidatePolicyAsync(AdmissionWorkflowStageDefinition stage)
	{
		try
		{
			var policy = AdmissionReviewAccessService.ReadPolicy(stage); if (policy == null) return [];
			foreach (var role in new[] { policy.ReviewerRole, policy.ApproverRole }.Where(role => !string.IsNullOrWhiteSpace(role)))
				if (!await roles.AnyAsync(item => item.NormalizedName == role!.ToUpperInvariant()))
					throw new ArgumentException("Select existing reviewer and approver roles for this school.");
			if (policy.AssignedReviewerId.HasValue)
			{
				var user = await users.FindAsync(policy.AssignedReviewerId.Value, includeDetails: true);
				if (user?.IsActive != true || !await HasRoleAsync(user, policy.ReviewerRole))
					throw new ArgumentException("Select an active reviewer belonging to the configured role.");
			}
			return [];
		}
		catch (Exception exception) when (exception is BusinessException or ArgumentException)
		{ return [new() { Code = AdmissionReviewErrorCodes.InvalidPolicy, Message = exception.Message, StageId = stage.Id, Severity = AdmissionWorkflowValidationSeverity.Error }]; }
	}
	public async Task<List<AdmissionEligibilityRoleDto>> GetRolesAsync() => (await roles.GetListAsync()).OrderBy(item => item.Name)
		.Select(item => new AdmissionEligibilityRoleDto { Id = item.Id, Name = item.Name }).ToList();
	public async Task<List<AdmissionReviewUserDto>> GetUsersAsync(string? filter, string? role)
	{
		if (filter?.Length > 128 || role?.Length > 256) throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		var query = (await users.GetQueryableAsync()).Where(user => user.IsActive);
		if (!string.IsNullOrWhiteSpace(filter)) query = query.Where(user => user.UserName.Contains(filter) || user.Name.Contains(filter) || user.Surname.Contains(filter));
		if (!string.IsNullOrWhiteSpace(role))
		{
			var selected = await roles.FindAsync(item => item.NormalizedName == role.ToUpperInvariant());
			if (selected == null) return [];
			query = query.Where(user => user.Roles.Any(item => item.RoleId == selected.Id));
		}
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(user => user.UserName).Take(50));
		var result = new List<AdmissionReviewUserDto>();
		foreach (var user in rows)
			if ((await scopes.GetUserScopeAsync(user.Id)).HasAnyAccess)
				result.Add(new() { Id = user.Id, UserName = user.UserName, Name = Display(user) });
		return result;
	}
	public async Task<string?> GetNameAsync(Guid? userId) => userId.HasValue && await users.FindAsync(userId.Value) is { } user ? Display(user) : null;
	private static string Display(IdentityUser user) => string.IsNullOrWhiteSpace(user.Name) && string.IsNullOrWhiteSpace(user.Surname) ? user.UserName : $"{user.Name} {user.Surname}".Trim();
	private async Task<bool> HasRoleAsync(IdentityUser user, string? role)
	{
		if (string.IsNullOrWhiteSpace(role)) return true;
		var selected = await roles.FindAsync(item => item.NormalizedName == role.ToUpperInvariant());
		return selected != null && user.Roles.Any(item => item.RoleId == selected.Id);
	}
	private async Task<bool> HasOfferingAccessAsync(Guid userId, Guid offeringId)
	{
		var query = AdmissionOfferingScopeQuery.Apply(await offerings.GetQueryableAsync(), await cycles.GetQueryableAsync(),
			await divisions.GetQueryableAsync(), await scopes.GetUserScopeAsync(userId));
		return await AsyncExecuter.AnyAsync(query.Where(item => item.Id == offeringId));
	}
}
