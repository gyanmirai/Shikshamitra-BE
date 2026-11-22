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
using ClosurePermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_EmergencyClosure;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicEmergencyClosureAudience(bool Teachers, bool Guardians, bool Students)
{
	internal AcademicEmergencyClosureAudience Restrict(AcademicEmergencyClosureAudience selected) =>
		new(Teachers && selected.Teachers, Guardians && selected.Guardians, Students && selected.Students);

	internal IReadOnlyList<string> Selectors()
	{
		var selectors = new List<string>();
		if (Teachers) selectors.Add("AssignedTeacher");
		if (Guardians) selectors.AddRange(["PrimaryGuardian", "AdditionalGuardians"]);
		if (Students) selectors.Add("StudentAccount");
		return selectors;
	}
}

internal sealed record AcademicEmergencyClosurePlacement(AcademicCalendarEventMaterial Material, string Title)
{
	internal static AcademicEmergencyClosurePlacement From(AcademicCalendarDay day) => new(AcademicCalendarEventMaterial.From(day), day.Title);
}

[RemoteService(false), ExposeServices(typeof(AcademicEmergencyClosureNotificationSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicEmergencyClosureNotificationSource(AcademicCalendarPublicationSource publication,
	AcademicCalendarNotificationAudience audience, IRepository<AcademicCalendar, Guid> calendars,
	IRepository<AcademicCalendarDay, Guid> days, IUserScopeService scopes, AcademicNotificationRuleScopeAccess access,
	IPermissionChecker permissions, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser current,
	ITenantStore tenants, NotificationSchoolTimeZone schoolZone)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Declared = AcademicNotificationEventKeys.CalendarEmergencyClosureDeclared;
	internal const string Changed = AcademicNotificationEventKeys.CalendarEmergencyClosureChanged;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { Declared, Changed });

	private sealed record ClosureNotice(string Revision, string EventKey, AcademicEmergencyClosurePlacement Current,
		CalendarDayStatus Status, AcademicEmergencyClosureAudience Audience, AcademicEmergencyClosurePlacement? Previous);

	internal static AcademicEmergencyClosureAudience? ReadAudience(AcademicCalendar calendar, Guid dayId) =>
		calendar.GetProperty<string>(SelectionProperty(dayId)) is { } json
			? NotificationRuntimeJson.Deserialize<AcademicEmergencyClosureAudience>(json, NotificationErrorCodes.SourceUnavailable)
			: ReadNotice(calendar, dayId)?.Audience;

	internal static void SaveAudience(AcademicCalendar calendar, Guid dayId, AcademicEmergencyClosureAudience selection)
	{
		calendar.SetProperty(SelectionProperty(dayId), NotificationRuntimeJson.Serialize(selection, NotificationErrorCodes.SourceUnavailable));
		if (ReadNotice(calendar, dayId) is not { } notice) return;
		var remaining = notice.Audience.Restrict(selection);
		if (remaining == notice.Audience) return;
		calendar.SetProperty(Property(dayId),
			NotificationRuntimeJson.Serialize(notice with { Audience = remaining }, NotificationErrorCodes.SourceUnavailable));
	}

	internal static string Save(AcademicCalendar calendar, AcademicCalendarDay day, string eventKey,
		AcademicEmergencyClosureAudience selection, AcademicEmergencyClosurePlacement? previous)
	{
		var notice = new ClosureNotice(Guid.NewGuid().ToString("N"), eventKey, AcademicEmergencyClosurePlacement.From(day),
			day.Status, selection, previous);
		calendar.RemoveProperty(SelectionProperty(day.Id));
		calendar.SetProperty(Property(day.Id), NotificationRuntimeJson.Serialize(notice, NotificationErrorCodes.SourceUnavailable));
		return notice.Revision;
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != AcademicCalendarPublicationSource.Type ||
			!EventKeys.Contains(context.EventKey) || await FindAsync(school, context.SourceId, token) is not { } record ||
			record.Calendar.Status == CalendarStatus.Archived || record.Notice.EventKey != context.EventKey ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != record.Notice.Revision) ||
			record.Day.Status != record.Notice.Status ||
			AcademicCalendarEventMaterial.From(record.Day) != record.Notice.Current.Material)
			return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		var currentPlacement = AcademicEmergencyClosurePlacement.From(record.Day);
		var previous = record.Notice.Previous;
		var date = previous != null && previous.Material.Date > record.Day.Date.Date ? previous.Material.Date : record.Day.Date.Date;
		if (string.IsNullOrWhiteSpace(schoolName) || date == DateTime.MaxValue.Date) return Unavailable();
		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		var expires = NotificationDeliveryTimingPolicy.Boundary(date.AddDays(1), zone, later: false);
		if (context.OccurredAt >= expires) return Unavailable();

		var selectors = record.Notice.Audience.Selectors();
		if (selectors.Count == 0) return Unavailable();
		var revised = await ReadAudienceAsync(school, record.Scope, context.SourceScope, currentPlacement.Material, token);
		var earlier = previous != null && previous.Material.Scope(record.Scope) != currentPlacement.Material.Scope(record.Scope)
			? await ReadAudienceAsync(school, record.Scope, context.SourceScope, previous.Material, token) : [];
		var revisedRecipients = revised.SelectMany(x => x.Recipients).ToHashSet();
		var slices = earlier.Concat(revised).GroupBy(x => x.Scope)
			.Select(group => new NotificationSourceAudienceSliceDto(group.Key, group.SelectMany(x => x.Recipients).Distinct().ToArray())).ToArray();
		var recipients = slices.SelectMany(x => x.Recipients).Distinct().ToArray();
		var removed = previous == null ? [] : recipients.Where(x => !revisedRecipients.Contains(x)).Select(x =>
			new NotificationSourceRecipientContentDto(x,
				Variables(schoolName, previous, "The previous closure no longer applies to your class."), Link)).ToArray();
		var summary = record.Day.Status == CalendarDayStatus.Cancelled ? "The emergency closure has been lifted." :
			ScheduleSummary(currentPlacement.Material, context.EventKey == Declared);
		return new(true, null, record.Notice.Revision, Link, Variables(schoolName, currentPlacement, summary), recipients,
			expires, slices, removed)
		{
			DeliveryLimits = new(selectors, AcademicNotificationCatalogue.GetDefinition(context.EventKey).SupportedChannels)
		};
	}

	private Task<IReadOnlyList<NotificationSourceAudienceSliceDto>> ReadAudienceAsync(Guid school, NotificationSourceScopeDto medium,
		NotificationSourceScopeDto? requested, AcademicCalendarEventMaterial material, CancellationToken token) =>
		requested != null && !material.Includes(requested) ? Task.FromResult<IReadOnlyList<NotificationSourceAudienceSliceDto>>([]) :
			audience.ReadAsync(school, medium, requested ?? (material.AppliesToAll ? null : material.Scope(medium)), token);

	private static Dictionary<string, NotificationVariableValueDto> Variables(string schoolName, AcademicEmergencyClosurePlacement placement,
		string summary) => new(StringComparer.Ordinal)
	{
		["school_name"] = new(NotificationVariableType.Text, schoolName),
		["event_title"] = new(NotificationVariableType.Text, placement.Title),
		["event_date"] = new(NotificationVariableType.Date, placement.Material.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
		["change_summary"] = new(NotificationVariableType.Text, summary)
	};

	private static string ScheduleSummary(AcademicCalendarEventMaterial material, bool declared)
	{
		var action = declared ? "An emergency closure is in effect" : "The emergency closure has changed";
		return material.Start is TimeSpan start && material.End is TimeSpan end
			? $"{action} from {start.ToString(@"hh\:mm", CultureInfo.InvariantCulture)} to {end.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}."
			: $"{action} for the full school day.";
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(ClosurePermissions.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		try
		{
			await RequireScopeAsync(school, caller, record.Notice.Current.Material.Scope(record.Scope), token);
		}
		catch (AbpAuthorizationException) when (record.Notice.Previous != null)
		{
			await RequireScopeAsync(school, caller, record.Notice.Previous.Material.Scope(record.Scope), token);
		}
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);

	internal Task<NotificationRuleScope> RequireScopeAsync(Guid school, UserScopeContext caller, NotificationSourceScopeDto scope,
		CancellationToken token) => access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId),
			school, caller, token);

	private async Task<(AcademicCalendar Calendar, AcademicCalendarDay Day, NotificationSourceScopeDto Scope, ClosureNotice Notice)?>
		FindAsync(Guid school, Guid sourceId, CancellationToken token)
	{
		var record = await queries.SingleOrDefaultAsync(from day in await days.GetQueryableAsync()
			join calendar in await calendars.GetQueryableAsync() on day.AcademicCalendarId equals calendar.Id
			where day.Id == sourceId && day.TenantId == school && !day.IsDeleted && day.DayType == DayType.EmergencyClosure &&
				calendar.TenantId == school && !calendar.IsDeleted
			select new { Day = day, Calendar = calendar }, token);
		if (record == null || ReadNotice(record.Calendar, sourceId) is not { } notice ||
			await publication.ReadScopeAsync(school, record.Calendar, token) is not { } scope) return null;
		return (record.Calendar, record.Day, scope, notice);
	}

	private static ClosureNotice? ReadNotice(AcademicCalendar calendar, Guid id) =>
		calendar.GetProperty<string>(Property(id)) is { } json
			? NotificationRuntimeJson.Deserialize<ClosureNotice>(json, NotificationErrorCodes.SourceUnavailable)
			: null;

	private static string Property(Guid id) => $"AcademicEmergencyClosureNotice:{id:N}";
	private static string SelectionProperty(Guid id) => $"AcademicEmergencyClosureAudience:{id:N}";
}

[RemoteService(false)]
public sealed class AcademicEmergencyClosureNotification(AcademicCalendarPublicationSource publication,
	AcademicEmergencyClosureNotificationSource source, IRepository<AcademicCalendar, Guid> calendars,
	INotificationIntentWriter writer, IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries,
	IUserScopeService scopes, IPermissionChecker permissions, ICurrentTenant tenant, ICurrentUser current,
	ICancellationTokenProvider cancellation, IUnitOfWorkManager units) : ITransientDependency
{
	internal Task DeclareAsync(AcademicCalendar calendar, AcademicCalendarDay day, AcademicEmergencyClosureAudience selection) =>
		RecordAsync(calendar, day, selection, AcademicEmergencyClosureNotificationSource.Declared, ClosurePermissions.Add);

	internal Task ChangeAsync(AcademicCalendar calendar, AcademicCalendarDay day, AcademicEmergencyClosurePlacement previous,
		AcademicEmergencyClosureAudience selection) =>
		RecordAsync(calendar, day, selection, AcademicEmergencyClosureNotificationSource.Changed, ClosurePermissions.Edit, previous);

	internal async Task ReopenAsync(AcademicCalendarDay day)
	{
		var calendar = await calendars.GetAsync(day.AcademicCalendarId, cancellationToken: cancellation.Token);
		if (AcademicEmergencyClosureNotificationSource.ReadAudience(calendar, day.Id) is { } selection)
			await RecordAsync(calendar, day, selection, AcademicEmergencyClosureNotificationSource.Changed, ClosurePermissions.Delete);
	}

	private async Task RecordAsync(AcademicCalendar calendar, AcademicCalendarDay day, AcademicEmergencyClosureAudience selection,
		string eventKey, string permission, AcademicEmergencyClosurePlacement? previous = null)
	{
		if (units.Current?.Options.IsTransactional != true || tenant.Id is not Guid school || current.TenantId != school ||
			calendar.TenantId != school || calendar.Status == CalendarStatus.Archived || day.TenantId != school ||
			current.Id is not Guid user || !await permissions.IsGrantedAsync(permission)) return;
		var token = cancellation.Token;
		if (await publication.ReadScopeAsync(school, calendar, token) is not { } medium) return;
		var placement = AcademicCalendarEventMaterial.From(day).Scope(medium);
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) return;
		var priorPlacement = previous?.Material.Scope(medium);
		try
		{
			await source.RequireScopeAsync(school, caller, placement, token);
			if (priorPlacement != null && priorPlacement != placement)
				await source.RequireScopeAsync(school, caller, priorPlacement, token);
		}
		catch (AbpAuthorizationException) { return; }

		if (previous?.Material == AcademicCalendarEventMaterial.From(day))
		{
			if (selection != AcademicEmergencyClosureNotificationSource.ReadAudience(calendar, day.Id))
				AcademicEmergencyClosureNotificationSource.SaveAudience(calendar, day.Id, selection);
			return;
		}
		var revision = AcademicEmergencyClosureNotificationSource.Save(calendar, day, eventKey, selection, previous);
		var allClasses = !placement.ClassId.HasValue || priorPlacement is { ClassId: null };
		var priorClass = priorPlacement?.ClassId;
		if (selection.Selectors().Count == 0) return;
		var applicable = (await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.IsEnabled && x.EventKey == eventKey &&
			(!x.AcademicYearId.HasValue || x.AcademicYearId == medium.AcademicYearId) &&
			(!x.BoardId.HasValue || x.BoardId == medium.BoardId) && (!x.MediumId.HasValue || x.MediumId == medium.MediumId) &&
			(allClasses || !x.ClassId.HasValue || x.ClassId == placement.ClassId || x.ClassId == priorClass));
		if (await queries.AnyAsync(applicable, token))
			await writer.WriteAsync(new(eventKey, AcademicCalendarPublicationSource.Type, day.Id, revision, DateTimeOffset.UtcNow,
				[], ActorUserId: user), token);
	}
}
