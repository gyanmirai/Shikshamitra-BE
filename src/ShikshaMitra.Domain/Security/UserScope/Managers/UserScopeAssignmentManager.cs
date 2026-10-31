using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeAssignmentManager : DomainService
{
	private readonly IUserScopeAssignmentRepository _scopeRepository;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;

	public UserScopeAssignmentManager(
		IUserScopeAssignmentRepository scopeRepository,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Division, Guid> divisionRepository)
	{
		_scopeRepository = scopeRepository;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_academicYearRepository = academicYearRepository;
		_educationBoardRepository = educationBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
	}

	public async Task<UserScopeAssignment> CreateAsync(
		Guid? userId,
		string? roleName,
		Guid? academicYearId = null,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		string? description = null,
		bool checkDuplicate = true,
		CancellationToken cancellationToken = default)
	{
		var normalizedRoleName = NormalizeRoleName(roleName);
		await ValidateAsync(
			userId,
			normalizedRoleName,
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			checkDuplicate,
			cancellationToken);

		return new UserScopeAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			userId,
			normalizedRoleName,
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			description);
	}

	public async Task ValidateAsync(
		Guid? userId,
		string? roleName,
		Guid? academicYearId = null,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		bool checkDuplicate = true,
		CancellationToken cancellationToken = default)
	{
		var normalizedRoleName = NormalizeRoleName(roleName);
		await ValidateTargetAsync(userId, normalizedRoleName, cancellationToken);
		await ValidateHierarchyAsync(
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			cancellationToken);

		if (checkDuplicate && await _scopeRepository.ExistsAsync(
			    userId,
			    normalizedRoleName,
			    academicYearId,
			    educationBoardId,
			    educationMediumId,
			    educationMediumStageId,
			    standardId,
			    divisionId,
			    cancellationToken))
		{
			throw new BusinessException(UserScopeErrorCodes.AlreadyExists)
				.WithData("UserId", userId!)
				.WithData("RoleName", normalizedRoleName!);
		}
	}

	public Task ValidateAcademicHierarchyAsync(
		Guid? academicYearId = null,
		Guid? educationBoardId = null,
		Guid? educationMediumId = null,
		Guid? educationMediumStageId = null,
		Guid? standardId = null,
		Guid? divisionId = null,
		CancellationToken cancellationToken = default)
	{
		return ValidateHierarchyAsync(
			academicYearId,
			educationBoardId,
			educationMediumId,
			educationMediumStageId,
			standardId,
			divisionId,
			cancellationToken);
	}

	private async Task ValidateTargetAsync(Guid? userId, string? roleName, CancellationToken cancellationToken)
	{
		if ((userId.HasValue && !string.IsNullOrWhiteSpace(roleName)) ||
		    (!userId.HasValue && string.IsNullOrWhiteSpace(roleName)))
		{
			throw new BusinessException(UserScopeErrorCodes.InvalidAssignment);
		}

		if (userId.HasValue && await _userRepository.FindAsync(userId.Value, cancellationToken: cancellationToken) is null)
		{
			throw new BusinessException(UserScopeErrorCodes.UserNotFound)
				.WithData("UserId", userId.Value);
		}

		if (!string.IsNullOrWhiteSpace(roleName) &&
		    await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant(), cancellationToken: cancellationToken) is null)
		{
			throw new BusinessException(UserScopeErrorCodes.RoleNotFound)
				.WithData("RoleName", roleName);
		}
	}

	private async Task ValidateHierarchyAsync(
		Guid? academicYearId,
		Guid? educationBoardId,
		Guid? educationMediumId,
		Guid? educationMediumStageId,
		Guid? standardId,
		Guid? divisionId,
		CancellationToken cancellationToken)
	{
		AcademicYear? academicYear = null;
		EducationBoard? board = null;
		EducationMedium? medium = null;
		EducationMediumStage? mediumStage = null;
		Standard? standard = null;
		Division? division = null;

		if (academicYearId.HasValue)
		{
			academicYear = await _academicYearRepository.FindAsync(academicYearId.Value, cancellationToken: cancellationToken);
			EnsureExists(academicYear, nameof(academicYearId), academicYearId.Value);
		}

		if (educationBoardId.HasValue)
		{
			board = await _educationBoardRepository.FindAsync(educationBoardId.Value, cancellationToken: cancellationToken);
			EnsureExists(board, nameof(educationBoardId), educationBoardId.Value);
		}

		if (educationMediumId.HasValue)
		{
			medium = await _educationMediumRepository.FindAsync(educationMediumId.Value, cancellationToken: cancellationToken);
			EnsureExists(medium, nameof(educationMediumId), educationMediumId.Value);
		}

		if (educationMediumStageId.HasValue)
		{
			mediumStage = await _educationMediumStageRepository.FindAsync(educationMediumStageId.Value, cancellationToken: cancellationToken);
			EnsureExists(mediumStage, nameof(educationMediumStageId), educationMediumStageId.Value);
		}

		if (standardId.HasValue)
		{
			standard = await _standardRepository.FindAsync(standardId.Value, cancellationToken: cancellationToken);
			EnsureExists(standard, nameof(standardId), standardId.Value);
		}

		if (divisionId.HasValue)
		{
			division = await _divisionRepository.FindAsync(divisionId.Value, cancellationToken: cancellationToken);
			EnsureExists(division, nameof(divisionId), divisionId.Value);
		}

		if (division is not null && standard is null)
		{
			standard = await _standardRepository.FindAsync(division.StandardId, cancellationToken: cancellationToken);
			EnsureExists(standard, nameof(standardId), division.StandardId);
		}

		if (standard is not null && mediumStage is null)
		{
			mediumStage = await _educationMediumStageRepository.FindAsync(standard.EducationMediumStageId, cancellationToken: cancellationToken);
			EnsureExists(mediumStage, nameof(educationMediumStageId), standard.EducationMediumStageId);
		}

		if (mediumStage is not null && medium is null)
		{
			medium = await _educationMediumRepository.FindAsync(mediumStage.EducationMediumId, cancellationToken: cancellationToken);
			EnsureExists(medium, nameof(educationMediumId), mediumStage.EducationMediumId);
		}

		if (medium is not null && board is null)
		{
			board = await _educationBoardRepository.FindAsync(medium.EducationBoardId, cancellationToken: cancellationToken);
			EnsureExists(board, nameof(educationBoardId), medium.EducationBoardId);
		}

		if (academicYear is not null && board is not null && board.AcademicYearId != academicYear.Id)
		{
			ThrowInvalidHierarchy(nameof(educationBoardId), board.Id, nameof(academicYearId), academicYear.Id);
		}

		if (board is not null && medium is not null && medium.EducationBoardId != board.Id)
		{
			ThrowInvalidHierarchy(nameof(educationMediumId), medium.Id, nameof(educationBoardId), board.Id);
		}

		if (medium is not null && mediumStage is not null && mediumStage.EducationMediumId != medium.Id)
		{
			ThrowInvalidHierarchy(nameof(educationMediumStageId), mediumStage.Id, nameof(educationMediumId), medium.Id);
		}

		if (mediumStage is not null && standard is not null && standard.EducationMediumStageId != mediumStage.Id)
		{
			ThrowInvalidHierarchy(nameof(standardId), standard.Id, nameof(educationMediumStageId), mediumStage.Id);
		}

		if (standard is not null && division is not null && division.StandardId != standard.Id)
		{
			ThrowInvalidHierarchy(nameof(divisionId), division.Id, nameof(standardId), standard.Id);
		}
	}

	private static string? NormalizeRoleName(string? roleName) =>
		string.IsNullOrWhiteSpace(roleName) ? null : roleName.Trim();

	private static void EnsureExists(object? entity, string fieldName, Guid id)
	{
		if (entity is null)
		{
			throw new BusinessException(UserScopeErrorCodes.InvalidReferences)
				.WithData("Field", fieldName)
				.WithData("Id", id);
		}
	}

	private static void ThrowInvalidHierarchy(string childField, Guid childId, string parentField, Guid parentId)
	{
		throw new BusinessException(
				UserScopeErrorCodes.InvalidHierarchy,
				"The selected academic permission row is not a valid hierarchy path. Please choose board, medium, stage, class, and division from the same branch.")
			.WithData("ChildField", childField)
			.WithData("ChildId", childId)
			.WithData("ParentField", parentField)
			.WithData("ParentId", parentId);
	}
}
