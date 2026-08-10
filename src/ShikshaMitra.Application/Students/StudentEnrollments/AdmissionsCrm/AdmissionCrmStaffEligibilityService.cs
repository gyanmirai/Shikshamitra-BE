using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmStaffEligibilityService : ApplicationService, ITransientDependency
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IRepository<IdentityUser, Guid> _userRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;
	private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IPermissionChecker _permissionChecker;

	public AdmissionCrmStaffEligibilityService(
		IEmployeeRepository employeeRepository,
		IRepository<IdentityUser, Guid> userRepository,
		IRepository<IdentityRole, Guid> roleRepository,
		IRepository<PermissionGrant, Guid> permissionGrantRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IPermissionChecker permissionChecker)
	{
		_employeeRepository = employeeRepository;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_permissionGrantRepository = permissionGrantRepository;
		_cycleRepository = cycleRepository;
		_permissionChecker = permissionChecker;
	}

	public async Task<IReadOnlyDictionary<Guid, Employee>> GetEligibleEmployeesByUserIdAsync()
	{
		var crmUserIds = await GetCrmUserIdsAsync();
		if (crmUserIds.Count == 0)
		{
			return new Dictionary<Guid, Employee>();
		}

		var employees = await _employeeRepository.GetListAsync(employee =>
			employee.UserId.HasValue &&
			(!employee.EmployeeStatusType.HasValue || employee.EmployeeStatusType == EmployeeStatusType.Active));

		return employees
			.Where(employee => employee.UserId.HasValue && crmUserIds.Contains(employee.UserId.Value))
			.GroupBy(employee => employee.UserId!.Value)
			.ToDictionary(group => group.Key, group => group.OrderBy(employee => employee.EmployeeCode).First());
	}

	public async Task<HashSet<Guid>> GetOwnerEligibleUserIdsAsync(Guid academicYearId)
	{
		var eligible = (await GetEligibleEmployeesByUserIdAsync()).Keys.ToHashSet();
		if (eligible.Count == 0)
		{
			return eligible;
		}

		var configuration = await _cycleRepository.FirstOrDefaultAsync(item =>
			item.AcademicYearId == academicYearId);
		if (configuration == null)
		{
			return eligible;
		}

		var ownerPool = AdmissionCrmLeadRoutingRuleMatcher
			.DeserializeSettings(configuration.LeadRoutingRulesJson)
			.OwnerPoolUserIds
			.ToHashSet();
		if (ownerPool.Count > 0)
		{
			eligible.IntersectWith(ownerPool);
		}

		return eligible;
	}

	public async Task<bool> IsOwnerEligibleAsync(Guid userId, Guid academicYearId)
	{
		var eligibleOwnerIds = await GetOwnerEligibleUserIdsAsync(academicYearId);
		return eligibleOwnerIds.Contains(userId);
	}

	public async Task EnsureOwnerEligibleAsync(Guid? userId, Guid academicYearId)
	{
		if (!userId.HasValue)
		{
			return;
		}

		var eligibleEmployees = await GetEligibleEmployeesByUserIdAsync();
		if (!eligibleEmployees.ContainsKey(userId.Value))
		{
			throw new BusinessException("AdmissionCrm:OwnerMustBeActiveEmployee")
				.WithData("OwnerUserId", userId.Value);
		}

		var eligibleOwnerIds = await GetOwnerEligibleUserIdsAsync(academicYearId);
		if (!eligibleOwnerIds.Contains(userId.Value))
		{
			throw new BusinessException("AdmissionCrm:OwnerOutsideConfiguredPool")
				.WithData("OwnerUserId", userId.Value);
		}
	}

	public async Task EnsureTaskAssigneeEligibleAsync(Guid? userId)
	{
		if (!userId.HasValue)
		{
			return;
		}

		var eligibleEmployees = await GetEligibleEmployeesByUserIdAsync();
		if (!eligibleEmployees.ContainsKey(userId.Value))
		{
			throw new BusinessException("AdmissionCrm:AssigneeMustBeActiveEmployee")
				.WithData("AssignedUserId", userId.Value);
		}
	}

	public async Task EnsureReviewerEligibleAsync(Guid userId)
	{
		await EnsureTaskAssigneeEligibleAsync(userId);
		var reviewerIds = await GetReviewerEligibleUserIdsAsync();
		if (!reviewerIds.Contains(userId))
		{
			throw new BusinessException("AdmissionCrm:ReviewerPermissionRequired")
				.WithData("ReviewerUserId", userId);
		}
	}

	public async Task<HashSet<Guid>> GetReviewerEligibleUserIdsAsync()
	{
		var submitIds = await GetUserIdsWithPermissionAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SubmitReviews);
		var viewIds = await GetUserIdsWithPermissionAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewStructuredReviews);
		submitIds.IntersectWith(viewIds);
		if (submitIds.Count > 0)
		{
			var activeUsers = await _userRepository.GetListAsync(user =>
				user.IsActive && submitIds.Contains(user.Id));
			submitIds.IntersectWith(activeUsers.Select(user => user.Id));
		}
		return submitIds;
	}

	public async Task ValidateConfigurationStaffAsync(
		AdmissionCrmLeadRoutingSettingsDto routingSettings,
		IReadOnlyCollection<AdmissionCrmChannelSettingDto> channelSettings,
		IReadOnlyCollection<AdmissionCrmJourneyRuleDto> journeyRules,
		AdmissionCrmSlaPolicyDto slaPolicy)
	{
		var ownerPool = (routingSettings.OwnerPoolUserIds ?? [])
			.Where(userId => userId != Guid.Empty)
			.Distinct()
			.ToHashSet();
		var routedOwnerIds = new[] { routingSettings.DefaultOwnerUserId }
			.Concat((routingSettings.Rules ?? []).Select(rule => rule.OwnerUserId))
			.Concat(channelSettings.Select(setting => setting.DefaultOwnerUserId))
			.Concat(journeyRules.Select(rule => rule.OwnerUserId))
			.Concat([slaPolicy.EscalationOwnerUserId])
			.Where(userId => userId.HasValue)
			.Select(userId => userId!.Value)
			.Distinct()
			.ToList();
		var referencedUserIds = ownerPool.Concat(routedOwnerIds).ToHashSet();
		if (referencedUserIds.Count == 0)
		{
			return;
		}

		var eligibleEmployees = await GetEligibleEmployeesByUserIdAsync();
		var invalidUserIds = referencedUserIds.Where(userId => !eligibleEmployees.ContainsKey(userId)).ToList();
		if (invalidUserIds.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:ConfiguredStaffMustBeActiveEmployees")
				.WithData("UserIds", string.Join(",", invalidUserIds));
		}

		if (ownerPool.Count > 0)
		{
			var outsidePool = routedOwnerIds.Where(userId => !ownerPool.Contains(userId)).ToList();
			if (outsidePool.Count > 0)
			{
				throw new BusinessException("AdmissionCrm:ConfiguredOwnerOutsidePool")
					.WithData("UserIds", string.Join(",", outsidePool));
			}
		}
	}

	private async Task<HashSet<Guid>> GetCrmUserIdsAsync()
	{
		var permissionName = ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View;
		var userIds = await GetUserIdsWithPermissionAsync(permissionName);

		var currentUserId = CurrentUser.Id;
		if (currentUserId.HasValue && await _permissionChecker.IsGrantedAsync(permissionName))
		{
			userIds.Add(currentUserId.Value);
		}

		return userIds;
	}

	private async Task<HashSet<Guid>> GetUserIdsWithPermissionAsync(string permissionName)
	{
		var grants = await _permissionGrantRepository.GetListAsync(grant =>
			grant.Name == permissionName &&
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
		if (roleNames.Count > 0)
		{
			var roles = await _roleRepository.GetListAsync(role => roleNames.Contains(role.Name));
			var roleIds = roles.Select(role => role.Id).ToList();
			if (roleIds.Count > 0)
			{
				var queryable = await _userRepository.GetQueryableAsync();
				var roleUserIds = await AsyncExecuter.ToListAsync(queryable
					.Where(user => user.Roles.Any(role => roleIds.Contains(role.RoleId)))
					.Select(user => user.Id));
				userIds.UnionWith(roleUserIds);
			}
		}

		return userIds;
	}
}
