using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
[RemoteService(false)]
public class AdmissionCrmWorkspaceAppService : ApplicationService, IAdmissionCrmWorkspaceAppService
{
	private readonly IRepository<IdentityUser, Guid> _userRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;
	private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;
	private readonly IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> _providerRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmWorkspaceAppService(
		IRepository<IdentityUser, Guid> userRepository,
		IRepository<IdentityRole, Guid> roleRepository,
		IRepository<PermissionGrant, Guid> permissionGrantRepository,
		IRepository<AdmissionCrmCommunicationProviderConfiguration, Guid> providerRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_permissionGrantRepository = permissionGrantRepository;
		_providerRepository = providerRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	public async Task<IReadOnlyList<AdmissionCrmStaffLookupDto>> GetStaffAsync(
		string? filter = null,
		string? ids = null,
		int maxResultCount = 20)
	{
		var allowedUserIds = await GetCrmUserIdsAsync();
		if (allowedUserIds.Count == 0)
		{
			return [];
		}

		var requestedIds = ParseIds(ids);
		var take = Math.Clamp(maxResultCount, 1, 50);
		var queryable = await _userRepository.GetQueryableAsync();
		var eligible = queryable.Where(user => user.IsActive && allowedUserIds.Contains(user.Id));
		var requested = requestedIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync(eligible.Where(user => requestedIds.Contains(user.Id)));

		var searchQuery = eligible.Where(user => !requestedIds.Contains(user.Id));
		if (!string.IsNullOrWhiteSpace(filter))
		{
			var search = filter.Trim().ToLowerInvariant();
			searchQuery = searchQuery.Where(user =>
				user.UserName.ToLower().Contains(search) ||
				(user.Name != null && user.Name.ToLower().Contains(search)) ||
				(user.Surname != null && user.Surname.ToLower().Contains(search)) ||
				(user.Email != null && user.Email.ToLower().Contains(search)));
		}

		var searchResults = await AsyncExecuter.ToListAsync(searchQuery
			.OrderBy(user => user.Name)
			.ThenBy(user => user.Surname)
			.ThenBy(user => user.UserName)
			.Take(take));

		return requested.Concat(searchResults)
			.DistinctBy(user => user.Id)
			.Select(MapStaff)
			.ToList();
	}

	public async Task<IReadOnlyList<AdmissionCrmIntegrationCapabilityDto>> GetIntegrationCapabilitiesAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetIntegrationCapabilitiesAsync));
		var providerQuery = await _providerRepository.GetQueryableAsync();
		var hasConfiguredProvider = await AsyncExecuter.AnyAsync(providerQuery.Where(provider =>
			provider.TenantId == CurrentTenant.Id &&
			provider.AcademicYearId == academicYearId &&
			provider.IsEnabled &&
			provider.Status != AdmissionCrmCommunicationProviderConfiguration.NotConfiguredStatus));

		return
		[
			Capability("finance", "Finance status", AdmissionCrmIntegrationCapabilityState.Manual,
				"Manual reference", "Enrollment records verified payment state and references until Finance synchronization is connected."),
			Capability("communications", "Outbound communications",
				hasConfiguredProvider ? AdmissionCrmIntegrationCapabilityState.Configured : AdmissionCrmIntegrationCapabilityState.Manual,
				hasConfiguredProvider ? "Generic webhook or manual queue" : "Manual queue",
				hasConfiguredProvider ? "At least one active generic provider is configured." : "Messages remain in the manual delivery queue."),
			Capability("government", "Government exchange", AdmissionCrmIntegrationCapabilityState.NotConfigured,
				"Validated files", "RTE and government workflows use validated imports and exports; no live adapter is configured."),
			Capability("optional-services", "Optional service handoff", AdmissionCrmIntegrationCapabilityState.Manual,
				"Manual handoff", "Enrollment records service interests without claiming destination-module fulfillment."),
			Capability("document-signing", "Document signing", AdmissionCrmIntegrationCapabilityState.NotConfigured,
				"Unsigned generation", "Documents can be previewed and issued without a digital-signing adapter.")
		];
	}

	private async Task<HashSet<Guid>> GetCrmUserIdsAsync()
	{
		var grants = await _permissionGrantRepository.GetListAsync(grant =>
			grant.Name == ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View &&
			(grant.ProviderName == "U" || grant.ProviderName == "R"));
		var userIds = grants
			.Where(grant => grant.ProviderName == "U" && Guid.TryParse(grant.ProviderKey, out _))
			.Select(grant => Guid.Parse(grant.ProviderKey!))
			.ToHashSet();
		var roleNames = grants
			.Where(grant => grant.ProviderName == "R" && !string.IsNullOrWhiteSpace(grant.ProviderKey))
			.Select(grant => grant.ProviderKey!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (roleNames.Count == 0)
		{
			return userIds;
		}

		var roles = await _roleRepository.GetListAsync(role => roleNames.Contains(role.Name));
		var roleIds = roles.Select(role => role.Id).ToList();
		if (roleIds.Count == 0)
		{
			return userIds;
		}

		var queryable = await _userRepository.GetQueryableAsync();
		var roleUserIds = await AsyncExecuter.ToListAsync(queryable
			.Where(user => user.Roles.Any(role => roleIds.Contains(role.RoleId)))
			.Select(user => user.Id));
		userIds.UnionWith(roleUserIds);
		return userIds;
	}

	private static HashSet<Guid> ParseIds(string? ids) => string.IsNullOrWhiteSpace(ids)
		? []
		: ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
			.Where(id => id != Guid.Empty)
			.ToHashSet();

	private static AdmissionCrmStaffLookupDto MapStaff(IdentityUser user) => new()
	{
		Id = user.Id,
		DisplayName = string.Join(" ", new[] { user.Name, user.Surname }.Where(value => !string.IsNullOrWhiteSpace(value))) is { Length: > 0 } name
			? name
			: user.UserName,
		UserName = user.UserName,
		Email = user.Email
	};

	private static AdmissionCrmIntegrationCapabilityDto Capability(
		string code,
		string displayName,
		AdmissionCrmIntegrationCapabilityState state,
		string operationalMode,
		string detail) => new()
	{
		Code = code,
		DisplayName = displayName,
		State = state,
		OperationalMode = operationalMode,
		Detail = detail
	};
}
