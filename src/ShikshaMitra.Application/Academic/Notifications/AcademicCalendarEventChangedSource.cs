using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using CalendarPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicCalendarEventMaterial(Guid Id, DayType Type, DateTime Date, TimeSpan? Start, TimeSpan? End,
	string? Venue, bool AppliesToAll, Guid? ClassId, Guid? DivisionId)
{
	internal NotificationSourceScopeDto Scope(NotificationSourceScopeDto parent) =>
		AppliesToAll ? parent : parent with { ClassId = ClassId, DivisionId = DivisionId };

	internal bool Includes(NotificationSourceScopeDto scope) =>
		AppliesToAll || ClassId == scope.ClassId && (!DivisionId.HasValue || DivisionId == scope.DivisionId);

	internal static AcademicCalendarEventMaterial From(AcademicCalendarDay day) =>
		new(day.Id, day.DayType, day.Date.Date, day.StartTime, day.EndTime, day.Venue, day.AppliesToAll, day.StandardId, day.DivisionId);
}

internal sealed record AcademicCalendarEventChange(string Revision, string? CalendarRevision, AcademicCalendarEventMaterial[] Before,
	AcademicCalendarEventMaterial[] After, string PreviousTitle);

[RemoteService(false), ExposeServices(typeof(AcademicCalendarEventChangedSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicCalendarEventChangedSource(AcademicCalendarPublicationSource publication,
	AcademicCalendarNotificationAudience audience, IRepository<AcademicCalendar, Guid> calendars,
	IRepository<AcademicCalendarDay, Guid> days, IUserScopeService scopes, AcademicNotificationRuleScopeAccess access,
	IPermissionChecker permissions, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser current,
	ITenantStore tenants, NotificationSchoolTimeZone schoolZone)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.CalendarEventChanged;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	internal static string Record(AcademicCalendar calendar, Guid anchor, AcademicCalendarEventMaterial[] before,
		AcademicCalendarEventMaterial[] after, string previousTitle)
	{
		foreach (var id in before.Select(x => x.Id).Concat(after.Select(x => x.Id)).Distinct())
			calendar.RemoveProperty(Property(id));
		var change = new AcademicCalendarEventChange(Guid.NewGuid().ToString("N"),
			AcademicCalendarPublicationSource.CurrentRevision(calendar), before, after, previousTitle);
		calendar.SetProperty(Property(anchor), NotificationRuntimeJson.Serialize(change, NotificationErrorCodes.SourceUnavailable));
		return change.Revision;
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != AcademicCalendarPublicationSource.Type ||
			context.EventKey != EventKey || await FindAsync(school, context.SourceId, token) is not { } record ||
			record.Calendar.Status != CalendarStatus.Published || record.Day.Status != CalendarDayStatus.Active ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != record.Change.Revision) ||
			record.Change.CalendarRevision != AcademicCalendarPublicationSource.CurrentRevision(record.Calendar))
			return Unavailable();

		var activeIds = record.Change.After.Select(x => x.Id).ToArray();
		var active = await queries.ToListAsync((await days.GetQueryableAsync()).Where(x => x.TenantId == school && !x.IsDeleted &&
			x.AcademicCalendarId == record.Calendar.Id && x.Status == CalendarDayStatus.Active && Enumerable.Contains(activeIds, x.Id)), token);
		if (!active.Select(AcademicCalendarEventMaterial.From).ToHashSet().SetEquals(record.Change.After))
			return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();
		var latestDate = record.Change.Before.Concat(record.Change.After).Max(x => x.Date);
		var finalDate = latestDate <= record.Calendar.EndDate.Date ? latestDate : record.Calendar.EndDate.Date;
		if (finalDate == DateTime.MaxValue.Date) return Unavailable();
		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		var expires = NotificationDeliveryTimingPolicy.Boundary(finalDate.AddDays(1), zone, later: false);
		if (context.OccurredAt >= expires) return Unavailable();

		var revised = await ReadAudienceAsync(school, record.Scope, context.SourceScope, record.Change.After, token);
		var samePlacements = record.Change.Before.Select(row => row.Scope(record.Scope)).ToHashSet()
			.SetEquals(record.Change.After.Select(row => row.Scope(record.Scope)));
		var previous = samePlacements ? revised : await ReadAudienceAsync(school, record.Scope, context.SourceScope, record.Change.Before, token);
		var currentRecipients = revised.SelectMany(x => x.Recipients).ToHashSet();
		var slices = previous.Concat(revised).GroupBy(x => x.Scope)
			.Select(group => new NotificationSourceAudienceSliceDto(group.Key, group.SelectMany(x => x.Recipients).Distinct().ToArray()))
			.ToArray();
		var recipients = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		var settings = AcademicCalendarEventMaterial.From(record.Day);
		var variables = Variables(schoolName, record.Day.Title, settings.Date, settings.Venue, "The event details have changed.");
		var earlier = record.Change.Before[0];
		var removedContent = recipients.Where(x => !currentRecipients.Contains(x)).Select(x =>
			new NotificationSourceRecipientContentDto(x, Variables(schoolName, record.Change.PreviousTitle,
				earlier.Date, earlier.Venue, "The previous event details no longer apply to your division."), Link)).ToArray();
		return new(true, null, record.Change.Revision, Link, variables, recipients, expires, slices, removedContent);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != EventKey || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(CalendarPermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		foreach (var scope in record.Change.Before.Concat(record.Change.After).Select(row => row.Scope(record.Scope)).Distinct())
		{
			try
			{
				await RequireScopeAsync(school, caller, scope, token);
				return;
			}
			catch (AbpAuthorizationException) { }
		}
		throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);

	internal Task<NotificationRuleScope> RequireScopeAsync(Guid school, UserScopeContext caller, NotificationSourceScopeDto scope,
		CancellationToken token) => access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
		school, caller, token);

	private async Task<IReadOnlyList<NotificationSourceAudienceSliceDto>> ReadAudienceAsync(Guid school, NotificationSourceScopeDto medium,
		NotificationSourceScopeDto? requested, IReadOnlyList<AcademicCalendarEventMaterial> rows, CancellationToken token)
	{
		if (requested != null)
			return rows.Any(row => row.Includes(requested)) ? await audience.ReadAsync(school, medium, requested, token) : [];
		if (rows.Any(row => row.AppliesToAll)) return await audience.ReadAsync(school, medium, null, token);
		var result = new List<NotificationSourceAudienceSliceDto>();
		foreach (var scope in rows.Select(row => row.Scope(medium)).Distinct())
			result.AddRange(await audience.ReadAsync(school, medium, scope, token));
		return result;
	}

	private async Task<(AcademicCalendar Calendar, AcademicCalendarDay Day, NotificationSourceScopeDto Scope, AcademicCalendarEventChange Change)?>
		FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var record = await queries.SingleOrDefaultAsync(from day in await days.GetQueryableAsync()
			join calendar in await calendars.GetQueryableAsync() on day.AcademicCalendarId equals calendar.Id
			where day.Id == id && day.TenantId == school && !day.IsDeleted && calendar.TenantId == school && !calendar.IsDeleted
			select new { Day = day, Calendar = calendar }, token);
		if (record == null || record.Calendar.GetProperty<string>(Property(id)) is not { } json ||
			await publication.ReadScopeAsync(school, record.Calendar, token) is not { } scope) return null;
		var change = NotificationRuntimeJson.Deserialize<AcademicCalendarEventChange>(json, NotificationErrorCodes.SourceUnavailable);
		return (record.Calendar, record.Day, scope, change);
	}

	private static Dictionary<string, NotificationVariableValueDto> Variables(string schoolName, string title, DateTime date, string? venue,
		string summary) => new(StringComparer.Ordinal)
	{
		["school_name"] = new(NotificationVariableType.Text, schoolName),
		["event_title"] = new(NotificationVariableType.Text, title),
		["event_date"] = new(NotificationVariableType.Date, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
		["location"] = new(NotificationVariableType.Text, string.IsNullOrWhiteSpace(venue) ? "See the Academic Calendar" : venue),
		["change_summary"] = new(NotificationVariableType.Text, summary)
	};

	private static string Property(Guid id) => $"AcademicCalendarEventChangedNotice:{id:N}";
}

[RemoteService(false)]
public sealed class AcademicCalendarEventChangedNotification(AcademicCalendarPublicationSource publication,
	AcademicCalendarEventChangedSource source, IRepository<AcademicCalendar, Guid> calendars, INotificationIntentWriter writer,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries,
	IUserScopeService scopes, IPermissionChecker permissions, ICurrentTenant tenant, ICurrentUser current,
	ICancellationTokenProvider cancellation, IUnitOfWorkManager units) : ITransientDependency
{
	internal async Task RecordAsync(AcademicCalendar calendar, Guid anchor, AcademicCalendarEventMaterial[] before,
		IReadOnlyCollection<AcademicCalendarDay> saved, string previousTitle)
	{
		if (units.Current?.Options.IsTransactional != true || tenant.Id is not Guid school || current.TenantId != school ||
			calendar.TenantId != school || calendar.Status != CalendarStatus.Published || current.Id is not Guid user ||
			before.Length == 0 || saved.Count == 0 || !await permissions.IsGrantedAsync(CalendarPermissions.Edit)) return;
		var after = saved.Select(AcademicCalendarEventMaterial.From).ToArray();
		if (before.ToHashSet().SetEquals(after)) return;

		var token = cancellation.Token;
		var medium = await publication.ReadScopeAsync(school, calendar, token);
		if (medium == null) return;
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) return;
		try
		{
			foreach (var scope in before.Concat(after).Select(row => row.Scope(medium)).Distinct())
				await source.RequireScopeAsync(school, caller, scope, token);
		}
		catch (AbpAuthorizationException) { return; }

		var revision = AcademicCalendarEventChangedSource.Record(calendar, anchor, before, after, previousTitle);
		await calendars.UpdateAsync(calendar, cancellationToken: token);
		var applicable = (await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.IsEnabled && x.EventKey == AcademicCalendarEventChangedSource.EventKey &&
			(!x.AcademicYearId.HasValue || x.AcademicYearId == medium.AcademicYearId) &&
			(!x.BoardId.HasValue || x.BoardId == medium.BoardId) && (!x.MediumId.HasValue || x.MediumId == medium.MediumId));
		if (!await queries.AnyAsync(applicable, token)) return;
		await writer.WriteAsync(new(AcademicCalendarEventChangedSource.EventKey, AcademicCalendarPublicationSource.Type,
			anchor, revision, DateTimeOffset.UtcNow, [], ActorUserId: user), token);
	}
}
