using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordRenewalCoordinator(IRepository<StudentRecordRenewalCase, Guid> renewals,
	IRepository<StudentRecordTransition, Guid> transitions, IStudentRepository students,
	IRepository<StudentEnrollment, Guid> enrollments, StudentRecordsAccessService access,
	StudentRecordTransitionReadinessService placement, StudentRecordRenewalReadinessService readiness,
	StudentRecordsPolicyResolver policies, StudentScopeAccessService scope, IRepository<StudentRecordTransitionEvent, Guid> events,
	StudentRecordTransitionAuthorityService authority, IRepository<StudentFeeStructure, Guid> feeAssignments)
	: ApplicationService, ITransientDependency
{
	public async Task<bool> CanCancelAsync(StudentRecordTransition transition)
	{
		if (transition.Status != "AwaitingRenewal" || !transition.RenewalCaseId.HasValue) return false;
		var item = await GetScopedAsync(transition.RenewalCaseId.Value);
		if (item.Status != "PendingChecklist" || item.StudentEnrollmentId.HasValue) return false;
		var before = JsonSerializer.Deserialize<StudentRecordTransitionSnapshot>(item.SourceSnapshotJson)!;
		// Historical same-year obligations stay with the existing enrollment. Only
		// dependencies introduced for a different target year prevent cancellation.
		return item.AcademicYearId == before.AcademicYearId || (!await enrollments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AcademicYearId == item.AcademicYearId) && !await feeAssignments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AcademicYearId == item.AcademicYearId));
	}
	public async Task CancelAsync(StudentRecordTransition transition)
	{
		if (!await CanCancelAsync(transition)) throw new BusinessException("Students:Records:InvalidRenewalState");
		var item = await GetScopedAsync(transition.RenewalCaseId!.Value); item.Cancel(); await renewals.UpdateAsync(item);
	}
	public async Task<StudentRecordRenewalCase> CreateAsync(StudentRecordTransition transition, StudentRecordTransitionInput input)
	{
		var existing = await renewals.FindAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId == transition.Id);
		if (existing != null) return existing;
		if (await renewals.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == transition.StudentId && x.AcademicYearId == input.TargetAcademicYearId && x.Status != "Completed" && x.Status != "Cancelled")) throw new BusinessException("Students:Records:RenewalAlreadyExists");
		var rules = await readiness.CaptureRulesAsync(transition.StudentId, input.TargetAcademicYearId!.Value, input.TargetStandardId!.Value, input.TargetDivisionId!.Value);
		var item = new StudentRecordRenewalCase(GuidGenerator.Create(), CurrentTenant.Id!.Value, transition.StudentId,
			input.TargetAcademicYearId.Value, input.TargetStandardId.Value, input.TargetDivisionId.Value, input.RollNumber,
			transition.Id, transition.PolicyJson, rules, transition.BeforeJson, "transition:" + transition.Id.ToString("N"));
		return await renewals.InsertAsync(item);
	}
	public async Task<StudentRecordRenewalDto> GetAsync(Guid id)
	{
		var item = await GetScopedAsync(id);
		var policy = StudentRecordsPolicy.FromJson(item.PolicyJson);
		var requirements = JsonSerializer.Deserialize<StudentRecordRenewalRequirements>(item.OnboardingRulesJson)!;
		var student = await access.GetScopedStudentAsync(item.StudentId);
		var currentPolicy = (await policies.ResolveAsync(student.AcademicYearId)).Policy;
		var yearPolicy = (await policies.ResolveAsync(item.AcademicYearId)).Policy;
		var roles = await access.GetDatabaseRoleNamesAsync();
		return new()
		{
			Id = item.Id, StudentId = item.StudentId, AcademicYearId = item.AcademicYearId, StandardId = item.StandardId,
			DivisionId = item.DivisionId, RollNumber = item.RollNumber, Status = item.Status, ConcurrencyStamp = item.ConcurrencyStamp,
			StudentName = policy.IsMasked("Identity", roles) || currentPolicy.IsMasked("Identity", roles) || yearPolicy.IsMasked("Identity", roles) ? null : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			GuardianReviewRequired = requirements.Family || requirements.Communication || requirements.ServiceConsent,
			GuardianReviewConfirmed = item.GuardianReviewConfirmed, FinanceRequired = true,
			PortalReviewRequired = requirements.StudentAccount || requirements.GuardianAccount,
			Issues = item.Status is "Completed" or "Cancelled" ? [] : await readiness.GetIssuesAsync(item, item.Status == "Provisioning")
		};
	}
	public async Task<StudentRecordRenewalCase> GetScopedAsync(Guid id)
	{
		var item = await renewals.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		await access.GetScopedStudentAsync(item.StudentId);
		await scope.EnsureCanAccessStandardAsync(item.StandardId, "renewal target class");
		await scope.EnsureCanAccessDivisionAsync(item.DivisionId, "renewal target division"); return item;
	}
	public async Task ConfirmAsync(Guid id, StudentRecordRenewalDecisionDto input)
	{
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		item.ConfirmGuardianReview(CurrentUser.Id ?? throw new BusinessException("Students:Records:UserRequired"), Clock.Now, input.Remarks);
		await renewals.UpdateAsync(item, autoSave: true);
		await AddEventAsync(item, "RenewalGuardianReviewed", input.Remarks);
	}
	public async Task ActivateAsync(Guid id, StudentRecordRenewalDecisionDto input)
	{
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		if (item.Status == "Completed") return;
		if (!item.TransitionId.HasValue) throw new BusinessException("Students:Records:InvalidRenewalState");
		var approved = await transitions.GetAsync(x => x.Id == item.TransitionId && x.TenantId == CurrentTenant.Id);
		if (approved.Status != "AwaitingRenewal") throw new BusinessException("Students:Records:InvalidRenewalState");
		await authority.ValidateAsync(approved);
		if (item.Status == "PendingChecklist")
		{
			if (StudentRecordsPolicy.FromJson(item.PolicyJson).Transitions.BatchExceptionMode == "HoldAll")
			{
				var siblings = await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == approved.BatchId && x.Id != approved.Id);
				if (siblings.Any(x => x.Status is "Submitted" or "Rejected" or "Cancelled" or "Exception")) throw new BusinessException("Students:Records:BatchHeld");
				foreach (var sibling in siblings.Where(x => x.Status == "AwaitingRenewal" && x.RenewalCaseId.HasValue))
				{
					var other = await renewals.GetAsync(x => x.Id == sibling.RenewalCaseId && x.TenantId == CurrentTenant.Id);
					if (other.Status == "PendingChecklist" && (await readiness.GetIssuesAsync(other, provisioning: false)).Count > 0) throw new BusinessException("Students:Records:BatchHeld");
				}
			}
			var issues = await readiness.GetIssuesAsync(item, provisioning: false);
			if (issues.Count > 0) throw new BusinessException("Students:Records:RenewalChecklistRequired").WithData("Issues", string.Join(" ", issues.Select(x => x.Message)));
			var before = JsonSerializer.Deserialize<StudentRecordTransitionSnapshot>(item.SourceSnapshotJson)!;
			// Access reads may be detached. Mutate the canonical tracked aggregate that
			// the owner readiness checks loaded in this same transaction.
			var student = await students.GetRegistrationAggregateAsync(item.StudentId)
				?? throw new BusinessException("Students:Records:EnrollmentMissing");
			var source = await enrollments.GetAsync(before.EnrollmentId);
			if (StudentRecordTransitionReadinessService.Capture(student, source) != item.SourceSnapshotJson) throw new BusinessException("Students:Records:StaleSnapshot");
			var sameYearReturn = approved.Outcome == "Return" && before.AcademicYearId == item.AcademicYearId;
			var standard = await placement.ValidateTargetAsync(item.StudentId, before.AcademicYearId, item.AcademicYearId, item.StandardId, item.DivisionId, item.RollNumber, approved.Outcome == "Return");
			await placement.ValidateRollRuleAsync(item, approved.GetOnboardingRollRule() ?? "null", input.Remarks);
			var attendanceActivationDate = ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
			var enrollment = await placement.ActivateTargetEnrollmentAsync(item.StudentId, item.AcademicYearId, item.StandardId, item.DivisionId, item.RollNumber, sameYearReturn, attendanceActivationDate);
			if (source.Id != enrollment.Id) { source.Deactivate(attendanceActivationDate); await enrollments.UpdateAsync(source); }
			var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
			student.UpdateAcademicPlacement(item.AcademicYearId, medium.EducationBoardId, medium.Id, stage.Id, item.StandardId, item.DivisionId);
			student.AssignRollNo(item.RollNumber); student.ChangeOperationalStatus(StudentStatus.Active, "Approved year-specific renewal activation");
			await students.UpdateAsync(student); item.Activate(enrollment.Id); await renewals.UpdateAsync(item, autoSave: true);
			await AddEventAsync(item, "RenewalActivated", input.Remarks);
			return;
		}
		if (item.Status != "Provisioning") throw new BusinessException("Students:Records:InvalidRenewalState");
		var provisioning = await readiness.GetIssuesAsync(item, provisioning: true);
		if (provisioning.Count > 0) throw new BusinessException("Students:Records:RenewalProvisioningRequired").WithData("Issues", string.Join(" ", provisioning.Select(x => x.Message)));
		item.Complete(); await renewals.UpdateAsync(item);
		await AddEventAsync(item, "RenewalCompleted", input.Remarks);
		if (item.TransitionId.HasValue)
		{
			var transition = await transitions.GetAsync(item.TransitionId.Value);
			var student = await students.GetAsync(item.StudentId); var enrollment = await enrollments.GetAsync(item.StudentEnrollmentId!.Value);
			transition.Apply(enrollment.Id, StudentRecordTransitionReadinessService.Capture(student, enrollment), Clock.Now);
			await transitions.UpdateAsync(transition, autoSave: true);
		}
	}
	private async Task AddEventAsync(StudentRecordRenewalCase item, string action, string remarks)
	{
		if (item.TransitionId.HasValue) await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, item.TransitionId.Value,
			CurrentUser.Id!.Value, action, 0, remarks));
	}
	private static void Match(StudentRecordRenewalCase item, string stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || item.ConcurrencyStamp != stamp) throw new BusinessException("Students:Records:ConcurrencyConflict");
	}
}
