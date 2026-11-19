using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Employees.Onboarding;

public class EmployeeOnboardingAcademicScopeAssigner(
	UserScopeAssignmentManager assignmentManager,
	IUserScopeAssignmentRepository scopeRepository,
	EmployeeOrganizationAssignmentSyncer organizationAssignmentSyncer,
	IUnitOfWorkManager unitOfWorkManager,
	IPermissionChecker permissionChecker) : ITransientDependency
{
	private const string AssignmentDescription = "Staff onboarding academic permission";

	public async Task AssignAsync(
		Guid userId,
		Guid academicYearId,
		IReadOnlyCollection<EmployeeApplicationAcademicScopeDto> scopes)
	{
		var normalizedScopes = NormalizeScopes(scopes);

		var assignments = new List<UserScopeAssignment>(normalizedScopes.Count);
		foreach (var scope in normalizedScopes)
		{
			assignments.Add(await assignmentManager.CreateAsync(
				userId,
				null,
				academicYearId,
				scope.EducationBoardId,
				scope.EducationMediumId,
				scope.EducationMediumStageId,
				scope.StandardId,
				scope.DivisionId,
				AssignmentDescription,
				checkDuplicate: false));
		}

		await scopeRepository.InsertManyAsync(assignments, autoSave: false);
		await SaveChangesAsync();
		await organizationAssignmentSyncer.SyncFromAcademicPermissionsAsync([userId], [academicYearId]);
		await SaveChangesAsync();
	}

	public async Task ValidateAsync(
		Guid academicYearId,
		IReadOnlyCollection<EmployeeApplicationAcademicScopeDto> scopes)
	{
		if (!await permissionChecker.IsGrantedAsync(ShikshaMitraPermissions.UserScope.BulkAssign))
		{
			throw new AbpAuthorizationException(
				$"Required permission: {ShikshaMitraPermissions.UserScope.BulkAssign}");
		}

		foreach (var scope in NormalizeScopes(scopes))
		{
			await assignmentManager.ValidateAcademicHierarchyAsync(
				academicYearId,
				scope.EducationBoardId,
				scope.EducationMediumId,
				scope.EducationMediumStageId,
				scope.StandardId,
				scope.DivisionId);
		}
	}

	private static List<EmployeeApplicationAcademicScopeDto> NormalizeScopes(
		IReadOnlyCollection<EmployeeApplicationAcademicScopeDto> scopes)
	{
		return scopes
			.GroupBy(scope => new
			{
				scope.EducationBoardId,
				scope.EducationMediumId,
				scope.EducationMediumStageId,
				scope.StandardId,
				scope.DivisionId
			})
			.Select(group => group.First())
			.ToList();
	}

	private Task SaveChangesAsync() =>
		unitOfWorkManager.Current?.SaveChangesAsync() ??
		throw new InvalidOperationException("Staff onboarding scope assignment requires an active unit of work.");
}
