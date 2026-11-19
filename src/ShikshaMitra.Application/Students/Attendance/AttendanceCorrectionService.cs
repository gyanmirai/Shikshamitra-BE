using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceCorrectionService(IRepository<StudentAttendanceCorrection, Guid> corrections, AttendanceRosterService rosters,
	IAttendanceDayRepository days, AttendanceManager manager, IStudentRepository students, StudentScopeAccessService scope, IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AttendanceCorrectionDto>> ListAsync(AttendanceCorrectionListInput input)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.View);
		var scopedDays = await scope.ApplyCurrentUserAttendanceDayScopeAsync(await days.GetQueryableAsync());
		var query = from correction in await corrections.GetQueryableAsync()
			join day in scopedDays on correction.AttendanceDayId equals day.Id
			where correction.TenantId == CurrentTenant.Id && (!input.AcademicYearId.HasValue || day.AcademicYearId == input.AcademicYearId) &&
				(!input.StandardId.HasValue || day.StandardId == input.StandardId) && (!input.DivisionId.HasValue || day.DivisionId == input.DivisionId) &&
				(input.Status == null || correction.Status == input.Status)
			select correction;
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(c => c.CreationTime).ThenBy(c => c.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		var dayIds = rows.Select(r => r.AttendanceDayId).Distinct().ToArray();
		var studentIds = rows.Select(r => r.StudentId).Distinct().ToArray();
		var dayRows = await days.GetListAsync(d => d.TenantId == CurrentTenant.Id && Enumerable.Contains(dayIds, d.Id));
		var studentRows = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, s.Id)));
		var result = rows.Select(row => Map(row, dayRows.Single(d => d.Id == row.AttendanceDayId), studentRows.Single(s => s.Id == row.StudentId))).ToList();
		return new(count, result);
	}

	[UnitOfWork(isTransactional: true)]
	public Task<AttendanceCorrectionDto> RequestAsync(AttendanceCorrectionInput input) => AttendanceCommandConflictMapper.ExecuteAsync(() => RequestCoreAsync(input));
	private async Task<AttendanceCorrectionDto> RequestCoreAsync(AttendanceCorrectionInput input)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var roster = await rosters.LoadAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		var day = roster.Day ?? throw new BusinessException("SM:AttendanceDayNotFound");
		var requestJson = JsonSerializer.Serialize(input); var actor = CurrentUser.GetId();
		if (input.OperationId != Guid.Empty)
		{
			var duplicate = await corrections.FindAsync(c => c.TenantId == CurrentTenant.Id && c.OperationId == input.OperationId);
			if (duplicate != null)
			{
				if (duplicate.RequestedBy != actor || duplicate.RequestJson != requestJson) throw new BusinessException(AttendanceOperationErrorCodes.IdempotencyKeyReused);
				var replay = await MapAsync(duplicate); await work.CompleteAsync(); return replay;
			}
		}
		var member = roster.Members.SingleOrDefault(r => r.Student.Id == input.StudentId && r.Expected)
			?? throw new BusinessException(AttendanceOperationErrorCodes.StudentOutsideDatedRoster);
		if (member.Entry == null || member.Entry.Status == AttendanceStatus.Pending) throw new BusinessException(AttendanceOperationErrorCodes.NoRecordedAttendanceToCorrect);
		if (input.Status == AttendanceStatus.Pending) throw new BusinessException(AttendanceOperationErrorCodes.CannotUnmarkSubmittedAttendance);
		var requested = Entry(input); AttendanceEntryRules.Validate(requested, roster.Options);
		if (!AttendanceDailyCommandService.Changed(member.Entry, requested) && !AttendanceEntryRules.NeedsCreditCorrection(member, roster))
			throw new BusinessException(AttendanceOperationErrorCodes.CorrectionHasNoChange);
		if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 2000) throw new BusinessException(AttendanceOperationErrorCodes.CorrectionReasonRequired);
		AttendanceDailyCommandService.ValidateRevision(roster, input.Revision, null);
		if (await corrections.AnyAsync(c => c.TenantId == CurrentTenant.Id && c.AttendanceDayId == day.Id && c.StudentId == input.StudentId && c.Status == "Pending"))
			throw new BusinessException(AttendanceOperationErrorCodes.CorrectionAlreadyPending);
		var item = new StudentAttendanceCorrection(GuidGenerator.Create(), CurrentTenant.Id, day.Id, input.StudentId, actor, input.OperationId,
			AttendanceEntryRules.CorrectionSnapshot(member), requestJson, JsonSerializer.Serialize(roster.Options), input.Reason);
		var requiresApproval = day.IsLocked && roster.Options.LockedCorrectionRequiresApproval ||
			input.Date.Date < AttendanceSchoolDate.Today(roster.Options, Clock.Now) && roster.Options.PriorDayCorrectionRequiresApproval;
		if (!requiresApproval)
		{
			if (day.IsLocked) await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add);
			await ApplyAsync(item, input, roster, member); item.Apply(actor, input.Reason, reviewed: false);
		}
		await corrections.InsertAsync(item, autoSave: true);
		var result = await MapAsync(item); await work.CompleteAsync(); return result;
	}

	[UnitOfWork(isTransactional: true)]
	public Task<AttendanceCorrectionDto> DecideAsync(Guid id, AttendanceCorrectionDecisionInput input, bool approve) => AttendanceCommandConflictMapper.ExecuteAsync(() => DecideCoreAsync(id, input, approve));
	private async Task<AttendanceCorrectionDto> DecideCoreAsync(Guid id, AttendanceCorrectionDecisionInput input, bool approve)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_LeaveAndAbsent.Add);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var item = await corrections.GetAsync(c => c.Id == id && c.TenantId == CurrentTenant.Id);
		var day = await days.GetAsync(item.AttendanceDayId);
		await scope.EnsureCanAccessAttendanceScopeAsync(day.AcademicYearId, day.StandardId, day.DivisionId, "review attendance correction");
		if (item.ConcurrencyStamp != input.Revision) throw new BusinessException(AttendanceOperationErrorCodes.ConcurrencyConflict);
		if (approve)
		{
			var request = JsonSerializer.Deserialize<AttendanceCorrectionInput>(item.RequestJson)!;
			var roster = await rosters.LoadAsync(day.AcademicYearId, day.StandardId, day.DivisionId, day.Date);
			var member = roster.Members.SingleOrDefault(r => r.Student.Id == item.StudentId && r.Expected)
				?? throw new BusinessException(AttendanceOperationErrorCodes.StaleRoster);
			if (AttendanceEntryRules.CorrectionSnapshot(member) != item.BeforeJson) throw new BusinessException(AttendanceOperationErrorCodes.StaleCorrection);
			if (day.IsLocked) await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add);
			item.Apply(CurrentUser.GetId(), input.Reason, reviewed: true); await ApplyAsync(item, request, roster, member);
		}
		else item.Reject(CurrentUser.GetId(), input.Reason);
		await corrections.UpdateAsync(item, autoSave: true);
		var result = await MapAsync(item); await work.CompleteAsync(); return result;
	}
	private async Task ApplyAsync(StudentAttendanceCorrection item, AttendanceCorrectionInput input, AttendanceRoster roster, AttendanceRosterMember member)
	{
		var options = JsonSerializer.Deserialize<AttendancePolicyOptions>(item.PolicyJson)!;
		await manager.MarkAttendanceAsync(roster.Day!.Id, [AttendanceEntryRules.ToDomain(Entry(input), member, input.Date, options)], approvedCorrection: true);
		roster.Day.SaveRevision(item.OperationId, AttendanceRosterService.Hash(item.RequestJson), roster.Day.IsSubmitted, roster.Revision, item.PolicyJson);
		await days.UpdateAsync(roster.Day, autoSave: true);
	}
	private static AttendanceEntryDto Entry(AttendanceCorrectionInput input) => new() { StudentId = input.StudentId, Status = input.Status, Late = input.Late, HalfDay = input.HalfDay, Remarks = input.Remarks };
	private async Task<AttendanceCorrectionDto> MapAsync(StudentAttendanceCorrection item)
	{
		var day = await days.GetAsync(item.AttendanceDayId);
		var student = await AsyncExecuter.SingleAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.Id == item.StudentId && s.TenantId == CurrentTenant.Id));
		return Map(item, day, student);
	}
	private static AttendanceCorrectionDto Map(StudentAttendanceCorrection item, AttendanceDay day, Student student)
	{
		var requested = JsonSerializer.Deserialize<AttendanceEntryDto>(item.RequestJson)!;
		var before = JsonSerializer.Deserialize<CorrectionEvidence>(item.BeforeJson)!;
		var hasCredits = before.DenominatorCredit.HasValue && before.ExcludedFromPercentage.HasValue;
		var options = JsonSerializer.Deserialize<AttendancePolicyOptions>(item.PolicyJson)!;
		var recordedCredits = AttendanceEntryRules.CalculateCredits(before, before.SourceLeave, options);
		var requestedCredits = AttendanceEntryRules.CalculateCredits(requested, before.SourceLeave, options);
		return new() { Id = item.Id, StudentId = item.StudentId, StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
			AcademicYearId = day.AcademicYearId, StandardId = day.StandardId, DivisionId = day.DivisionId, Date = day.Date, BeforeStatus = before.Status,
			IsCreditCorrection = hasCredits && (before.ApprovedLeaveId != before.SourceLeave?.Id || before.PresentCredit != recordedCredits.Present ||
				before.DenominatorCredit != recordedCredits.Denominator || before.ExcludedFromPercentage != recordedCredits.Excluded),
			BeforePresentCredit = before.PresentCredit, BeforeDenominatorCredit = before.DenominatorCredit,
			RequestedPresentCredit = hasCredits ? requestedCredits.Present : null, RequestedDenominatorCredit = hasCredits ? requestedCredits.Denominator : null,
			RequestedStatus = requested.Status, Late = requested.Late, HalfDay = requested.HalfDay, Remarks = requested.Remarks, Reason = item.Reason, Status = item.Status, Revision = item.ConcurrencyStamp };
	}
	private sealed class CorrectionEvidence : AttendanceEntryDto
	{
		public double? PresentCredit { get; set; }
		public double? DenominatorCredit { get; set; }
		public bool? ExcludedFromPercentage { get; set; }
		public Guid? ApprovedLeaveId { get; set; }
		public AttendanceApprovedLeave? SourceLeave { get; set; }
	}
}
