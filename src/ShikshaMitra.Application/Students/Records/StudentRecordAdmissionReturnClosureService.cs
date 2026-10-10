using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Closes the linked return in its owner's transaction without changing the permanent student record.</summary>
[RemoteService(false)]
public class StudentRecordAdmissionReturnClosureService(
	IRepository<StudentRecordReturnHandoff, Guid> handoffs,
	IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<StudentRecordTransitionEvent, Guid> events,
	IRepository<AdmissionApplicant, Guid> applicants)
	: ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task CloseRejectedApplicationAsync(AdmissionApplication application, string reason)
		=> await CloseApplicationAsync(application, reason, AdmissionApplicationStatus.Rejected, "AdmissionRejected");

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	public async Task CloseWithdrawnApplicationAsync(AdmissionApplication application, string reason)
		=> await CloseApplicationAsync(application, reason, AdmissionApplicationStatus.Withdrawn, "AdmissionWithdrawn");

	private async Task CloseApplicationAsync(AdmissionApplication application, string reason, AdmissionApplicationStatus expectedStatus, string action)
	{
		var handoff = await handoffs.FindAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId.HasValue &&
			x.AdmissionApplicationId == application.Id);
		if (handoff == null) return;
		if (application.TenantId != CurrentTenant.Id || application.Status != expectedStatus ||
			application.OnboardingCaseId.HasValue || application.StudentEnrollmentId.HasValue ||
			handoff.AdmissionOfferingId != application.AdmissionOfferingId)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		var applicant = await applicants.GetAsync(x => x.Id == application.ApplicantId && x.TenantId == CurrentTenant.Id);
		if (applicant.ExistingStudentId != handoff.StudentId)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		await CloseAsync(handoff, action, reason.Trim());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel)]
	public async Task CloseCancelledOnboardingAsync(StudentOnboardingCase item)
	{
		var transitionId = StudentRecordAdmissionReturnService.ReturnTransitionId(item);
		if (!transitionId.HasValue) return;
		var handoff = await handoffs.GetAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId == transitionId);
		if (item.TenantId != CurrentTenant.Id || item.Status != StudentOnboardingStatus.Cancelled ||
			item.StudentEnrollmentId.HasValue || handoff.AdmissionApplicationId != item.ApplicationId ||
			handoff.StudentId != item.StudentId || handoff.AcademicYearId != item.AcademicYearId ||
			handoff.AdmissionOfferingId != item.OfferingId)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		var transition = await GetTransitionAsync(handoff);
		if (transition.TargetStandardId != item.IntendedStandardId)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		await CloseAsync(handoff, "OnboardingCancelled", item.CancellationReason!, transition);
	}

	private async Task<StudentRecordTransition> GetTransitionAsync(StudentRecordReturnHandoff handoff)
	{
		var transition = await transitions.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == handoff.TransitionId);
		if (transition.ReturnHandoffId != handoff.Id || transition.Outcome != "Return" ||
			transition.StudentId != handoff.StudentId || transition.TargetAcademicYearId != handoff.AcademicYearId ||
			transition.TargetEnrollmentId.HasValue || transition.AppliedAt.HasValue)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		return transition;
	}

	private async Task CloseAsync(StudentRecordReturnHandoff handoff, string action, string reason, StudentRecordTransition? transition = null)
	{
		transition ??= await GetTransitionAsync(handoff);
		if (transition.Status == "Cancelled") return;
		var actorId = CurrentUser.Id ?? throw new AbpAuthorizationException();
		transition.CancelAdmissionReturn(activationHasStarted: false);
		await transitions.UpdateAsync(transition, false);
		await events.InsertAsync(new StudentRecordTransitionEvent(GuidGenerator.Create(), CurrentTenant.Id!.Value,
			transition.Id, actorId, action, transition.CurrentApprovalStage, reason), false);
	}
}
