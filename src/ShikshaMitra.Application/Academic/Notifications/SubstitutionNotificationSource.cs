using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(SubstitutionNotificationSource), typeof(INotificationSourceAdapter), typeof(INotificationDueSource))]
public sealed class SubstitutionNotificationSource(IRepository<SubstitutionRequest, Guid> duties, IRepository<Standard, Guid> classes,
	IRepository<Division, Guid> divisions, IRepository<Employee, Guid> employees, ICurrentTenant tenant, ITenantStore tenants,
	IAsyncQueryableExecuter queries, IClock clock, IConfiguration configuration, IUserScopeService userScopes,
	SubstitutionNotificationEvidence evidence)
	: INotificationSourceAdapter, INotificationDueSource, ITransientDependency
{
	internal const string Type = "Academic.Substitution";
	internal const string Assigned = AcademicNotificationEventKeys.SubstitutionAssigned;
	internal const string Changed = AcademicNotificationEventKeys.SubstitutionChanged;
	internal const string Cancelled = AcademicNotificationEventKeys.SubstitutionCancelled;
	internal const string AcknowledgementDue = AcademicNotificationEventKeys.SubstitutionAcknowledgementDue;
	private const string PreviousSubstitute = "PreviousSubstitute";
	private const string SendAutomatically = "AcademicSubstitutionNotify";
	private const string AssignmentRevision = "AcademicSubstitutionRevision";
	private const string TransitionEvent = "AcademicSubstitutionTransition";
	private const string RemovedDuties = "AcademicSubstitutionRemovedDuties";
	public string SourceType => Type;
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[] { Assigned, Changed, Cancelled, AcknowledgementDue });
	public IReadOnlyCollection<string> EventKeys => SupportedEvents;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcknowledgementDue });

	internal static bool IsOptedIn(SubstitutionRequest duty) => duty.GetProperty<bool>(SendAutomatically);
	internal static string AssignmentEvent(SubstitutionRequest duty) => duty.GetProperty<string>(TransitionEvent) == Changed ? Changed : Assigned;

	internal static void RecordAssignmentChoice(SubstitutionRequest duty, bool sendNotification, string? previousRevision)
	{
		if (IsOptedIn(duty) == sendNotification && previousRevision == Revision(duty)) return;
		duty.SetProperty(AssignmentRevision, Guid.NewGuid().ToString("N"));
		duty.SetProperty(SendAutomatically, sendNotification);
	}

	internal static void RecordTransition(SubstitutionRequest duty, string eventKey)
	{
		duty.SetProperty(TransitionEvent, eventKey);
		if (eventKey == Cancelled) duty.SetProperty(AssignmentRevision, Guid.NewGuid().ToString("N"));
	}

	internal async Task RecordPreviousDutyAsync(SubstitutionRequest duty, Guid nextTeacherId, CancellationToken token)
	{
		if (duty.SubstituteTeacherId == nextTeacherId || tenant.Id is not Guid tenantId || duty.TenantId != tenantId) return;
		var previous = await FindTeacherAsync(tenantId, duty.SubstituteTeacherId, PreviousSubstitute, token);
		var known = await evidence.ReadAsync(tenantId, duty.Id, token);
		var removed = ReadPreviousDuties(duty).Where(x => x.Recipient != previous && x.Recipient.EmployeeId != nextTeacherId &&
			!known.Any(n => n.Matches(x.Recipient) && n.AcceptedAt >= x.RemovedAt)).ToList();
		if (previous != null)
			removed.Add(new(previous, new DateTimeOffset(clock.Now.ToUniversalTime()), DateOnly.FromDateTime(duty.Date), duty.Period, duty.StartTime, duty.EndTime));
		duty.SetProperty(RemovedDuties, JsonSerializer.Serialize(removed));
	}

	private static PreviousDuty[] ReadPreviousDuties(SubstitutionRequest duty)
	{
		var saved = duty.GetProperty<string>(RemovedDuties);
		if (string.IsNullOrEmpty(saved)) return [];
		try { return JsonSerializer.Deserialize<PreviousDuty[]>(saved) ?? []; }
		catch (JsonException) { return []; }
	}

	private sealed record PreviousDuty(NotificationRecipientRefDto Recipient, DateTimeOffset RemovedAt, DateOnly Date, string Period, TimeOnly Start, TimeOnly End);

	internal static string Revision(SubstitutionRequest duty) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
	{
		Assignment = duty.GetProperty<string>(AssignmentRevision),
		Date = DateOnly.FromDateTime(duty.Date), duty.StartTime, duty.EndTime, duty.Period,
		duty.AcademicYearId, duty.StandardId, duty.DivisionId, duty.SubjectId, duty.TimetableSlotSubjectId,
		duty.OriginalTeacherId, duty.SubstituteTeacherId, duty.AssignedRoomId, duty.AcknowledgementRequired
	})));

	internal static bool CanRemind(SubstitutionRequest duty) => duty.AcknowledgementRequired &&
		duty.Status is SubstitutionStatus.Assigned or SubstitutionStatus.WaitingAck &&
		duty.AcknowledgementStatus is SubstitutionAcknowledgementStatus.NotSent or SubstitutionAcknowledgementStatus.Sent;

	internal static bool IsActive(SubstitutionRequest duty) =>
		duty.Status is SubstitutionStatus.Assigned or SubstitutionStatus.WaitingAck or SubstitutionStatus.Acknowledged;

	internal static DateTimeOffset PeriodEnd(SubstitutionRequest duty) =>
		new(SubstitutionTimeWindow.ToUtc(duty.Date, duty.EndTime, SubstitutionAppService.GetInstitutionTimeZone()));

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceType != Type ||
			!EventKeys.Contains(context.EventKey) || context.SourceId == Guid.Empty)
			return LearningTaskNotificationContent.Unavailable();
		var match = await FindAsync(context.TenantId, context.SourceId, cancellationToken);
		if (match == null) return LearningTaskNotificationContent.Unavailable();
		var duty = match.Duty;
		var end = PeriodEnd(duty);
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		var reminder = context.EventKey == AcknowledgementDue;
		var changed = context.EventKey == Changed;
		var cancelled = context.EventKey == Cancelled;
		var eligible = context.EventKey switch
		{
			AcknowledgementDue => CanRemind(duty),
			Cancelled => duty.Status == SubstitutionStatus.Cancelled && duty.GetProperty<string>(TransitionEvent) == Cancelled,
			Changed => IsOptedIn(duty) && IsActive(duty) && duty.GetProperty<string>(TransitionEvent) == Changed,
			_ => IsOptedIn(duty) && IsActive(duty)
		};
		if (end <= now || !eligible ||
			(context.SourceScope != null && context.SourceScope != match.Scope) ||
			(context.DueOccurrenceAt.HasValue && context.DueOccurrenceAt != end))
			return LearningTaskNotificationContent.Unavailable();

		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(school)) return LearningTaskNotificationContent.Unavailable();
		var recipients = new List<NotificationRecipientRefDto>();
		var substitute = await FindTeacherAsync(context.TenantId, duty.SubstituteTeacherId, "SubstituteTeacher", cancellationToken);
		if (substitute != null) recipients.Add(substitute);
		else if (!changed && !cancelled) return LearningTaskNotificationContent.Unavailable();
		if (!reminder)
		{
			var original = await FindTeacherAsync(context.TenantId, duty.OriginalTeacherId, "OriginalTeacher", cancellationToken);
			if (original != null) recipients.Add(original);
			var configured = configuration[$"Notifications:Academic:Tenants:{context.TenantId:N}:SubstitutionReviewerUserId"];
			if (Guid.TryParse(configured, out var reviewer) && reviewer != Guid.Empty &&
				StudentRecordScope.HasAccess(match, await userScopes.GetUserScopeAsync(reviewer)))
				recipients.Add(new("ConfiguredReviewer", UserId: reviewer));
		}

		var overrides = new List<NotificationSourceRecipientContentDto>();
		var revision = Revision(duty);
		if (changed || cancelled)
		{
			var known = await evidence.ReadAsync(context.TenantId, duty.Id, cancellationToken);
			if (cancelled) recipients.RemoveAll(recipient => !known.Any(x => x.Matches(recipient)));
			foreach (var prior in ReadPreviousDuties(duty))
			{
				var earlier = prior.Recipient;
				if (earlier.UserId == substitute?.UserId ||
					known.Any(x => x.Matches(earlier) && x.SourceRevision != revision && x.AcceptedAt >= prior.RemovedAt) ||
					(cancelled && !known.Any(x => x.Matches(earlier with { Kind = "SubstituteTeacher" })))) continue;
				var currentReference = await FindTeacherAsync(context.TenantId, earlier.EmployeeId, PreviousSubstitute, cancellationToken);
				if (currentReference != earlier) continue;
				recipients.Add(earlier);
				overrides.Add(new(earlier, DutyVariables(school, prior.Date, prior.Period, prior.Start, prior.End), LearningTaskNotificationContent.Link));
			}
		}
		if (recipients.Count == 0) return LearningTaskNotificationContent.Unavailable();

		var variables = DutyVariables(school, DateOnly.FromDateTime(duty.Date), duty.Period, duty.StartTime, duty.EndTime);
		variables["class_name"] = new(NotificationVariableType.Text, match.ClassName);
		if (!string.IsNullOrWhiteSpace(duty.SubjectName)) variables["subject_name"] = new(NotificationVariableType.Text, duty.SubjectName);
		if (!string.IsNullOrWhiteSpace(duty.AssignedRoomName)) variables["room_name"] = new(NotificationVariableType.Text, duty.AssignedRoomName);
		return new(true, null, revision, LearningTaskNotificationContent.Link, variables, recipients, end,
			[new(match.Scope, recipients)], overrides);
	}

	private static Dictionary<string, NotificationVariableValueDto> DutyVariables(string school, DateOnly date, string period, TimeOnly start, TimeOnly end) =>
		new(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["period_name"] = new(NotificationVariableType.Text, period),
			["session_date"] = new(NotificationVariableType.Date, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
			["start_time"] = new(NotificationVariableType.Text, start.ToString("HH:mm", CultureInfo.InvariantCulture)),
			["end_time"] = new(NotificationVariableType.Text, end.ToString("HH:mm", CultureInfo.InvariantCulture))
		};

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken cancellationToken)
	{
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id || request.PageSize is < 1 or > 100)
			throw new BusinessException(NotificationErrorCodes.StateConflict);
		var zone = SubstitutionAppService.GetInstitutionTimeZone();
		var day = DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(request.Now, zone).Date, DateTimeKind.Utc);
		var query = (await ScopedAsync(request.TenantId)).Where(x => x.Duty.Date >= day &&
			x.Duty.AcknowledgementRequired && x.Duty.SubstituteTeacherId.HasValue &&
			(x.Duty.Status == SubstitutionStatus.Assigned || x.Duty.Status == SubstitutionStatus.WaitingAck) &&
			(x.Duty.AcknowledgementStatus == SubstitutionAcknowledgementStatus.Sent || x.Duty.AcknowledgementStatus == SubstitutionAcknowledgementStatus.NotSent));
		if (request.Continuation is { } continuation)
		{
			if (!Guid.TryParseExact(continuation, "N", out var id) || id == Guid.Empty)
				throw new BusinessException(NotificationErrorCodes.StateConflict);
			query = query.Where(x => x.Duty.Id.CompareTo(id) > 0);
		}
		var rows = await queries.ToListAsync(query.OrderBy(x => x.Duty.Id).Take(request.PageSize + 1), cancellationToken);
		var candidates = rows.Take(request.PageSize).Where(x => IsOptedIn(x.Duty)).Select(x =>
		{
			var end = PeriodEnd(x.Duty);
			return new NotificationDueSourceCandidate(AcknowledgementDue, x.Duty.Id, Revision(x.Duty), x.Scope, end, end,
				NotificationDueTimeMode.PrecedingSchoolDays);
		}).Where(x => x.ExpiresAt > request.Now).ToArray();
		return new(candidates, rows.Count > request.PageSize ? rows[request.PageSize - 1].Duty.Id.ToString("N") : null);
	}

	internal async Task<ScopedDuty?> FindAsync(Guid tenantId, Guid id, CancellationToken token) =>
		await queries.SingleOrDefaultAsync((await ScopedAsync(tenantId)).Where(x => x.Duty.Id == id), token);

	private async Task<IQueryable<ScopedDuty>> ScopedAsync(Guid tenantId) =>
		from duty in await duties.GetQueryableAsync()
		join standard in await classes.GetQueryableAsync() on duty.StandardId equals (Guid?)standard.Id
		join division in await divisions.GetQueryableAsync() on duty.DivisionId equals (Guid?)division.Id
		let stage = standard.EducationMediumStage
		let medium = stage.EducationMedium
		let board = medium.EducationBoard
		where duty.TenantId == tenantId && !duty.IsDeleted && standard.TenantId == tenantId && division.TenantId == tenantId &&
			division.StandardId == standard.Id && stage.TenantId == tenantId && medium.TenantId == tenantId &&
			board.TenantId == tenantId && board.AcademicYear.TenantId == tenantId &&
			(!duty.AcademicYearId.HasValue || duty.AcademicYearId == board.AcademicYearId)
		select new ScopedDuty
		{
			Duty = duty, Year = board.AcademicYearId, Board = board.Id, Medium = medium.Id, Stage = stage.Id,
			Standard = standard.Id, Division = division.Id, ClassName = standard.Name
		};

	private async Task<NotificationRecipientRefDto?> FindTeacherAsync(Guid tenantId, Guid? reference, string kind, CancellationToken token)
	{
		if (reference is not Guid id || id == Guid.Empty) return null;
		var matches = await queries.ToListAsync((await employees.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.UserId.HasValue && x.UserId != Guid.Empty && (x.Id == id || x.UserId == id) &&
			(!x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active))
			.Select(x => new { x.Id, x.UserId }).Take(2), token);
		return matches.Count == 1 ? new(kind, UserId: matches[0].UserId, EmployeeId: matches[0].Id) : null;
	}

	internal sealed class ScopedDuty : IUserScopedEntity
	{
		public required SubstitutionRequest Duty { get; init; }
		public Guid Year { get; init; }
		public Guid Board { get; init; }
		public Guid Medium { get; init; }
		public Guid Stage { get; init; }
		public Guid Standard { get; init; }
		public Guid Division { get; init; }
		public string ClassName { get; init; } = string.Empty;
		public NotificationSourceScopeDto Scope => new(Year, Board, Medium, Standard, Division);
		Guid? IUserScopedEntity.AcademicYearId => Year;
		Guid? IUserScopedEntity.EducationBoardId => Board;
		Guid? IUserScopedEntity.EducationMediumId => Medium;
		Guid? IUserScopedEntity.EducationMediumStageId => Stage;
		Guid? IUserScopedEntity.StandardId => Standard;
		Guid? IUserScopedEntity.DivisionId => Division;
	}
}
