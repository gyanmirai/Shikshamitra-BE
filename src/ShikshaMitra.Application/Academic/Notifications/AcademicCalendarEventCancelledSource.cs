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

[RemoteService(false), ExposeServices(typeof(AcademicCalendarEventCancelledSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicCalendarEventCancelledSource(AcademicCalendarPublicationSource publication,
	AcademicCalendarNotificationAudience audience, AcademicCalendarEventChangedSource scopeAccess,
	IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days, IDataFilter filters,
	IUserScopeService scopes, IPermissionChecker permissions, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser current,
	ITenantStore tenants, NotificationSchoolTimeZone schoolZone) : INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.CalendarEventCancelled;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	private sealed record CancellationNotice(string Revision, string? PublicationRevision);

	internal static string Record(AcademicCalendar calendar, Guid sourceId)
	{
		var cancellation = new CancellationNotice(Guid.NewGuid().ToString("N"), AcademicCalendarPublicationSource.CurrentRevision(calendar));
		calendar.SetProperty(Property(sourceId), NotificationRuntimeJson.Serialize(cancellation, NotificationErrorCodes.SourceUnavailable));
		return cancellation.Revision;
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != AcademicCalendarPublicationSource.Type ||
			context.EventKey != EventKey || await FindAsync(school, context.SourceId, token) is not { } record ||
			record.Calendar.Status != CalendarStatus.Published ||
			record.Notice.PublicationRevision != AcademicCalendarPublicationSource.CurrentRevision(record.Calendar) ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != record.Notice.Revision))
			return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		var material = AcademicCalendarEventMaterial.From(record.Day);
		if (string.IsNullOrWhiteSpace(schoolName) || material.Date == DateTime.MaxValue.Date ||
			(context.SourceScope != null && !material.Includes(context.SourceScope))) return Unavailable();
		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		var expires = NotificationDeliveryTimingPolicy.Boundary(material.Date.AddDays(1), zone, later: false);
		if (context.OccurredAt >= expires) return Unavailable();

		var placement = material.AppliesToAll ? null : material.Scope(record.Scope);
		var slices = await audience.ReadAsync(school, record.Scope, context.SourceScope ?? placement, token);
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["event_title"] = new(NotificationVariableType.Text, record.Day.Title),
			["event_date"] = new(NotificationVariableType.Date, material.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
			["change_summary"] = new(NotificationVariableType.Text, "The event has been cancelled.")
		};
		return new(true, null, record.Notice.Revision, Link, variables, slices.SelectMany(x => x.Recipients).Distinct().ToArray(),
			expires, AudienceSlices: slices);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != EventKey || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(CalendarPermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await scopeAccess.RequireScopeAsync(school, caller, AcademicCalendarEventMaterial.From(record.Day).Scope(record.Scope), token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);

	private async Task<(AcademicCalendar Calendar, AcademicCalendarDay Day, NotificationSourceScopeDto Scope, CancellationNotice Notice)?>
		FindAsync(Guid school, Guid id, CancellationToken token)
	{
		AcademicCalendar? calendar;
		AcademicCalendarDay? day;
		using (filters.Disable<ISoftDelete>())
		{
			var row = await queries.SingleOrDefaultAsync(from savedDay in await days.GetQueryableAsync()
				join savedCalendar in await calendars.GetQueryableAsync() on savedDay.AcademicCalendarId equals savedCalendar.Id
				where savedDay.Id == id && savedDay.TenantId == school && savedDay.IsDeleted && savedDay.DayType == DayType.Event &&
					savedCalendar.TenantId == school && !savedCalendar.IsDeleted
				select new { Day = savedDay, Calendar = savedCalendar }, token);
			calendar = row?.Calendar;
			day = row?.Day;
		}
		if (day == null || calendar == null || calendar.GetProperty<string>(Property(id)) is not { } json ||
			await publication.ReadScopeAsync(school, calendar, token) is not { } scope) return null;
		return (calendar, day, scope, NotificationRuntimeJson.Deserialize<CancellationNotice>(json, NotificationErrorCodes.SourceUnavailable));
	}

	private static string Property(Guid id) => $"AcademicCalendarEventCancelledNotice:{id:N}";
}

[RemoteService(false)]
public sealed class AcademicCalendarEventCancelledNotification(AcademicCalendarPublicationSource publication,
	AcademicCalendarEventChangedSource scopeAccess, IRepository<AcademicCalendar, Guid> calendars, INotificationIntentWriter writer,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries, IUserScopeService scopes, IPermissionChecker permissions,
	ICurrentTenant tenant, ICurrentUser current, ICancellationTokenProvider cancellation, IUnitOfWorkManager units) : ITransientDependency
{
	internal async Task RecordAsync(AcademicCalendarDay removed)
	{
		var key = AcademicCalendarEventCancelledSource.EventKey;
		if (units.Current?.Options.IsTransactional != true || tenant.Id is not Guid school || current.TenantId != school ||
			removed.TenantId != school || current.Id is not Guid user || !await permissions.IsGrantedAsync(CalendarPermissions.Delete)) return;
		var token = cancellation.Token;
		var calendar = await calendars.FindAsync(removed.AcademicCalendarId, cancellationToken: token);
		if (calendar == null || calendar.Status != CalendarStatus.Published ||
			await publication.ReadScopeAsync(school, calendar, token) is not { } medium) return;
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) return;
		var placement = AcademicCalendarEventMaterial.From(removed).Scope(medium);
		try { await scopeAccess.RequireScopeAsync(school, caller, placement, token); }
		catch (AbpAuthorizationException) { return; }

		var revision = AcademicCalendarEventCancelledSource.Record(calendar, removed.Id);
		var applicable = (await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.IsEnabled && x.EventKey == key &&
			(!x.AcademicYearId.HasValue || x.AcademicYearId == medium.AcademicYearId) &&
			(!x.BoardId.HasValue || x.BoardId == medium.BoardId) && (!x.MediumId.HasValue || x.MediumId == medium.MediumId) &&
			(!placement.ClassId.HasValue || !x.ClassId.HasValue || x.ClassId == placement.ClassId) &&
			(!placement.DivisionId.HasValue || !x.DivisionId.HasValue || x.DivisionId == placement.DivisionId));
		if (await queries.AnyAsync(applicable, token))
			await writer.WriteAsync(new(key, AcademicCalendarPublicationSource.Type, removed.Id, revision, DateTimeOffset.UtcNow,
				[], ActorUserId: user), token);
	}
}
