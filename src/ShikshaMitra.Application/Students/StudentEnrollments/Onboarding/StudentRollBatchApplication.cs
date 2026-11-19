using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Only called inside the batch command's serializable transaction, after full-cohort revalidation.</summary>
[RemoteService(false)]
public class StudentRollBatchApplication(IStudentRepository students, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentOnboardingCase, Guid> cases, IRepository<StudentOnboardingStep, Guid> steps,
	IRepository<StudentOnboardingEvent, Guid> events, IRepository<StudentRecordChange, Guid> changes,
	IRepository<StudentRecordChangeDecision, Guid> decisions, StudentOnboardingRollRuleReader sequence,
	IRepository<Division, Guid> divisions, AttendancePolicyResolver attendancePolicies) : ApplicationService, ITransientDependency
{
	public async Task ApplyAsync(StudentRollNumberBatch batch)
	{
		if (CurrentUnitOfWork == null || batch.Status != "Approved") throw new BusinessException(StudentRollNumberErrorCodes.InvalidState);
		var rows = batch.Rows().Where(x => x.CurrentRollNumber != x.ProposedRollNumber).ToList();
		var enrollmentIds = rows.Where(x => x.EnrollmentId.HasValue).Select(x => x.EnrollmentId!.Value).ToArray();
		var enrolled = await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(enrollmentIds, x.Id));
		// The unique active-division index is immediate. Clear only these validated slots
		// inside this transaction, then install the complete final mapping before commit.
		foreach (var enrollment in enrolled)
		{
			enrollment.StageRollReassignment(rows.Single(x => x.EnrollmentId == enrollment.Id).CurrentRollNumber);
			await enrollments.UpdateAsync(enrollment, false);
		}
		await CurrentUnitOfWork.SaveChangesAsync();
		var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var attendanceDate = AttendanceSchoolDate.Today((await attendancePolicies.ResolveAsync(batch.AcademicYearId, Clock.Now.Date)).Options, Clock.Now);
		var division = await divisions.GetAsync(x => x.TenantId == CurrentTenant.Id && x.Id == batch.DivisionId);
		var groupLocked = division.ExtraProperties.TryGetValue($"IdentifierGroupLocked:{batch.AcademicYearId:N}:roll", out var value) && bool.TryParse(value?.ToString(), out var locked) && locked;
		foreach (var row in rows)
		{
			var number = row.ProposedRollNumber ?? throw new BusinessException(StudentRollNumberErrorCodes.BatchBlocked);
			var student = await students.GetAsync(x => x.Id == row.StudentId && x.TenantId == CurrentTenant.Id);
			if (row.EnrollmentId.HasValue)
			{
				var enrollment = enrolled.Single(x => x.Id == row.EnrollmentId);
				enrollment.ChangePlacement(batch.StandardId, batch.DivisionId, number, attendanceDate);
				student.AssignRollNo(number);
				if (groupLocked) student.SetIdentifierLocked("roll", true);
				await enrollments.UpdateAsync(enrollment, false); await students.UpdateAsync(student, false);
				foreach (var item in await cases.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentEnrollmentId == enrollment.Id && x.Status != StudentOnboardingStatus.Completed && x.Status != StudentOnboardingStatus.Cancelled))
				{
					item.SynchronizeProvisioningRoll(enrollment.Id, number); await cases.UpdateAsync(item, false);
				}
				await AuditRecordAsync(batch, row, now);
			}
			else
			{
				var item = await cases.GetAsync(x => x.Id == row.CaseId && x.TenantId == CurrentTenant.Id);
				item.RecordIdentifier(item.IdentifierAllocationId, number, Clock.Now);
				if (groupLocked) { item.SetIdentifierLocked("roll", true); student.SetIdentifierLocked("roll", true); await students.UpdateAsync(student, false); }
				var checklist = await steps.GetListAsync(x => x.OnboardingCaseId == item.Id);
				var review = checklist.FirstOrDefault(x => x.Key == StudentOnboardingStepKeys.ApprovalReview);
				if (review != null) { review.Reset("Roll numbering changed; review readiness again."); await steps.UpdateAsync(review, false); }
				var identifiers = checklist.FirstOrDefault(x => x.Key == StudentOnboardingStepKeys.IdentifiersAndRoll);
				if (identifiers != null)
				{
					if (item.IdentifierAllocationId.HasValue || !string.IsNullOrWhiteSpace(student.GrNumber) || student.GrNo.HasValue)
						identifiers.Complete(CurrentUser.Id, Clock.Now, "GR and Roll numbers prepared for activation.");
					else identifiers.Reset("Prepare the pending GR number.");
					await steps.UpdateAsync(identifiers, false);
				}
				await cases.UpdateAsync(item, false);
				await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id, item.Id, "RollNumberPrepared",
					$"Roll number {number} prepared through a reviewed division batch.", JsonSerializer.Serialize(new { batchId = batch.Id, oldRoll = row.CurrentRollNumber, newRoll = number, batch.Reason })), false);
			}
		}
		await sequence.RecordIssuedAsync(batch.AcademicYearId, batch.StandardId, batch.DivisionId,
			batch.Rows().Select(x => x.ProposedRollNumber ?? 0).DefaultIfEmpty(0).Max(), reset: batch.Operation == "renumber");
		batch.Apply(now);
	}
	private async Task AuditRecordAsync(StudentRollNumberBatch batch, StudentRollBatchRow row, DateTimeOffset now)
	{
		string Placement(int? roll) => JsonSerializer.Serialize(new StudentRecordPlacementPayload { AcademicYearId = batch.AcademicYearId, StandardId = batch.StandardId, DivisionId = batch.DivisionId, RollNumber = roll }, StudentRecordPayloadService.JsonOptions);
		var history = batch.Decisions().Where(x => x.Approved).ToList();
		Guid? policyVersion = batch.ExtraProperties.TryGetValue("PolicyVersionId", out var savedVersion) && Guid.TryParse(savedVersion?.ToString(), out var version) ? version : null;
		var change = new StudentRecordChange(GuidGenerator.Create(), CurrentTenant.Id, row.StudentId, batch.AcademicYearId, batch.RequestedBy,
			"placement", "Placement", "roll-batch:" + batch.Id.ToString("N"), string.IsNullOrWhiteSpace(batch.Reason) ? "Assign missing roll through reviewed division batch" : batch.Reason,
			Placement(row.CurrentRollNumber), Placement(row.ProposedRollNumber), batch.PolicyJson, policyVersion, batch.ApprovalStagesJson, history.Count, now);
		change.ExtraProperties["RollNumberBatchId"] = batch.Id.ToString("D");
		foreach (var decision in history)
		{
			change.Approve(decision.ActorId);
			await decisions.InsertAsync(new StudentRecordChangeDecision(GuidGenerator.Create(), CurrentTenant.Id, change.Id, decision.ActorId, decision.Stage, "Approved", decision.Reason), false);
		}
		change.Apply(now); await changes.InsertAsync(change, false);
	}
}
