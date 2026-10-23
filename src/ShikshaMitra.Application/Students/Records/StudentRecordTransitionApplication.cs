using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public partial class StudentRecordTransitionApplication(IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<StudentRecordTransitionEvent, Guid> events, IStudentRepository students,
	IRepository<StudentEnrollment, Guid> enrollments, StudentRecordsAccessService access,
	StudentRecordTransitionReadinessService readiness, StudentRecordRenewalCoordinator renewals,
	StudentRecordTransitionCorrectionService corrections, IPermissionChecker permissions,
	IdentityUserManager users, ICurrentPrincipalAccessor principals, StudentRecordTransitionAuthorityService authority, StudentRecordAdmissionReturnService admissionReturns) : ApplicationService, ITransientDependency
{
	public async Task ApplyOrScheduleAsync(Guid id)
	{
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var item = await transitions.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (item.Status is not ("Approved" or "Scheduled")) return;
			await EnsureAuthorityAsync(item);
			await authority.ValidateAsync(item);
			var policy = StudentRecordsPolicy.FromJson(item.PolicyJson);
			var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
			var removalOnApproval = (item.Outcome is "Graduate" or "Withdraw" or "Transfer") && policy.Transitions.RosterRemovalTiming == "Approval";
			if (item.EffectiveDate > today && !removalOnApproval)
			{
				if (item.Status == "Approved") item.Schedule();
				await transitions.UpdateAsync(item); await work.CompleteAsync(); return;
			}
			if (policy.Transitions.BatchExceptionMode == "HoldAll")
			{
				var siblings = await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == item.BatchId && x.Id != id);
				if (siblings.Any(x => x.Status is "Submitted" or "Rejected" or "Cancelled" or "Exception")) { await work.CompleteAsync(); return; }
				foreach (var sibling in siblings.Where(x => x.Status is "Approved" or "Scheduled"))
				{
					if (sibling.ReturnHandoffId.HasValue) continue;
					var preview = await readiness.PreviewAsync(sibling.AcademicYearId, JsonSerializer.Deserialize<StudentRecordTransitionInput>(sibling.InputJson)!, StudentRecordsPolicy.FromJson(sibling.PolicyJson), false, validateDate: false, frozenRollRule: sibling.GetOnboardingRollRule());
					if (!preview.Ready) throw new BusinessException("Students:Records:BatchHeld");
				}
			}
			if (item.CorrectionOfId.HasValue) await corrections.ApplyAsync(item);
			else await ApplyCoreAsync(item, policy);
			await transitions.UpdateAsync(item);
			await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, item.Id, CurrentUser.Id!.Value, item.Status == "AwaitingAdmission" ? "AdmissionOpened" : item.Status == "AwaitingRenewal" ? "RenewalOpened" : "Applied", item.CurrentApprovalStage, item.Reason));
			await work.CompleteAsync();
		}
		catch (BusinessException exception) { await MarkExceptionAsync(id, exception.Code ?? "Students:Records:ValidationConflict"); }
		catch (AbpAuthorizationException) { await MarkExceptionAsync(id, "Students:Records:AuthorityChanged"); }
		catch (AbpDbConcurrencyException) { await MarkExceptionAsync(id, "Students:Records:ConcurrencyConflict"); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception) ||
			ShikshaMitra.Finance.Fees.Collections.WithdrawalProrationService.IsSourceConflict(exception))
		{ await MarkExceptionAsync(id, "Students:Records:ConcurrencyConflict"); }
	}
	private async Task ApplyCoreAsync(StudentRecordTransition item, StudentRecordsPolicy policy)
	{
		if (item.ReturnHandoffId.HasValue) { await admissionReturns.StartApprovedAsync(item); return; }
		var input = JsonSerializer.Deserialize<StudentRecordTransitionInput>(item.InputJson)!;
		var row = await readiness.PreviewAsync(item.AcademicYearId, input, policy, false, validateDate: false, frozenRollRule: item.GetOnboardingRollRule());
		if (!row.Ready) throw new BusinessException(row.Issues[0].Code).WithData("Issues", string.Join(" ", row.Issues.Select(x => x.Message)));
		var source = await readiness.GetSourceAsync(item.StudentId, item.AcademicYearId);
		if (StudentRecordTransitionReadinessService.Capture(source.Student, source.Enrollment) != item.BeforeJson) throw new BusinessException("Students:Records:StaleSnapshot");
		if (item.Outcome is "Promote" or "Repeat" or "Return")
		{
			if (item.Outcome == "Return" || policy.Transitions.AnnualRenewalMode == "ShortChecklist")
			{
				var renewal = await renewals.CreateAsync(item, input); item.WaitForRenewal(renewal.Id); return;
			}
			var standard = await readiness.ValidateTargetAsync(item.StudentId, item.AcademicYearId, input.TargetAcademicYearId!.Value, input.TargetStandardId!.Value, input.TargetDivisionId!.Value, input.RollNumber);
			var next = await readiness.ActivateTargetEnrollmentAsync(item.StudentId, input.TargetAcademicYearId.Value, input.TargetStandardId.Value, input.TargetDivisionId.Value, input.RollNumber, effectiveDate: item.EffectiveDate);
			source.Enrollment.Deactivate(item.EffectiveDate); await enrollments.UpdateAsync(source.Enrollment);
			var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
			source.Student.UpdateAcademicPlacement(input.TargetAcademicYearId.Value, medium.EducationBoardId, medium.Id, stage.Id, standard.Id, input.TargetDivisionId.Value);
			source.Student.AssignRollNo(input.RollNumber); source.Student.ChangeOperationalStatus(StudentStatus.Active, item.Reason); await students.UpdateAsync(source.Student);
			item.Apply(next.Id, StudentRecordTransitionReadinessService.Capture(source.Student, next), Clock.Now); return;
		}
		if (item.Outcome is "Graduate" or "Withdraw" or "Transfer")
		{
			if (item.Outcome == "Withdraw")
				await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Finance.Fees.Collections.WithdrawalProrationService>()
					.ApplyAsync(item.Id, item.StudentId, item.AcademicYearId, item.EffectiveDate, item.Reason);
			var removalDate = policy.Transitions.RosterRemovalTiming == "Approval"
				? AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now) : item.EffectiveDate;
			source.Enrollment.Deactivate(removalDate); source.Student.ChangeOperationalStatus(StudentStatus.Left, item.Reason);
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Portal.StudentPortalLifecycleService>().RevokeAsync(item, source.Student);
			await enrollments.UpdateAsync(source.Enrollment); await students.UpdateAsync(source.Student);
		}
		item.Apply(null, StudentRecordTransitionReadinessService.Capture(source.Student, source.Enrollment), Clock.Now);
	}
	private async Task EnsureAuthorityAsync(StudentRecordTransition item)
	{
		if (!CurrentUser.Id.HasValue || CurrentUser.Id != (item.LastApprovedBy ?? item.RequestedBy)) throw new AbpAuthorizationException();
		if (!await permissions.IsGrantedAsync(item.ApprovalStageCount > 0 ? ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve : ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)) throw new AbpAuthorizationException();
		var actor = await users.FindByIdAsync(CurrentUser.Id.Value.ToString());
		if (actor == null || actor.TenantId != CurrentTenant.Id || !actor.IsActive) throw new AbpAuthorizationException();
		if (item.ApprovalStageCount > 0)
		{
			var rule = JsonSerializer.Deserialize<StudentRecordsChangeRule>(item.ApprovalRuleJson)!;
			var roles = await access.GetDatabaseRoleNamesAsync();
			if (!rule.Stages[item.ApprovalStageCount - 1].RoleNames.Intersect(roles, StringComparer.OrdinalIgnoreCase).Any()) throw new AbpAuthorizationException();
		}
	}
	public async Task ApplyAsRecordedActorAsync(Guid id)
	{
		Guid actor;
		using (var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
		{
			var item = await transitions.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
			if (item.Status is not ("Approved" or "Scheduled" or "Exception")) return;
			actor = item.LastApprovedBy ?? item.RequestedBy;
			if (item.Status == "Exception") { item.Retry(); await transitions.UpdateAsync(item); }
			await read.CompleteAsync();
		}
		var user = await users.FindByIdAsync(actor.ToString());
		if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive) { await MarkExceptionAsync(id, "Students:Records:AuthorityChanged"); return; }
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, actor.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
		if (CurrentTenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, CurrentTenant.Id.Value.ToString()));
		foreach (var role in await users.GetRolesAsync(user)) claims.Add(new(AbpClaimTypes.Role, role));
		using var principal = principals.Change(new ClaimsPrincipal(new ClaimsIdentity(claims, "StudentTransitionScheduledCommand")));
		await ApplyOrScheduleAsync(id);
	}
	private async Task MarkExceptionAsync(Guid id, string code)
	{
		using var work = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var item = await transitions.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		if (item.Status is "Approved" or "Scheduled") { item.MarkException(code); await transitions.UpdateAsync(item); }
		await work.CompleteAsync();
	}
}
