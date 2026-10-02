using System;
using System.Linq;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceDailyCommandService(AttendanceRosterService rosters, AttendanceManager manager,
	IAttendanceDayRepository days, IRepository<StudentAttendanceOperation, Guid> operations,
	IRepository<StudentAttendanceCorrection, Guid> corrections, IPermissionChecker permissions, IRepository<StudentEnrollment, Guid> enrollmentRecords)
	: ApplicationService, ITransientDependency
{
	[UnitOfWork(isTransactional: true)]
	public Task<MarkAttendanceOutput> SaveAsync(MarkAttendanceInput input) => AttendanceCommandConflictMapper.ExecuteAsync(() => SaveCoreAsync(input));
	private async Task<MarkAttendanceOutput> SaveCoreAsync(MarkAttendanceInput input)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var roster = await rosters.LoadAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		if (input.Date.Date > AttendanceSchoolDate.Today(roster.Options, Clock.Now)) throw new BusinessException("SM:CannotMarkFutureAttendance");
		var actor = CurrentUser.GetId();
		var hash = AttendanceRosterService.Hash(JsonSerializer.Serialize(input));
		if (input.OperationId != Guid.Empty)
		{
			var previous = await operations.FindAsync(x => x.Id == input.OperationId && x.TenantId == CurrentTenant.Id);
			if (previous != null)
			{
				if (previous.ActorId != actor || previous.RequestHash != hash) throw new BusinessException(AttendanceOperationErrorCodes.IdempotencyKeyReused);
				await work.CompleteAsync();
				return JsonSerializer.Deserialize<MarkAttendanceOutput>(previous.ResultJson)!;
			}
		}
		if (string.IsNullOrWhiteSpace(input.RosterRevision)) throw new BusinessException(AttendanceOperationErrorCodes.RosterRevisionRequired);
		ValidateRevision(roster, input.Revision, input.RosterRevision);
		roster.Day?.EnsureNotLocked();
		if (!roster.SchoolDay.IsExpected) throw new BusinessException(AttendanceOperationErrorCodes.SchoolNotExpected).WithData("Reason", roster.SchoolDay.Reason);
		if (input.Entries.Count == 0 || input.Entries.Select(e => e.StudentId).Distinct().Count() != input.Entries.Count)
			throw new BusinessException(AttendanceOperationErrorCodes.DuplicateOrEmptyEntries);
		var policyJson = JsonSerializer.Serialize(roster.Options);
		var changed = input.Entries.Where(e => Changed(roster.Members.SingleOrDefault(r => r.Student.Id == e.StudentId)?.Entry, e)).ToList();
		foreach (var item in input.Entries)
		{
			var member = roster.Members.SingleOrDefault(r => r.Student.Id == item.StudentId);
			if (member == null || !member.Expected || item.EnrollmentId.HasValue && item.EnrollmentId != member.Enrollment.Id)
				throw new BusinessException(AttendanceOperationErrorCodes.StudentOutsideDatedRoster);
			AttendanceEntryRules.Validate(item, roster.Options);
			if (AttendanceEntryRules.NeedsCreditCorrection(member, roster))
				throw new BusinessException(AttendanceOperationErrorCodes.CreditCorrectionRequired);
			if (member.Leave != null && item.Status == AttendanceStatus.Present && string.IsNullOrWhiteSpace(input.CorrectionReason))
				throw new BusinessException(AttendanceOperationErrorCodes.LeaveObservationConflictRequiresReason);
			if (changed.Contains(item) && member.Entry != null && member.Entry.Status != AttendanceStatus.Pending)
			{
				if (input.Date.Date < AttendanceSchoolDate.Today(roster.Options, Clock.Now) && roster.Options.PriorDayCorrectionRequiresApproval)
					throw new BusinessException(AttendanceOperationErrorCodes.CorrectionApprovalRequired);
				if (roster.Day?.IsSubmitted == true && roster.Options.SameDayCorrectionRequiresReason && string.IsNullOrWhiteSpace(input.CorrectionReason))
					throw new BusinessException(AttendanceOperationErrorCodes.CorrectionReasonRequired);
			}
		}
		if (input.Submit)
		{
			if (roster.Members.Any(r => r.Expected && AttendanceEntryRules.NeedsCreditCorrection(r, roster)))
				throw new BusinessException(AttendanceOperationErrorCodes.CreditCorrectionRequired);
			var statuses = roster.Members.Where(r => r.Expected).Select(r => input.Entries.SingleOrDefault(e => e.StudentId == r.Student.Id)?.Status ?? r.Entry?.Status ?? AttendanceStatus.Pending);
			if (!statuses.Any() || statuses.Any(s => s == AttendanceStatus.Pending)) throw new BusinessException(AttendanceOperationErrorCodes.IncompleteRoster);
		}
		if (roster.Day?.IsSubmitted == true && input.Entries.Any(e => e.Status == AttendanceStatus.Pending))
			throw new BusinessException(AttendanceOperationErrorCodes.CannotUnmarkSubmittedAttendance);
		var day = roster.Day ?? await manager.GetOrCreateAttendanceDayAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		var entriesToSave = input.Submit ? roster.Members.Where(r => r.Expected).Select(r =>
			input.Entries.SingleOrDefault(e => e.StudentId == r.Student.Id) ?? new AttendanceEntryDto {
				StudentId = r.Student.Id, EnrollmentId = r.Enrollment.Id, Status = r.Entry!.Status,
				Late = r.Entry.Late, HalfDay = r.Entry.HalfDay, Remarks = r.Entry.Remarks }).ToList() : input.Entries;
		// Compare the dated roster's enrollment revisions in the same transaction as attendance.
		// Placement, start-date and individual schedule writers update these same aggregate stamps.
		var affectedIds = input.Entries.Select(e => e.StudentId).ToHashSet();
		await enrollmentRecords.UpdateManyAsync(roster.Members.Where(r => affectedIds.Contains(r.Student.Id) || input.Submit && r.Expected).Select(r => r.Enrollment));
		foreach (var item in changed.Where(_ => roster.Day?.IsSubmitted == true))
		{
			var before = roster.Members.Single(r => r.Student.Id == item.StudentId);
			var audit = new StudentAttendanceCorrection(GuidGenerator.Create(), CurrentTenant.Id, day.Id, item.StudentId, actor,
				GuidGenerator.Create(), AttendanceEntryRules.CorrectionSnapshot(before),
				JsonSerializer.Serialize(item), policyJson, input.CorrectionReason ?? "Submitted attendance correction");
			audit.Apply(actor, audit.Reason, reviewed: false); await corrections.InsertAsync(audit);
		}
		await manager.MarkAttendanceAsync(day.Id, entriesToSave.Select(e => AttendanceEntryRules.ToDomain(e,
			roster.Members.Single(r => r.Student.Id == e.StudentId), input.Date, roster.Options)).ToList());
		day.SaveRevision(input.OperationId, hash, input.Submit, roster.Revision, policyJson);
		await days.UpdateAsync(day, autoSave: true);
		var output = new MarkAttendanceOutput { AttendanceDayId = day.Id, IsLocked = day.IsLocked, IsSubmitted = day.IsSubmitted,
			Revision = day.Revision.ToString(), TotalMarked = input.Entries.Count,
			PresentCount = input.Entries.Count(e => e.Status == AttendanceStatus.Present), AbsentCount = input.Entries.Count(e => e.Status == AttendanceStatus.Absent),
			Message = day.IsSubmitted ? "Attendance submitted" : "Attendance draft saved" };
		if (input.OperationId != Guid.Empty) await operations.InsertAsync(new(input.OperationId, CurrentTenant.Id, day.Id, actor, hash, JsonSerializer.Serialize(output)), autoSave: true);
		await work.CompleteAsync();
		return output;
	}

	public static void ValidateRevision(AttendanceRoster roster, string? revision, string? rosterRevision)
	{
		if (roster.Day != null && revision != roster.Day.Revision.ToString() || roster.Day == null && revision is not (null or "0"))
			throw new BusinessException(AttendanceOperationErrorCodes.ConcurrencyConflict);
		if (rosterRevision != null && rosterRevision != roster.Revision) throw new BusinessException(AttendanceOperationErrorCodes.StaleRoster);
	}
	public static bool Changed(AttendanceEntry? before, AttendanceEntryDto item) => before == null ||
		before.Status != item.Status || before.Late != item.Late || before.HalfDay != item.HalfDay || before.Remarks != item.Remarks;
}

internal static class AttendanceEntryRules
{
	public static bool NeedsCreditCorrection(AttendanceRosterMember member, AttendanceRoster roster)
	{
		var entry = member.Entry;
		if (roster.Day?.IsSubmitted != true || entry == null || entry.Status == AttendanceStatus.Pending) return false;
		var derived = ToDomain(new AttendanceEntryDto { StudentId = member.Student.Id, Status = entry.Status,
			Late = entry.Late, HalfDay = entry.HalfDay, Remarks = entry.Remarks }, member, roster.Date, roster.Options);
		return entry.ApprovedLeaveId != derived.ApprovedLeaveId || entry.PresentCredit != derived.PresentCredit ||
			entry.DenominatorCredit != derived.DenominatorCredit || entry.ExcludedFromPercentage != derived.ExcludedFromPercentage;
	}

	// Keep the recorded credits and request-time leave facts together. Approval must not silently
	// apply newer source facts, even when the requested Present/Absent mark is unchanged.
	public static string CorrectionSnapshot(AttendanceRosterMember member) => JsonSerializer.Serialize(new {
		member.Entry?.Status, member.Entry?.Late, member.Entry?.HalfDay, member.Entry?.Remarks,
		member.Entry?.EnrollmentId, member.Entry?.PresentCredit, member.Entry?.DenominatorCredit,
		member.Entry?.ExcludedFromPercentage, member.Entry?.ApprovedLeaveId,
		SourceLeave = member.Leave
	});

	public static void Validate(AttendanceEntryDto entry, AttendancePolicyOptions options)
	{
		if (!Enum.IsDefined(entry.Status)) throw new BusinessException(AttendanceOperationErrorCodes.InvalidStatus);
		if ((entry.Late || entry.HalfDay) && entry.Status != AttendanceStatus.Present) throw new BusinessException(AttendanceOperationErrorCodes.DetailsRequirePresent);
		if (entry.Late && !options.AllowLate || entry.HalfDay && !options.AllowHalfDay) throw new BusinessException(AttendanceOperationErrorCodes.DetailDisabledByPolicy);
		if (entry.Remarks?.Length > 500) throw new BusinessException("SM:RemarksExceedsMaxLength");
	}
	public static AttendanceManager.AttendanceEntryInput ToDomain(AttendanceEntryDto item, AttendanceRosterMember member, DateTime date, AttendancePolicyOptions options)
	{
		Validate(item, options);
		var credits = CalculateCredits(item, member.Leave, options);
		return new() { StudentId = item.StudentId, EnrollmentId = member.Enrollment.Id, Date = date, Status = item.Status, Remarks = item.Remarks,
			HistoricalRollNo = member.RollNumber,
			Late = item.Late, HalfDay = item.HalfDay, ApprovedLeaveId = member.Leave?.Id, ExcludedFromPercentage = credits.Excluded,
			PresentCredit = credits.Present, DenominatorCredit = credits.Denominator,
			PolicyJson = JsonSerializer.Serialize(options) };
	}
	public static (double? Present, double Denominator, bool Excluded) CalculateCredits(AttendanceEntryDto item,
		AttendanceApprovedLeave? leave, AttendancePolicyOptions options)
	{
		var excluded = leave != null && !leave.IsHalfDay && item.Status == AttendanceStatus.Absent && options.ApprovedLeaveTreatment == "Exclude";
		var denominator = excluded ? 0 : leave?.IsHalfDay == true && options.ApprovedLeaveTreatment == "Exclude" && options.HalfDayLeaveTreatment == "HalfDenominator" ? .5 : 1;
		var credit = item.Status == AttendanceStatus.Pending ? (double?)null : item.Status == AttendanceStatus.Absent ? 0 :
			item.HalfDay ? Math.Min((double)options.HalfDayCredit, denominator) : (item.Late ? (double)options.LateCredit : 1) * denominator;
		return (credit, denominator, excluded);
	}
}

internal static class AttendanceSchoolDate
{
	public static bool PastMarkingCutoff(AttendancePolicyOptions options, DateTime schoolDate, DateTime now)
	{
		var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now.ToUniversalTime(), DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone));
		return schoolDate.Date < localNow.Date || schoolDate.Date == localNow.Date && TimeOnly.FromDateTime(localNow) >= TimeOnly.ParseExact(options.MarkingCutoff, "HH:mm");
	}
	public static DateTime Today(AttendancePolicyOptions options, DateTime now) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now.ToUniversalTime(), DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone)).Date;
}
