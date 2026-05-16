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
		"Head of Department",
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

	public StaffUserScopeSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		IRepository<UserScopeAssignment, Guid> scopeRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_scopeRepository = scopeRepository;
		_educationBoardRepository = educationBoardRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var tenantId = CurrentTenant.Id;
		if (!tenantId.HasValue)
		{
			return;
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

			var existingScope = await _scopeRepository.FirstOrDefaultAsync(scope =>
				scope.UserId == user.Id &&
				scope.AcademicYearId == null &&
				scope.EducationBoardId == null &&
				scope.EducationMediumId == null &&
				scope.EducationMediumStageId == null &&
				scope.StandardId == null &&
				scope.DivisionId == null);

			if (existingScope != null)
			{
				if (!existingScope.IsActive)
				{
					existingScope.Activate();
					await _scopeRepository.UpdateAsync(existingScope);
				}

				continue;
			}

			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				GuidGenerator.Create(),
				tenantId,
				userId: user.Id,
				description: $"Seeded full organization scope for {string.Join(", ", userRoleNames)}."));
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
			var existingScope = await _scopeRepository.FirstOrDefaultAsync(scope =>
				scope.UserId == user.Id &&
				scope.AcademicYearId == board.AcademicYearId &&
				scope.EducationBoardId == board.Id &&
				scope.EducationMediumId == null &&
				scope.EducationMediumStageId == null &&
				scope.StandardId == null &&
				scope.DivisionId == null);

			if (existingScope != null)
			{
				if (!existingScope.IsActive)
				{
					existingScope.Activate();
					await _scopeRepository.UpdateAsync(existingScope);
				}

				continue;
			}

			await _scopeRepository.InsertAsync(new UserScopeAssignment(
				GuidGenerator.Create(),
				tenantId,
				userId: user.Id,
				academicYearId: board.AcademicYearId,
				educationBoardId: board.Id,
				description: $"Seeded {board.Type} board scope for Principal."));
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
