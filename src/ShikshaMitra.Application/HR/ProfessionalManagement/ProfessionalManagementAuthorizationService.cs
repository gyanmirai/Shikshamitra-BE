using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalManagementAuthorizationService(
	IPermissionChecker permissionChecker,
	IRepository<PerformanceReviewAssignment, Guid> assignmentRepository,
	IRepository<PerformanceEvidence, Guid> evidenceRepository,
	IEmployeeRepository employeeRepository,
	ProfessionalManagementReadScopeService readScope)
	: ApplicationService, ITransientDependency
{
	private const string AccessDenied = "Access denied: This professional review is outside your assigned review scope.";

	public async Task EnsureViewAsync()
	{
		await EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.View,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);
	}

	public async Task EnsureCanViewReviewAsync(Guid reviewAssignmentId)
	{
		await EnsureViewAsync();
		var assignment = await assignmentRepository.GetAsync(reviewAssignmentId);
		await readScope.EnsureEmployeeAsync(assignment.EmployeeId);
	}

	public async Task EnsureReviewExistsAsync(Guid reviewAssignmentId)
	{
		await assignmentRepository.GetAsync(reviewAssignmentId);
	}

	public async Task EnsureCanViewEmployeeHistoryAsync(Guid employeeId)
	{
		await EnsureViewAsync();
		await readScope.EnsureEmployeeAsync(employeeId);
		await employeeRepository.GetAsync(employeeId);
	}

	public async Task<bool> CanViewConfidentialOutcomeAsync() =>
		await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ViewConfidentialOutcome) ||
		await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View);

	public async Task EnsureCanSelfReviewAsync(Guid reviewAssignmentId)
	{
		if (await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View))
		{
			await assignmentRepository.GetAsync(reviewAssignmentId);
			return;
		}

		await EnsureGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview);
		var assignment = await assignmentRepository.GetAsync(reviewAssignmentId);
		if (ProfessionalManagementAccessRules.CanSelfReview(await GetCurrentEmployeeIdAsync(), assignment))
		{
			return;
		}

		throw new AbpAuthorizationException(AccessDenied);
	}

	public async Task EnsureCanReviewEmployeeAsync(Guid reviewAssignmentId)
	{
		if (await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add))
		{
			await assignmentRepository.GetAsync(reviewAssignmentId);
			return;
		}

		await EnsureGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees);
		var assignment = await assignmentRepository.GetAsync(reviewAssignmentId);
		if (await IsCurrentReviewerAsync(assignment))
		{
			return;
		}

		throw new AbpAuthorizationException(AccessDenied);
	}

	public async Task EnsureCanAddObservationAsync(Guid reviewAssignmentId, Guid observerEmployeeId)
	{
		await ResolveObservationObserverEmployeeIdAsync(reviewAssignmentId, observerEmployeeId);
	}

	public async Task<Guid> ResolveObservationObserverEmployeeIdAsync(Guid reviewAssignmentId, Guid? requestedObserverEmployeeId)
	{
		if (await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add))
		{
			await assignmentRepository.GetAsync(reviewAssignmentId);
			if (requestedObserverEmployeeId is { } adminObserverEmployeeId && adminObserverEmployeeId != Guid.Empty)
			{
				return adminObserverEmployeeId;
			}

			return await GetCurrentEmployeeIdAsync()
			       ?? throw new AbpAuthorizationException("Access denied: Observer staff could not be resolved.");
		}

		await EnsureGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ClassroomObservation);
		var assignment = await assignmentRepository.GetAsync(reviewAssignmentId);
		var currentEmployeeId = await GetCurrentEmployeeIdAsync();
		if (currentEmployeeId.HasValue &&
		    ProfessionalManagementAccessRules.CanAddObservation(currentEmployeeId, assignment, currentEmployeeId.Value))
		{
			return currentEmployeeId.Value;
		}

		throw new AbpAuthorizationException(AccessDenied);
	}

	public async Task EnsureCanUploadEvidenceAsync(Guid reviewAssignmentId)
	{
		if (await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.View) ||
		    await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add))
		{
			await assignmentRepository.GetAsync(reviewAssignmentId);
			return;
		}

		var assignment = await assignmentRepository.GetAsync(reviewAssignmentId);
		var hasSelfReviewPermission = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview);
		var hasReviewEmployeesPermission = await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees);
		if (ProfessionalManagementAccessRules.CanUploadEvidence(
			    await GetCurrentEmployeeIdAsync(),
			    assignment,
			    hasSelfReviewPermission,
			    hasReviewEmployeesPermission))
		{
			return;
		}

		throw new AbpAuthorizationException($"Required permission: {ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.SelfReview}");
	}

	public async Task EnsureCanDeleteEvidenceAsync(Guid evidenceId)
	{
		if (await IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add))
		{
			await evidenceRepository.GetAsync(evidenceId);
			return;
		}

		await EnsureGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ReviewEmployees);
		var evidence = await evidenceRepository.GetAsync(evidenceId);
		var assignment = await assignmentRepository.GetAsync(evidence.ReviewAssignmentId);
		if (await IsCurrentReviewerAsync(assignment))
		{
			return;
		}

		throw new AbpAuthorizationException(AccessDenied);
	}

	public async Task EnsureCanManageOutcomeAsync(Guid reviewAssignmentId)
	{
		await EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.HR.PerformanceManagement.ManageOutcomes,
			ShikshaMitraSubmodulePermissions.HR.ProfessionalManagement.Add);
		await assignmentRepository.GetAsync(reviewAssignmentId);
	}

	public async Task EnsureAnyPermissionAsync(string primaryPermission, string fallbackPermission)
	{
		if (await IsGrantedAsync(primaryPermission) || await IsGrantedAsync(fallbackPermission))
		{
			return;
		}

		throw new AbpAuthorizationException($"Required permission: {primaryPermission}");
	}

	private async Task EnsureGrantedAsync(string permission)
	{
		if (!await IsGrantedAsync(permission))
		{
			throw new AbpAuthorizationException($"Required permission: {permission}");
		}
	}

	private async Task<bool> IsCurrentReviewerAsync(PerformanceReviewAssignment assignment)
	{
		return ProfessionalManagementAccessRules.CanReviewEmployee(await GetCurrentEmployeeIdAsync(), assignment);
	}

	private async Task<Guid?> GetCurrentEmployeeIdAsync()
	{
		var employee = await GetCurrentEmployeeAsync();
		return employee?.Id;
	}

	private async Task<Employee?> GetCurrentEmployeeAsync()
	{
		if (!CurrentUser.Id.HasValue)
		{
			return null;
		}

		return await employeeRepository.GetEmployeeByUserIdAsync(CurrentUser.Id.Value);
	}

	private async Task<bool> IsGrantedAsync(string permission) =>
		await permissionChecker.IsGrantedAsync(permission);
}
