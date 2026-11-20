using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using CalendarPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(AcademicCalendarEventReminderSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicCalendarEventReminderSource(AcademicCalendarPublicationSource publication,
	AcademicCalendarNotificationAudience audience, AcademicCalendarEventChangedSource scopeAccess,
	IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days,
	IUserScopeService scopes, IPermissionChecker permissions, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser current,
	ITenantStore tenants, NotificationSchoolTimeZone schoolZone) : INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Key = AcademicNotificationEventKeys.CalendarEventReminder;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { Key });

	internal static DateTimeOffset? StartAt(AcademicCalendarDay day, TimeZoneInfo zone)
	{
		var time = day.StartTime ?? TimeSpan.Zero;
		return time < TimeSpan.Zero || time >= TimeSpan.FromDays(1) || day.Date.Date == DateTime.MaxValue.Date ? null :
			NotificationDeliveryTimingPolicy.Boundary(day.Date.Date.Add(time), zone, later: false);
	}

	internal static string Revision(AcademicCalendar calendar, AcademicCalendarDay day)
	{
		var material = AcademicCalendarEventMaterial.From(day);
		var json = NotificationRuntimeJson.Serialize(new
		{
			Publication = AcademicCalendarPublicationSource.CurrentRevision(calendar), Material = material, day.Title
		}, NotificationErrorCodes.SourceUnavailable);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.EventKey != Key ||
			context.SourceType != AcademicCalendarPublicationSource.Type || context.DueOccurrenceAt is not { } occurrence ||
			context.SourceScope is not { DivisionId: not null, ClassId: not null } requested ||
			await FindAsync(school, context.SourceId, token) is not { } record || record.Calendar.Status != CalendarStatus.Published ||
			record.Day.Status != CalendarDayStatus.Active || !AcademicCalendarEventMaterial.From(record.Day).Includes(requested) ||
			Revision(record.Calendar, record.Day) != context.SourceRevision)
			return Unavailable();

		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		if (StartAt(record.Day, zone) is not { } starts || starts != occurrence || context.OccurredAt >= starts) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();
		var slices = await audience.ReadAsync(school, record.Scope, requested, token);
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["event_title"] = new(NotificationVariableType.Text, record.Day.Title),
			["event_date"] = new(NotificationVariableType.Date, record.Day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
			["location"] = new(NotificationVariableType.Text, record.Day.Venue),
			["event_start"] = new(NotificationVariableType.DateTime, starts.ToString("O", CultureInfo.InvariantCulture))
		};
		return new(true, null, context.SourceRevision, Link, variables, slices.SelectMany(x => x.Recipients).Distinct().ToArray(),
			starts, AudienceSlices: slices);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != Key || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(CalendarPermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await scopeAccess.RequireScopeAsync(school, caller, AcademicCalendarEventMaterial.From(record.Day).Scope(record.Scope), token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => throw new AbpAuthorizationException();

	private async Task<(AcademicCalendar Calendar, AcademicCalendarDay Day, NotificationSourceScopeDto Scope)?> FindAsync(
		Guid school, Guid id, CancellationToken token)
	{
		var record = await queries.SingleOrDefaultAsync(from day in await days.GetQueryableAsync()
			join calendar in await calendars.GetQueryableAsync() on day.AcademicCalendarId equals calendar.Id
			where day.Id == id && day.TenantId == school && !day.IsDeleted && day.DayType == DayType.Event &&
				calendar.TenantId == school && !calendar.IsDeleted && day.Date >= calendar.StartDate && day.Date <= calendar.EndDate
			select new { Day = day, Calendar = calendar }, token);
		if (record == null || await publication.ReadScopeAsync(school, record.Calendar, token) is not { } scope) return null;
		return (record.Calendar, record.Day, scope);
	}
}

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class AcademicCalendarEventReminderDueSource(ICurrentTenant tenant, NotificationSchoolTimeZone schoolZone,
	IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days,
	IRepository<EducationMedium, Guid> mediums, IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries) : INotificationDueSource, ITransientDependency
{
	public string SourceType => AcademicCalendarPublicationSource.Type;
	public string ScanKey => AcademicCalendarEventReminderSource.Key;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { AcademicCalendarEventReminderSource.Key });

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		var school = request.TenantId;
		if (school == Guid.Empty || tenant.Id != school) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
		var enabled = (await rules.GetQueryableAsync()).Where(rule => rule.TenantId == school && !rule.IsDeleted &&
			rule.IsEnabled && rule.EventKey == AcademicCalendarEventReminderSource.Key);
		if (!await queries.AnyAsync(enabled, token)) return new([], null);
		var zone = await schoolZone.GetAsync(token);
		var today = DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(request.Now, zone).Date, DateTimeKind.Utc);

		var query = from day in await days.GetQueryableAsync()
			join calendar in await calendars.GetQueryableAsync() on day.AcademicCalendarId equals calendar.Id
			join medium in await mediums.GetQueryableAsync() on calendar.AcademicMediumId equals medium.Id
			join standard in await standards.GetQueryableAsync() on medium.Id equals standard.EducationMediumStage.EducationMediumId
			join division in await divisions.GetQueryableAsync() on standard.Id equals division.StandardId
			let board = medium.EducationBoard
			where day.TenantId == school && !day.IsDeleted && day.DayType == DayType.Event && day.Status == CalendarDayStatus.Active &&
				day.Date >= today && day.Date >= calendar.StartDate && day.Date <= calendar.EndDate &&
				calendar.TenantId == school && !calendar.IsDeleted && calendar.Status == CalendarStatus.Published &&
				medium.TenantId == school && !medium.IsDeleted && board.TenantId == school && !board.IsDeleted &&
				board.AcademicYear.TenantId == school && !board.AcademicYear.IsDeleted &&
				(!calendar.AcademicYearId.HasValue || calendar.AcademicYearId == board.AcademicYearId) &&
				standard.TenantId == school && !standard.IsDeleted && standard.EducationMediumStage.TenantId == school &&
				!standard.EducationMediumStage.IsDeleted && division.TenantId == school && !division.IsDeleted &&
				(day.AppliesToAll || (day.StandardId == standard.Id && (!day.DivisionId.HasValue || day.DivisionId == division.Id))) &&
				enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == board.AcademicYearId) &&
					(!rule.BoardId.HasValue || rule.BoardId == board.Id) && (!rule.MediumId.HasValue || rule.MediumId == medium.Id) &&
					(!rule.ClassId.HasValue || rule.ClassId == standard.Id) && (!rule.DivisionId.HasValue || rule.DivisionId == division.Id))
			select new { Day = day, Calendar = calendar, board.AcademicYearId, BoardId = board.Id, MediumId = medium.Id,
				ClassId = standard.Id, DivisionId = division.Id };
		if (request.Continuation is { } cursor)
		{
			var (dayId, divisionId) = LearningTaskReminderDuePager.ReadCursor(cursor);
			query = query.Where(x => x.Day.Id.CompareTo(dayId) > 0 || x.Day.Id == dayId && x.DivisionId.CompareTo(divisionId) > 0);
		}
		var rows = await queries.ToListAsync(query.OrderBy(x => x.Day.Id).ThenBy(x => x.DivisionId).Take(request.PageSize + 1), token);
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in rows.Take(request.PageSize))
		{
			if (AcademicCalendarEventReminderSource.StartAt(row.Day, zone) is not { } starts || starts <= request.Now) continue;
			candidates.Add(new(AcademicCalendarEventReminderSource.Key, row.Day.Id,
				AcademicCalendarEventReminderSource.Revision(row.Calendar, row.Day),
				new(row.AcademicYearId, row.BoardId, row.MediumId, row.ClassId, row.DivisionId),
				starts, starts, NotificationDueTimeMode.PrecedingSchoolDays));
		}
		var next = rows.Count <= request.PageSize ? null : $"{rows[request.PageSize - 1].Day.Id:N}:{rows[request.PageSize - 1].DivisionId:N}";
		return new(candidates, next);
	}
}
