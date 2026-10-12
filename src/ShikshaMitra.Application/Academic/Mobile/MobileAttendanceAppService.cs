using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileAttendanceAppService(MobileAcademicContextAppService context,
	IRepository<AttendanceEntry, Guid> entries, IRepository<AttendanceDay, Guid> days,
	IRepository<StudentLeave, Guid> leaves, IRepository<StudentLeaveType, Guid> leaveTypes,
	StudentLeaveManager leaveManager, MobileCalendarAppService calendar, AttendancePolicyResolver policy,
	AttendanceApplicabilityResolver applicability, MobileAttachmentStore files, IMobileLeaveRequestLock leaveLock) : ApplicationService, IMobileAttendanceAppService
{
	private static DateTime SchoolDate(DateTimeOffset date) => date.ToOffset(TimeSpan.FromHours(5.5)).Date;
	private record RecordedDay(DateTime Date, AttendanceEntry Entry);
	private async Task<List<RecordedDay>> ReadEntriesAsync(MobilePlacement p, DateTime start, DateTime end)
	{
		var query = from entry in await entries.GetQueryableAsync()
			join day in await days.GetQueryableAsync() on entry.AttendanceDayId equals day.Id
			where entry.TenantId == CurrentTenant.Id && day.TenantId == CurrentTenant.Id && entry.StudentId == p.Student.Id &&
				day.AcademicYearId == p.Year.Id && day.IsSubmitted && day.Date >= start && day.Date <= end && day.Date <= MobileAcademicContextAppService.SchoolToday
			select new RecordedDay(day.Date, entry);
		return await AsyncExecuter.ToListAsync(query);
	}

	public async Task<MobileAttendanceOverviewDto> GetOverviewAsync(MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var records = (await ReadEntriesAsync(p, p.Year.StartDate, p.Year.EndDate)).Select(x => x.Entry).ToList();
		var counted = records.Where(x => !x.ExcludedFromPercentage && x.Status != AttendanceStatus.Pending).ToList();
		var denominator = counted.Sum(x => x.DenominatorCredit);
		var credit = counted.Sum(x => x.PresentCredit ?? (x.Status == AttendanceStatus.Present ? (x.HalfDay ? .5 : 1) : 0));
		var options = (await policy.ResolveAsync(p.Year.Id, MobileAcademicContextAppService.SchoolToday)).Options;
		return new(p.Student.Id, p.Year.Id, records.Count(x => x.Status == AttendanceStatus.Present && !x.HalfDay),
			records.Count(x => x.Status == AttendanceStatus.Absent && !x.ApprovedLeaveId.HasValue), records.Count(x => x.HalfDay), records.Count(x => x.Late),
			denominator == 0 ? null : Math.Round(credit / denominator * 100, 2), (double)options.LowAttendanceThreshold);
	}

	public async Task<List<MobileAttendanceDayDto>> GetCalendarAsync(MobileCalendarInput input)
	{
		var p = await context.ResolveAsync(input);
		if (input.Year is < 1900 or > 9998 || input.Month is < 1 or > 12) throw MobileAcademicContextAppService.Invalid("Supply a valid month.");
		var start = new DateTime(input.Year, input.Month, 1);
		var end = start.AddMonths(1).AddDays(-1);
		var records = await ReadEntriesAsync(p, start, end);
		var markers = await calendar.ReadDaysAsync(p, start, end);
		var requests = (await leaves.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id &&
			x.Status != StudentLeaveStatusEnum.Cancelled && x.Status != StudentLeaveStatusEnum.Rejected)).Where(x =>
			x.LeaveDate < new DateTimeOffset(end.AddDays(1), TimeSpan.FromHours(5.5)) &&
			(x.EndDate ?? x.LeaveDate) >= new DateTimeOffset(start, TimeSpan.FromHours(5.5))).ToList();
		var result = new List<MobileAttendanceDayDto>();
		for (var date = start; date <= end; date = date.AddDays(1))
		{
			var entry = records.SingleOrDefault(x => x.Date.Date == date)?.Entry;
			var leave = requests.FirstOrDefault(x => SchoolDate(x.LeaveDate) <= date && SchoolDate(x.EndDate ?? x.LeaveDate) >= date);
			var closed = markers.FirstOrDefault(x => x.Date.Date == date && x.DayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure);
			var inside = date >= p.Year.StartDate.Date && date <= p.Year.EndDate.Date;
			var status = !inside ? "notMarkedYet" : closed != null ? "holiday" : entry?.ApprovedLeaveId != null || leave?.Status == StudentLeaveStatusEnum.Approved ? "approvedLeave" :
				entry?.Status == AttendanceStatus.Absent ? "absent" : entry?.Status == AttendanceStatus.Present ? entry.HalfDay ? "halfDay" : entry.Late ? "late" : "present" : "notMarkedYet";
			result.Add(new(DateOnly.FromDateTime(date), status, inside ? leave?.Id : null, inside ? closed?.Title ?? entry?.Remarks : null, inside ? entry?.GuardianAbsenceReason : null));
		}
		return result;
	}

	public async Task<MobileAttendanceDayDto> SetAbsenceReasonAsync(DateOnly date, MobileAbsenceInput input)
	{
		var p = await context.ResolveAsync(input);
		if (!p.IsGuardian) throw new AbpAuthorizationException();
		var value = date.ToDateTime(TimeOnly.MinValue);
		if (value < p.Year.StartDate.Date || value > p.Year.EndDate.Date) throw MobileAcademicContextAppService.Invalid("The date must be inside the academic year.");
		var entry = (await ReadEntriesAsync(p, value, value)).SingleOrDefault()?.Entry ?? throw new EntityNotFoundException(typeof(AttendanceEntry));
		entry.SetGuardianAbsenceReason(input.Reason);
		await entries.UpdateAsync(entry, autoSave: true);
		return new(date, "absent", entry.ApprovedLeaveId, entry.Remarks, entry.GuardianAbsenceReason);
	}

	public async Task<List<MobileLeaveTypeDto>> GetLeaveTypesAsync(MobileAcademicInput input)
	{
		await context.ResolveAsync(input);
		return (await leaveTypes.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.IsActive)).OrderBy(x => x.LeaveTypeName).Select(x => new MobileLeaveTypeDto(x.Id, x.LeaveTypeName)).ToList();
	}

	public async Task<MobilePage<MobileLeaveDto>> GetLeavesAsync(MobileListInput input)
	{
		var p = await context.ResolveAsync(input);
		var start = new DateTimeOffset(p.Year.StartDate.Date, TimeSpan.FromHours(5.5));
		var end = new DateTimeOffset(p.Year.EndDate.Date.AddDays(1), TimeSpan.FromHours(5.5));
		var list = (await leaves.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id)).Where(x => x.LeaveDate >= start && x.LeaveDate < end);
		var types = (await leaveTypes.GetListAsync(x => x.TenantId == CurrentTenant.Id)).ToDictionary(x => x.Id, x => x.LeaveTypeName);
		var rows = list.OrderByDescending(x => x.CreationTime).Select(x => Map(x, types.GetValueOrDefault(x.StudentLeaveTypeId, ""), p));
		return MobileAcademicContextAppService.Page(rows.Where(x => string.IsNullOrEmpty(input.Status) || x.Status == input.Status), input, p);
	}

	private async Task<StudentLeave> FindAsync(Guid id, MobilePlacement p)
	{
		var leave = await leaves.FindAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id);
		if (leave == null || SchoolDate(leave.LeaveDate) < p.Year.StartDate.Date || SchoolDate(leave.LeaveDate) > p.Year.EndDate.Date)
			throw new EntityNotFoundException(typeof(StudentLeave), id);
		return leave;
	}

	public async Task<MobileLeaveDto> GetLeaveAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var leave = await FindAsync(id, p);
		return Map(leave, (await leaveTypes.GetAsync(leave.StudentLeaveTypeId)).LeaveTypeName, p);
	}

	[UnitOfWork(isTransactional: true)]
	public async Task<MobileLeaveDto> SaveLeaveAsync(Guid? id, MobileLeaveInput input)
	{
		var p = await context.ResolveAsync(input);
		if (!p.IsGuardian) throw new AbpAuthorizationException();
		var start = input.StartDate.ToDateTime(TimeOnly.MinValue);
		await leaveLock.AcquireAsync(CurrentTenant.Id!.Value, p.Student.Id);
		var end = input.EndDate.ToDateTime(TimeOnly.MinValue);
		if (start < p.Year.StartDate.Date || end > p.Year.EndDate.Date || end < start || (end - start).Days > 30 || input.IsHalfDay && start != end || string.IsNullOrWhiteSpace(input.Description))
			throw MobileAcademicContextAppService.Invalid("Supply a valid leave range and reason within the enrolled year.");
		var type = await leaveTypes.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == input.LeaveTypeId && x.IsActive);
		if (type == null) throw MobileAcademicContextAppService.Invalid("Select an active leave type.");
		var leave = id.HasValue ? await FindAsync(id.Value, p) : null;
		if (leave != null && leave.Status != StudentLeaveStatusEnum.Pending) throw MobileAcademicContextAppService.Invalid("Only pending leave can be edited.");
		var startOffset = new DateTimeOffset(start, TimeSpan.FromHours(5.5));
		var endOffset = new DateTimeOffset(end, TimeSpan.FromHours(5.5));
		if ((await leaves.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id && x.Id != id &&
			(x.Status == StudentLeaveStatusEnum.Pending || x.Status == StudentLeaveStatusEnum.Approved))).Any(x => x.LeaveDate <= endOffset && (x.EndDate ?? x.LeaveDate) >= startOffset))
			throw MobileAcademicContextAppService.Invalid("The leave dates overlap an existing request.");
		var schoolDays = await applicability.LoadAsync(p.Year.Id, p.StandardId, p.DivisionId, start, end, []);
		var duration = 0m;
		for (var d = start; d <= end; d = d.AddDays(1))
		{
			var applicable = schoolDays.For(Guid.Empty, d);
			if (applicable.IsProvisional) throw MobileAcademicContextAppService.Invalid("Configure the school calendar before requesting leave.");
			if (applicable.IsExpected) duration += input.IsHalfDay ? .5m : 1m;
		}
		if (duration == 0) throw MobileAcademicContextAppService.Invalid("The range contains no school days.");
		var attachments = await files.ResolveAsync(p.Student.Id, Guid.Empty, input.AttachmentIds, leave?.MobileAttachmentsJson);
		var first = attachments.Count == 0 ? null : await files.DownloadAsync(attachments[0]);
		leave = leave == null
			? await leaveManager.CreateAsync(p.Student, input.LeaveTypeId, input.IsHalfDay, startOffset, input.IsHalfDay ? null : endOffset, input.Description, first?.Content, first?.Name, first?.ContentType)
			: await leaveManager.UpdateAsync(leave, input.LeaveTypeId, input.IsHalfDay, startOffset, input.IsHalfDay ? null : endOffset, input.Description, first?.Content, first?.Name, first?.ContentType);
		leave.SetHalfDayTimes(input.HalfDayStartTime, input.HalfDayEndTime);
		leave.SetSchoolDayDuration(duration);
		leave.SetEnrollmentPlacement(p.MediumId, p.StandardId, p.DivisionId);
		leave.SetMobileAttachments(MobileAttachmentStore.Serialize(attachments));
		if (id.HasValue) await leaves.UpdateAsync(leave, autoSave: true); else await leaves.InsertAsync(leave, autoSave: true);
		return Map(leave, type.LeaveTypeName, p);
	}

	public async Task<MobileLeaveDto> CancelLeaveAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		if (!p.IsGuardian) throw new AbpAuthorizationException();
		var leave = await FindAsync(id, p);
		leave.Cancel();
		await leaves.UpdateAsync(leave, autoSave: true);
		return Map(leave, (await leaveTypes.GetAsync(leave.StudentLeaveTypeId)).LeaveTypeName, p);
	}

	public async Task<MobileAttachmentDto> UploadAsync(MobileAcademicInput input, IRemoteStreamContent file)
	{
		var p = await context.ResolveAsync(input);
		if (!p.IsGuardian) throw new AbpAuthorizationException();
		var item = await files.UploadAsync(p.Student.Id, Guid.Empty, file);
		return new(item.Id, item.Name, "", item.ContentType, item.Size);
	}
	public async Task DeleteAttachmentAsync(Guid attachmentId, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		if (!p.IsGuardian) throw new AbpAuthorizationException();
		var requests = await leaves.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == p.Student.Id);
		if (requests.Any(x => MobileAttachmentStore.Read(x.MobileAttachmentsJson).Any(a => a.Id == attachmentId)))
			throw MobileAcademicContextAppService.Invalid("The attachment is already part of a leave request.");
		await files.DeleteAsync(p.Student.Id, Guid.Empty, attachmentId, null);
	}
	public async Task<MobileFileDto> DownloadAsync(Guid id, Guid attachmentId, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var leave = await FindAsync(id, p);
		var attachment = MobileAttachmentStore.Read(leave.MobileAttachmentsJson).SingleOrDefault(x => x.Id == attachmentId)
			?? throw new EntityNotFoundException(typeof(StudentLeave), attachmentId);
		return await files.DownloadAsync(attachment);
	}
	private static MobileLeaveDto Map(StudentLeave l, string reason, MobilePlacement p) => new(l.Id, l.StudentLeaveTypeId, reason,
		DateOnly.FromDateTime(SchoolDate(l.LeaveDate)), DateOnly.FromDateTime(SchoolDate(l.EndDate ?? l.LeaveDate)), l.TotalDays, l.IsHalfDay,
		l.Description ?? "", l.Status.ToString().ToLowerInvariant(), l.CreationTime, l.Status == StudentLeaveStatusEnum.Pending ? null : l.LastModificationTime,
		l.RejectionReason ?? l.Remarks, p.IsGuardian && l.Status == StudentLeaveStatusEnum.Pending, p.IsGuardian && l.Status == StudentLeaveStatusEnum.Pending,
		MobileAttachmentStore.Read(l.MobileAttachmentsJson).Select(x => MobileAttachmentStore.Map(x,
			$"/api/mobile/v1/academics/attendance/leave-requests/{l.Id}/attachments/{x.Id}/download?studentId={p.Student.Id}&academicYearId={p.Year.Id}")).ToList(),
		MobileAcademicContextAppService.Name(p.Student), $"{p.StandardName} - {p.DivisionName}", l.HalfDayStartTime, l.HalfDayEndTime);
}
