using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsQueueAccess(
	IRepository<AdmissionCrmWorkDelegation, Guid> delegationRepository) : ApplicationService, ITransientDependency
{
	internal async Task<HashSet<Guid>> GetDelegatedOwnersAsync(Guid academicYearId, AdmissionCrmOperationsQueueType queueType, Guid delegateUserId)
	{
		var now = Clock.Now;
		var queryable = await delegationRepository.GetQueryableAsync();
		var delegations = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.DelegateUserId == delegateUserId &&
			x.Status != AdmissionCrmDelegationStatus.Cancelled && x.StartsAt <= now && x.EndsAt >= now));
		return delegations.Where(x =>
			(JsonSerializer.Deserialize<List<AdmissionCrmOperationsQueueType>>(x.QueueTypesJson) ?? []).Contains(queueType))
			.Select(x => x.DelegatorUserId).ToHashSet();
	}

	internal async Task<IReadOnlyList<AdmissionCrmOperationsQueueType>> GetPermittedQueueTypesAsync()
	{
		var queueTypes = Enum.GetValues<AdmissionCrmOperationsQueueType>().ToHashSet();
		if (!await IsAnyPermissionGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishExamResults))
		{
			queueTypes.RemoveWhere(AdmissionCrmOperationsQueueBuilder.IsExamQueue);
		}
		if (!await IsAnyPermissionGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments,
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness))
		{
			queueTypes.RemoveWhere(AdmissionCrmOperationsQueueBuilder.IsRegistrationQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewSeatPlans))
		{
			queueTypes.RemoveWhere(AdmissionCrmOperationsQueueBuilder.IsOfferQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.PaymentCondition);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewActivation))
		{
			queueTypes.RemoveWhere(AdmissionCrmOperationsQueueBuilder.IsActivationQueue);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.RouteEligibilityReview);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.GovernmentAllotmentReconciliation);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.TransferEvidencePending);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.ReturningIdentityMatch);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.RouteAppealReview);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.HouseholdResolution);
			queueTypes.Remove(AdmissionCrmOperationsQueueType.IdentityConflict);
		}
		if (!await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks))
		{
			queueTypes.Remove(AdmissionCrmOperationsQueueType.PolicyException);
		}
		return queueTypes.OrderBy(x => (int) x).ToList();
	}

	internal async Task<bool> IsAnyPermissionGrantedAsync(params string[] permissions)
	{
		foreach (var permission in permissions)
		{
			if (await AuthorizationService.IsGrantedAsync(permission)) return true;
		}
		return false;
	}
}