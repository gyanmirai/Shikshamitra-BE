using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceScheduleAppService(IRepository<StudentAttendanceSchedule, Guid> schedules,
	IRepository<StudentEnrollment, Guid> enrollments, IRepository<AcademicYear, Guid> years,
	StudentScopeAccessService scope, AttendancePolicyResolver policies) : ApplicationService, IAttendanceScheduleAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<ListResultDto<AttendanceScheduleDto>> GetListAsync(Guid enrollmentId)
	{
		await EnrollmentAsync(enrollmentId);
		var rows = await schedules.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.EnrollmentId == enrollmentId);
		return new(ObjectMapper.Map<List<StudentAttendanceSchedule>, List<AttendanceScheduleDto>>(rows.OrderByDescending(x => x.StartDate).ToList()));
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	[UnitOfWork(true)]
	public async Task<AttendanceScheduleDto> SaveAsync(SaveAttendanceScheduleDto input)
	{
		var enrollment = await EnrollmentAsync(input.EnrollmentId);
		var year = await AsyncExecuter.SingleOrDefaultAsync((await years.GetQueryableAsync()).Where(y => y.Id == enrollment.AcademicYearId)
			.Select(y => new { y.StartDate, y.EndDate })) ?? throw new BusinessException(AttendancePolicyErrorCodes.InvalidSchedule);
		if (!enrollment.IsActive || input.StartDate.Date < year.StartDate.Date || input.StartDate.Date > year.EndDate.Date || input.EndDate?.Date > year.EndDate.Date)
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidSchedule);
		var existing = input.Id.HasValue ? await schedules.GetAsync(input.Id.Value) : null;
		if (existing != null && (existing.EnrollmentId != enrollment.Id || existing.TenantId != CurrentTenant.Id || existing.ConcurrencyStamp != input.ConcurrencyStamp))
			throw new BusinessException(AttendancePolicyErrorCodes.ConfigurationChanged);
		if (existing == null && input.ConcurrencyStamp != null) throw new BusinessException(AttendancePolicyErrorCodes.ConfigurationChanged);
		var today = await TodayAsync(enrollment.AcademicYearId);
		// Replacements are dated versions: an old schedule's historical applicability is never rewritten.
		if (existing != null && (existing.RetiredFrom.HasValue || input.StartDate.Date < today))
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidSchedule).WithData("Reason", "Choose an effective date from today for a replacement schedule");
		var candidate = new StudentAttendanceSchedule(GuidGenerator.Create(), CurrentTenant.Id!.Value, enrollment.Id, enrollment.AcademicYearId,
			input.Name, input.Kind, input.StartDate, input.EndDate, input.WeekDays, input.StartTime, input.EndTime);
		var others = await schedules.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.EnrollmentId == enrollment.Id && x.Id != input.Id);
		if (others.Any(candidate.Overlaps)) throw new BusinessException(AttendancePolicyErrorCodes.ScheduleOverlap);
		// Updating the enrollment's revision serializes concurrent schedule changes in the same transactional unit of work.
		await enrollments.UpdateAsync(enrollment, autoSave: true);
		if (existing != null) { existing.Retire(input.StartDate); await schedules.UpdateAsync(existing); }
		await schedules.InsertAsync(candidate, autoSave: true);
		return ObjectMapper.Map<StudentAttendanceSchedule, AttendanceScheduleDto>(candidate);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete)]
	[UnitOfWork(true)]
	public async Task DeleteAsync(Guid id, string concurrencyStamp)
	{
		var existing = await schedules.GetAsync(id); var enrollment = await EnrollmentAsync(existing.EnrollmentId);
		if (existing.TenantId != CurrentTenant.Id || existing.ConcurrencyStamp != concurrencyStamp || existing.RetiredFrom.HasValue)
			throw new BusinessException(AttendancePolicyErrorCodes.ConfigurationChanged);
		await enrollments.UpdateAsync(enrollment, autoSave: true);
		existing.Retire(await TodayAsync(enrollment.AcademicYearId)); await schedules.UpdateAsync(existing, autoSave: true);
	}
	private async Task<StudentEnrollment> EnrollmentAsync(Guid id)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		var enrollment = await enrollments.GetAsync(id);
		if (enrollment.TenantId != CurrentTenant.Id || !enrollment.StandardId.HasValue || !enrollment.DivisionId.HasValue)
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidSchedule);
		await scope.EnsureCanAccessAttendanceScopeAsync(enrollment.AcademicYearId, enrollment.StandardId.Value, enrollment.DivisionId.Value, "AttendanceSchedule");
		return enrollment;
	}
	private async Task<DateTime> TodayAsync(Guid year)
	{
		var p = await policies.ResolveAsync(year, Clock.Now.Date);
		return TimeZoneInfo.ConvertTimeFromUtc(Clock.Now.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById(p.Options.SchoolTimeZone)).Date;
	}
}
