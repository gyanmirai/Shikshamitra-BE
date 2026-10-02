using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentRecordPlacementService(
	IStudentRepository students, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	IRepository<StudentOnboardingCase, Guid> cases, IRepository<StudentSeatReservation, Guid> reservations,
	IRepository<StudentRecordRenewalCase, Guid> renewals,
	StudentScopeAccessService scope, StudentRecordsAccessService access, OnboardingFeeReadinessReader finance, StudentRecordsEnrollmentScopeService recordScope,
	AttendancePolicyResolver attendancePolicies) : ApplicationService, ITransientDependency
{
	public async Task<StudentRecordPlacementPayload> GetAsync(Guid studentId)
	{
		var student = await access.GetScopedStudentAsync(studentId);
		var enrollment = await CurrentEnrollmentAsync(student);
		return new() { AcademicYearId = enrollment.AcademicYearId, StandardId = enrollment.StandardId ?? Guid.Empty,
			DivisionId = enrollment.DivisionId ?? Guid.Empty, RollNumber = enrollment.RollNumber };
	}
	public async Task ApplyAsync(Guid studentId, StudentRecordPlacementPayload input)
	{
		var allowed = await access.EnsureWritableAsync(studentId, placement: true);
		var student = allowed.Student;
		var enrollment = await CurrentEnrollmentAsync(student);
		if (enrollment.AcademicYearId != input.AcademicYearId || !enrollment.IsActive)
			throw new BusinessException("Students:Records:UseYearEndTransition");
		await scope.EnsureCanAccessStandardAsync(input.StandardId, "change student placement");
		await scope.EnsureCanAccessDivisionAsync(input.DivisionId, "change student placement");
		await recordScope.EnsureTargetPlacementAsync(input.AcademicYearId, input.StandardId, input.DivisionId);
		var standard = await standards.GetAsync(input.StandardId, includeDetails: true);
		var division = await divisions.GetAsync(input.DivisionId);
		if (standard.TenantId != CurrentTenant.Id || division.TenantId != CurrentTenant.Id || division.StandardId != standard.Id ||
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
		await EnsureClassFinancialImpactAsync(enrollment, input.StandardId);
		var now = Clock.Now.ToUniversalTime();
		var occupied = await enrollments.CountAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId != studentId &&
			e.AcademicYearId == input.AcademicYearId && e.DivisionId == input.DivisionId && e.IsActive);
		var reserved = await reservations.CountAsync(r => r.TenantId == CurrentTenant.Id && r.AcademicYearId == input.AcademicYearId &&
			r.DivisionId == input.DivisionId && r.Status == StudentSeatReservationStatus.Reserved && r.ExpiresAt > now);
		if (occupied + reserved >= division.Strength) throw new BusinessException(StudentOnboardingErrorCodes.DivisionFull);
		if (input.RollNumber.HasValue && await enrollments.AnyAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId != studentId &&
			e.AcademicYearId == input.AcademicYearId && e.DivisionId == input.DivisionId && e.IsActive && e.RollNumber == input.RollNumber))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
		if (input.RollNumber.HasValue && await cases.AnyAsync(c => c.TenantId == CurrentTenant.Id && c.StudentId != studentId &&
			c.AcademicYearId == input.AcademicYearId && c.DivisionId == input.DivisionId && c.RollNumber == input.RollNumber &&
			c.Status != StudentOnboardingStatus.Completed && c.Status != StudentOnboardingStatus.Cancelled))
			throw new BusinessException(StudentOnboardingErrorCodes.RollNumberDuplicate);
		var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
		var provisioningCase = await cases.FindAsync(c => c.TenantId == CurrentTenant.Id && c.StudentId == studentId &&
			c.StudentEnrollmentId == enrollment.Id && c.Status == StudentOnboardingStatus.Provisioning);
		if (provisioningCase != null)
		{
			if (!input.RollNumber.HasValue) throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
			var item = provisioningCase;
			if (item.SeatReservationId.HasValue)
			{
				var old = await reservations.GetAsync(r => r.Id == item.SeatReservationId.Value && r.TenantId == CurrentTenant.Id);
				old.ReleaseCommittedForRecordChange("Approved Records placement change", now);
				await reservations.UpdateAsync(old);
			}
			var seat = new StudentSeatReservation(GuidGenerator.Create(), CurrentTenant.Id, item.Id,
				input.AcademicYearId, standard.Id, division.Id, occupied + reserved + 1, now, now.AddMinutes(1));
			seat.Commit(now);
			await reservations.InsertAsync(seat);
			item.ChangeProvisioningPlacement(medium.EducationBoardId, medium.Id, stage.Id, standard.Id, division.Id, seat.Id, input.RollNumber.Value);
			await cases.UpdateAsync(item);
		}
		var renewal = await renewals.FindAsync(r => r.TenantId == CurrentTenant.Id && r.StudentId == studentId &&
			r.AcademicYearId == input.AcademicYearId && r.StudentEnrollmentId == enrollment.Id && r.Status == "Provisioning");
		if (renewal != null)
		{
			renewal.ChangeProvisioningPlacement(standard.Id, division.Id, input.RollNumber);
			await renewals.UpdateAsync(renewal);
		}
		var attendanceDate = AttendanceSchoolDate.Today((await attendancePolicies.ResolveAsync(enrollment.AcademicYearId, Clock.Now.Date)).Options, Clock.Now);
		enrollment.ChangePlacement(standard.Id, division.Id, input.RollNumber, attendanceDate);
		student.UpdateAcademicPlacement(input.AcademicYearId, medium.EducationBoardId, medium.Id, stage.Id, standard.Id, division.Id);
		student.AssignRollNo(input.RollNumber);
		await enrollments.UpdateAsync(enrollment);
		await students.UpdateAsync(student);
	}
	public async Task ValidateImpactAsync(Guid studentId, StudentRecordPlacementPayload input)
	{
		var allowed = await access.EnsureWritableAsync(studentId, placement: true);
		var enrollment = await CurrentEnrollmentAsync(allowed.Student);
		if (enrollment.AcademicYearId != input.AcademicYearId) throw new BusinessException("Students:Records:UseYearEndTransition");
		await scope.EnsureCanAccessStandardAsync(input.StandardId, "validate placement class");
		await scope.EnsureCanAccessDivisionAsync(input.DivisionId, "validate placement division");
		await recordScope.EnsureTargetPlacementAsync(input.AcademicYearId, input.StandardId, input.DivisionId);
		await EnsureClassFinancialImpactAsync(enrollment, input.StandardId);
	}
	private async Task EnsureClassFinancialImpactAsync(StudentEnrollment enrollment, Guid targetStandardId)
	{
		if (enrollment.StandardId != targetStandardId && await finance.HasAssignmentAsync(enrollment.StudentId, enrollment.AcademicYearId))
			throw new BusinessException("Students:Records:FinancialPlacementImpactRequired");
	}
	public async Task ApplyStatusAsync(Guid studentId, StudentRecordStatusPayload input)
	{
		var student = (await access.EnsureWritableAsync(studentId)).Student;
		if (input.Status is not (StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive) ||
			student.Status is not (StudentLifeCycleStatusEnum.Enrolled or StudentLifeCycleStatusEnum.InActive))
			throw new BusinessException("Students:Records:UseExitOrReturnTransition");
		student.ChangeOperationalStatus(input.Status == StudentLifeCycleStatusEnum.Enrolled ? StudentStatus.Active : StudentStatus.Inactive,
			"Student Records governed status change");
		var enrollment = await CurrentEnrollmentAsync(student);
		var attendanceDate = AttendanceSchoolDate.Today((await attendancePolicies.ResolveAsync(enrollment.AcademicYearId, Clock.Now.Date)).Options, Clock.Now);
		if (input.Status == StudentLifeCycleStatusEnum.Enrolled)
		{
			enrollment.Activate(attendanceDate);
			await ApplyAsync(studentId, new StudentRecordPlacementPayload { AcademicYearId = enrollment.AcademicYearId,
				StandardId = enrollment.StandardId ?? Guid.Empty, DivisionId = enrollment.DivisionId ?? Guid.Empty, RollNumber = enrollment.RollNumber });
		}
		else enrollment.Deactivate(attendanceDate);
		await enrollments.UpdateAsync(enrollment);
		await students.UpdateAsync(student);
	}
	private async Task<StudentEnrollment> CurrentEnrollmentAsync(Student student) =>
		await enrollments.FindAsync(e => e.TenantId == CurrentTenant.Id && e.StudentId == student.Id && e.AcademicYearId == student.AcademicYearId)
		?? throw new BusinessException("Students:Records:EnrollmentMissing");
}
