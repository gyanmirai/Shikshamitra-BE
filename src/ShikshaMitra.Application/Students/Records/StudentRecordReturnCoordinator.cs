using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordReturnCoordinator(StudentRecordsAccessService access, StudentRecordsPolicyResolver policies,
	StudentRecordTransitionReadinessService readiness,
	IRepository<StudentRecordTransitionBatch, Guid> batches, IRepository<StudentRecordTransition, Guid> transitions, StudentRecordAdmissionReturnService admissionReturns)
	: ApplicationService, ITransientDependency
{
	public async Task<StudentRecordTransitionPreviewRowDto> PreviewAsync(StudentRecordReturnInput input)
	{
		var student = (await access.EnsureReadableAsync(input.StudentId)).Student;
		if (!student.AcademicYearId.HasValue) throw new BusinessException("Students:Records:InvalidTransition");
		var policy = await policies.ResolveAsync(student.AcademicYearId);
		var route = input.Route ?? policy.Policy.Transitions.DefaultReturnRoute;
		if (route != "ReuseProfile" || !policy.Policy.Transitions.AllowedReturnRoutes.Contains(route)) throw new BusinessException("Students:Records:ReturnRouteNotAllowed");
		return await readiness.PreviewAsync(student.AcademicYearId.Value, Command(input), policy.Policy, false);
	}
	private static StudentRecordTransitionInput Command(StudentRecordReturnInput input) => new()
	{
		StudentId = input.StudentId, Outcome = "Return", Reason = input.Reason, EffectiveDate = input.EffectiveDate ?? AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow),
		TargetAcademicYearId = input.TargetAcademicYearId, TargetStandardId = input.TargetStandardId, TargetDivisionId = input.TargetDivisionId,
		RollNumber = input.RollNumber, RollAssignmentMode = input.RollAssignmentMode, StudentConcurrencyStamp = input.StudentConcurrencyStamp ?? "", EnrollmentConcurrencyStamp = input.EnrollmentConcurrencyStamp ?? ""
	};
	public async Task<StudentRecordReturnDto> ReturnAsync(StudentRecordReturnInput input, Func<StudentRecordTransitionBatchInput, Task<StudentRecordTransitionBatchResultDto>> submit)
	{
		try { return await ReturnCoreAsync(input, submit); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			throw new BusinessException("Students:Records:ConcurrencyConflict");
		}
	}
	private async Task<StudentRecordReturnDto> ReturnCoreAsync(StudentRecordReturnInput input, Func<StudentRecordTransitionBatchInput, Task<StudentRecordTransitionBatchResultDto>> submit)
	{
		var student = (await access.EnsureReadableAsync(input.StudentId)).Student;
		var replay = await admissionReturns.ReplayAsync(input);
		if (replay != null) return replay;
		var previousBatch = await batches.FindAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey);
		if (previousBatch != null)
		{
			var prior = await transitions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == previousBatch.Id && x.StudentId == student.Id && x.Outcome == "Return");
			var command = prior == null ? null : JsonSerializer.Deserialize<StudentRecordTransitionInput>(prior.InputJson);
			if (prior == null || command!.TargetAcademicYearId != input.TargetAcademicYearId || command.TargetStandardId != input.TargetStandardId || command.TargetDivisionId != input.TargetDivisionId || command.RollNumber != input.RollNumber || command.RollAssignmentMode != input.RollAssignmentMode || command.Reason != input.Reason || (input.Route != null && input.Route != "ReuseProfile") || input.EffectiveDate.HasValue && command.EffectiveDate.Date != input.EffectiveDate.Value.Date)
				throw new BusinessException("Students:Records:IdempotencyConflict");
			return new() { StudentId = student.Id, Route = "ReuseProfile", TransitionId = prior.Id, RenewalCaseId = prior.RenewalCaseId, Status = prior.Status };
		}
		await access.EnsureWritableAsync(input.StudentId, placement: true);
		if (student.Status != StudentLifeCycleStatusEnum.Left || !student.AcademicYearId.HasValue || input.TargetAcademicYearId == Guid.Empty || string.IsNullOrWhiteSpace(input.IdempotencyKey) || string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException("Students:Records:InvalidTransition");
		var policy = await policies.ResolveAsync(student.AcademicYearId); var route = input.Route ?? policy.Policy.Transitions.DefaultReturnRoute;
		if (!policy.Policy.Transitions.AllowedReturnRoutes.Contains(route)) throw new BusinessException("Students:Records:ReturnRouteNotAllowed");

		if (route == "ReuseProfile")
		{
			var source = await readiness.GetSourceAsync(student.Id, student.AcademicYearId.Value);
			var command = Command(input);
			// Existing manual callers remain compatible; new clients send the preview stamps.
			command.StudentConcurrencyStamp = input.StudentConcurrencyStamp ?? source.Student.ConcurrencyStamp;
			command.EnrollmentConcurrencyStamp = input.EnrollmentConcurrencyStamp ?? source.Enrollment.ConcurrencyStamp;
			var result = await submit(new() { AcademicYearId = source.Enrollment.AcademicYearId, IdempotencyKey = input.IdempotencyKey, Items = [command] });
			var transition = result.Items.Single(); return new() { StudentId = student.Id, Route = route, TransitionId = transition.Id, RenewalCaseId = transition.RenewalCaseId, Status = transition.Status };
		}
		return await admissionReturns.CreateAsync(input, route);
	}
}
