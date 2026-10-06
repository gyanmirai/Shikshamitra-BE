using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordTransitionCorrectionService(IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<StudentEnrollment, Guid> enrollments, IStudentRepository students, StudentRecordsAccessService access,
	IRepository<StudentFeeStructure, Guid> feeAssignments, IRepository<AttendanceEntry, Guid> attendance,
	IRepository<StudentCertificate, Guid> certificates, IRepository<StudentIdCard, Guid> cards,
	StudentRecordTransitionReadinessService placement, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	public async Task<StudentRecordTransitionPreviewRowDto> PreviewAsync(StudentRecordTransition original)
	{
		var row = new StudentRecordTransitionPreviewRowDto { StudentId = original.StudentId, Outcome = "Correction" };
		if (original.ReturnHandoffId.HasValue)
		{
			StudentRecordTransitionReadinessService.Add(row, "DownstreamDependency", "The returning Admission and Onboarding owners must resolve their dependencies before this return can be corrected.");
			return row;
		}
		var student = (await access.EnsureWritableAsync(original.StudentId, placement: true)).Student;
		if (original.Status != "Applied" || original.CorrectionOfId.HasValue || original.AfterJson == null)
			StudentRecordTransitionReadinessService.Add(row, "InvalidChangeState", "Only an applied original transition can be corrected.");
		if (await transitions.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.CorrectionOfId == original.Id && x.Status != "Rejected" && x.Status != "Cancelled"))
			StudentRecordTransitionReadinessService.Add(row, "CorrectionAlreadyExists", "An existing correction already governs this transition.");
		var currentEnrollment = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.AcademicYearId == student.AcademicYearId);
		if (currentEnrollment == null || StudentRecordTransitionReadinessService.Capture(student, currentEnrollment) != original.AfterJson)
			StudentRecordTransitionReadinessService.Add(row, "StaleSnapshot", "Placement or status changed after this transition. Correct the later dependency first.");
		if (original.TargetEnrollmentId.HasValue && await feeAssignments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.AcademicYearId == currentEnrollment!.AcademicYearId))
			StudentRecordTransitionReadinessService.Add(row, "FinancialDependency", "Finance owns the new-year assignment. Records cannot reverse financial obligations.");
		if (original.AppliedAt.HasValue && (await attendance.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.CreationTime >= original.AppliedAt) ||
			await HasActiveDocumentDependencyAsync(original)))
			StudentRecordTransitionReadinessService.Add(row, "DownstreamDependency", "Attendance or issued documents depend on this transition. Resolve them in their owning module first.");
		row.StudentConcurrencyStamp = student.ConcurrencyStamp;
		row.EnrollmentConcurrencyStamp = currentEnrollment?.ConcurrencyStamp ?? "";
		row.Ready = row.Issues.Count == 0; return row;
	}
	public async Task ApplyAsync(StudentRecordTransition correction)
	{
		var original = await transitions.GetAsync(x => x.Id == correction.CorrectionOfId && x.TenantId == CurrentTenant.Id);
		// The current request is excluded from the duplicate check while revalidating its impact.
		var student = (await access.EnsureWritableAsync(original.StudentId, placement: true)).Student;
		var after = JsonSerializer.Deserialize<StudentRecordTransitionSnapshot>(original.AfterJson!)!;
		var current = await enrollments.GetAsync(x => x.Id == after.EnrollmentId && x.TenantId == CurrentTenant.Id);
		if (StudentRecordTransitionReadinessService.Capture(student, current) != original.AfterJson || correction.BeforeJson != original.AfterJson)
			throw new BusinessException("Students:Records:StaleSnapshot");
		if (original.TargetEnrollmentId.HasValue && await feeAssignments.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.AcademicYearId == after.AcademicYearId))
			throw new BusinessException("Students:Records:FinancialDependency");
		if (await attendance.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.CreationTime >= original.AppliedAt) ||
			await HasActiveDocumentDependencyAsync(original))
			throw new BusinessException("Students:Records:DownstreamDependency");
		var before = JsonSerializer.Deserialize<StudentRecordTransitionSnapshot>(original.BeforeJson)!;
		var source = await enrollments.GetAsync(x => x.Id == before.EnrollmentId && x.TenantId == CurrentTenant.Id);
		if (before.EnrollmentActive && before.DivisionId.HasValue && (!source.IsActive || current.Id != source.Id))
			await placement.ValidateCapacityAsync(student.Id, before.AcademicYearId, before.DivisionId.Value, before.RollNumber, (await divisions.GetAsync(x => x.Id == before.DivisionId && x.TenantId == CurrentTenant.Id)).Strength);
		if (current.Id != source.Id) { current.Deactivate(correction.EffectiveDate); await enrollments.UpdateAsync(current); }
		if (before.EnrollmentActive) source.Activate(correction.EffectiveDate); else source.Deactivate(correction.EffectiveDate);
		await enrollments.UpdateAsync(source);
		student.UpdateAcademicPlacement(before.AcademicYearId, before.BoardId, before.MediumId, before.StageId, before.StandardId, before.DivisionId);
		student.AssignRollNo(before.RollNumber);
		student.ChangeOperationalStatus(before.StudentStatus == StudentLifeCycleStatusEnum.Left ? StudentStatus.Left : before.StudentStatus == StudentLifeCycleStatusEnum.InActive ? StudentStatus.Inactive : StudentStatus.Active, correction.Reason);
		await students.UpdateAsync(student);
		await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Portal.StudentPortalLifecycleService>().RestoreCorrectionAsync(original, student);
		correction.Apply(source.Id, StudentRecordTransitionReadinessService.Capture(student, source), Clock.Now);
	}
	private async Task<bool> HasActiveDocumentDependencyAsync(StudentRecordTransition original) =>
		await certificates.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.Status != StudentCertificateStatus.Cancelled && (x.TransitionId == original.Id || x.CreationTime >= original.AppliedAt)) ||
		await cards.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == original.StudentId && x.Status != "Voided" && x.CreationTime >= original.AppliedAt);
}
