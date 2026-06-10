using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class StaffUserScopeSeederContributor : BaseSeederContributor
{
	private static readonly HashSet<string> FullOrganizationScopeRoles = new(StringComparer.OrdinalIgnoreCase)
	{
		"Director",
		"Vice Principal",
		"Administrative Manager",
		"IT Administrator",
		"HOD",
		"Teacher",
		"Clerk",
		"Office Assistant",
		"HR Manager",
		"Finance Officer"
	};

	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<UserScopeAssignment, Guid> _scopeRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly UserScopeAssignmentManager _assignmentManager;

	public StaffUserScopeSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		IRepository<UserScopeAssignment, Guid> scopeRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		UserScopeAssignmentManager assignmentManager)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_scopeRepository = scopeRepository;
		_educationBoardRepository = educationBoardRepository;
		_assignmentManager = assignmentManager;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var tenantId = CurrentTenant.Id;
		if (!tenantId.HasValue)
		{
			return;
		}

		var existingAssignments = await _scopeRepository.GetListAsync();
		if (existingAssignments.Count > 0)
		{
			await _scopeRepository.DeleteManyAsync(existingAssignments, autoSave: false);
		}

		var users = await _userRepository.GetListAsync(includeDetails: true);
		var roles = await _roleRepository.GetListAsync();
		var rolesById = roles.ToDictionary(role => role.Id);

		foreach (var user in users)
		{
			var userRoleNames = user.Roles
				.Select(userRole => rolesById.GetValueOrDefault(userRole.RoleId)?.Name)
				.Where(roleName => !string.IsNullOrWhiteSpace(roleName))
				.Cast<string>()
				.ToArray();

			if (userRoleNames.Any(roleName => string.Equals(roleName, "Principal", StringComparison.OrdinalIgnoreCase)))
			{
				await EnsurePrincipalBoardScopeAsync(user, tenantId.Value);
				continue;
			}

			if (!userRoleNames.Any(roleName => FullOrganizationScopeRoles.Contains(roleName)))
			{
				continue;
			}

			var assignment = await _assignmentManager.CreateAsync(
				userId: user.Id,
				roleName: null,
				description: $"Seeded full organization scope for {string.Join(", ", userRoleNames)}.",
				checkDuplicate: false);
			await _scopeRepository.InsertAsync(assignment);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task EnsurePrincipalBoardScopeAsync(IdentityUser user, Guid tenantId)
	{
		await DeactivateFullOrganizationScopeAsync(user.Id);

		var boardType = ResolvePrincipalBoardType(user);
		if (!boardType.HasValue)
		{
			return;
		}

		var boards = await _educationBoardRepository.GetListAsync(board => board.Type == boardType.Value);
		foreach (var board in boards)
		{
			var assignment = await _assignmentManager.CreateAsync(
				userId: user.Id,
				roleName: null,
				academicYearId: board.AcademicYearId,
				educationBoardId: board.Id,
				description: $"Seeded {board.Type} board scope for Principal.",
				checkDuplicate: false);
			await _scopeRepository.InsertAsync(assignment);
		}
	}

	private async Task DeactivateFullOrganizationScopeAsync(Guid userId)
	{
		var existingScope = await _scopeRepository.FirstOrDefaultAsync(scope =>
			scope.UserId == userId &&
			scope.AcademicYearId == null &&
			scope.EducationBoardId == null &&
			scope.EducationMediumId == null &&
			scope.EducationMediumStageId == null &&
			scope.StandardId == null &&
			scope.DivisionId == null);

		if (existingScope is { IsActive: true })
		{
			existingScope.Deactivate();
			await _scopeRepository.UpdateAsync(existingScope);
		}
	}

	private static EducationBoardType? ResolvePrincipalBoardType(IdentityUser user)
	{
		var email = user.Email ?? string.Empty;
		if (email.Contains("cbse", StringComparison.OrdinalIgnoreCase))
		{
			return EducationBoardType.CBSE;
		}

		if (email.Contains("gseb", StringComparison.OrdinalIgnoreCase) ||
		    email.Equals("principal-demo-gseb@ddvs.com", StringComparison.OrdinalIgnoreCase))
		{
			return EducationBoardType.GSEB;
		}

		return EducationBoardType.GSEB;
	}
}
