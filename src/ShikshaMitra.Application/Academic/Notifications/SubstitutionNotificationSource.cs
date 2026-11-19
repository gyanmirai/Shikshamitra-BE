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
	IAsyncQueryableExecuter queries, IClock clock, IConfiguration configuration, IUserScopeService userScopes)
	: INotificationSourceAdapter, INotificationDueSource, ITransientDependency
{
	internal const string Type = "Academic.Substitution";
	internal const string Assigned = AcademicNotificationEventKeys.SubstitutionAssigned;
	internal const string AcknowledgementDue = AcademicNotificationEventKeys.SubstitutionAcknowledgementDue;
	private const string SendAutomatically = "AcademicSubstitutionNotify";
	private const string AssignmentRevision = "AcademicSubstitutionRevision";
	public string SourceType => Type;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { Assigned, AcknowledgementDue });
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcknowledgementDue });

	internal static bool IsOptedIn(SubstitutionRequest duty) => duty.GetProperty<bool>(SendAutomatically);

	internal static void RecordAssignmentChoice(SubstitutionRequest duty, bool sendNotification, string? previousRevision)
	{
		if (IsOptedIn(duty) == sendNotification && previousRevision == Revision(duty)) return;
		duty.SetProperty(AssignmentRevision, Guid.NewGuid().ToString("N"));
		duty.SetProperty(SendAutomatically, sendNotification);
	}

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
		if (end <= now || (reminder ? !CanRemind(duty) : !IsOptedIn(duty) || !IsActive(duty)) ||
			(context.SourceScope != null && context.SourceScope != match.Scope) ||
			(context.DueOccurrenceAt.HasValue && context.DueOccurrenceAt != end))
			return LearningTaskNotificationContent.Unavailable();

		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(school)) return LearningTaskNotificationContent.Unavailable();
		var recipients = new List<NotificationRecipientRefDto>();
		await AddTeacherAsync(context.TenantId, duty.SubstituteTeacherId, "SubstituteTeacher", recipients, cancellationToken);
		if (recipients.Count == 0) return LearningTaskNotificationContent.Unavailable();
		if (!reminder)
		{
			await AddTeacherAsync(context.TenantId, duty.OriginalTeacherId, "OriginalTeacher", recipients, cancellationToken);
			var configured = configuration[$"Notifications:Academic:Tenants:{context.TenantId:N}:SubstitutionReviewerUserId"];
			if (Guid.TryParse(configured, out var reviewer) && reviewer != Guid.Empty &&
				StudentRecordScope.HasAccess(match, await userScopes.GetUserScopeAsync(reviewer)))
				recipients.Add(new("ConfiguredReviewer", UserId: reviewer));
		}

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["class_name"] = new(NotificationVariableType.Text, match.ClassName),
			["period_name"] = new(NotificationVariableType.Text, duty.Period),
			["session_date"] = new(NotificationVariableType.Date, DateOnly.FromDateTime(duty.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
		};
		if (!string.IsNullOrWhiteSpace(duty.SubjectName)) variables["subject_name"] = new(NotificationVariableType.Text, duty.SubjectName);
		return new(true, null, Revision(duty), LearningTaskNotificationContent.Link, variables, recipients, end,
			[new(match.Scope, recipients)]);
	}

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

	private async Task AddTeacherAsync(Guid tenantId, Guid? reference, string kind, List<NotificationRecipientRefDto> recipients, CancellationToken token)
	{
		if (reference is not Guid id || id == Guid.Empty) return;
		var matches = await queries.ToListAsync((await employees.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.UserId.HasValue && x.UserId != Guid.Empty && (x.Id == id || x.UserId == id) &&
			(!x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active))
			.Select(x => new { x.Id, x.UserId }).Take(2), token);
		if (matches.Count == 1) recipients.Add(new(kind, UserId: matches[0].UserId, EmployeeId: matches[0].Id));
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
