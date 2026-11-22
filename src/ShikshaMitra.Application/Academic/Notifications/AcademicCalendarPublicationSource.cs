using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using CalendarPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(AcademicCalendarPublicationSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicCalendarPublicationSource(IRepository<AcademicCalendar, Guid> calendars,
	IRepository<EducationMedium, Guid> mediums, AcademicCalendarNotificationAudience audience, IUserScopeService scopes,
	AcademicNotificationRuleScopeAccess access, IPermissionChecker permissions, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ICurrentUser current, ITenantStore tenants, NotificationSchoolTimeZone schoolZone)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.CalendarPublished;
	internal const string Type = "Academic.Calendar";
	private const string Property = "AcademicCalendarPublicationNotice";
	private sealed record Publication(string Revision, string Title, DateTime Start, DateTime End, NotificationSourceScopeDto Scope);
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	internal async Task<NotificationSourceScopeDto?> ReadScopeAsync(Guid school, AcademicCalendar calendar, CancellationToken token)
	{
		if (calendar.TenantId != school || calendar.IsDeleted || !calendar.AcademicMediumId.HasValue) return null;
		return await queries.SingleOrDefaultAsync((await mediums.GetQueryableAsync()).Where(x =>
			x.Id == calendar.AcademicMediumId && x.TenantId == school && !x.IsDeleted &&
			x.EducationBoard.TenantId == school && !x.EducationBoard.IsDeleted &&
			x.EducationBoard.AcademicYear.TenantId == school && !x.EducationBoard.AcademicYear.IsDeleted &&
			(!calendar.AcademicYearId.HasValue || calendar.AcademicYearId == x.EducationBoard.AcademicYearId))
			.Select(x => new NotificationSourceScopeDto(x.EducationBoard.AcademicYearId, x.EducationBoardId, x.Id, null, null)), token);
	}

	internal static string Record(AcademicCalendar calendar, NotificationSourceScopeDto scope)
	{
		var published = Describe(calendar, scope, Guid.NewGuid().ToString("N"));
		calendar.SetProperty(Property, NotificationRuntimeJson.Serialize(published, NotificationErrorCodes.SourceUnavailable));
		return published.Revision;
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != Type || context.EventKey != EventKey ||
			await FindCalendarAsync(school, context.SourceId, token) is not { Status: CalendarStatus.Published } calendar ||
			calendar.GetProperty<string>(Property) is not { } json ||
			await ReadScopeAsync(school, calendar, token) is not { } scope) return Unavailable();
		var publication = NotificationRuntimeJson.Deserialize<Publication>(json, NotificationErrorCodes.SourceUnavailable);
		if (publication != Describe(calendar, scope, publication.Revision) ||
			(!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != publication.Revision)) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName) || calendar.EndDate.Date < calendar.StartDate.Date ||
			calendar.EndDate.Date == DateTime.MaxValue.Date) return Unavailable();
		TimeZoneInfo zone;
		try { zone = await schoolZone.GetAsync(token); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return Unavailable(); }
		var expires = NotificationDeliveryTimingPolicy.Boundary(calendar.EndDate.Date.AddDays(1), zone, later: false);
		if (context.OccurredAt >= expires) return Unavailable();

		var slices = await audience.ReadAsync(school, scope, context.SourceScope, token);
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["event_title"] = new(NotificationVariableType.Text, calendar.Name),
			["event_date"] = new(NotificationVariableType.Date, calendar.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
		};
		return new(true, null, publication.Revision, Link, variables, slices.SelectMany(x => x.Recipients).Distinct().ToArray(),
			expires, AudienceSlices: slices);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != EventKey || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(CalendarPermissions.View) ||
			await FindCalendarAsync(school, sourceId, token) is not { } calendar ||
			await ReadScopeAsync(school, calendar, token) is not { } scope) throw new AbpAuthorizationException();
		await EnsureMediumScopeAsync(school, user, scope, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);

	internal static string? CurrentRevision(AcademicCalendar calendar) =>
		calendar.GetProperty<string>(Property) is { } json
			? NotificationRuntimeJson.Deserialize<Publication>(json, NotificationErrorCodes.SourceUnavailable).Revision
			: null;

	internal async Task EnsureMediumScopeAsync(Guid school, Guid user, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId), school, caller, token);
	}

	private async Task<AcademicCalendar?> FindCalendarAsync(Guid school, Guid id, CancellationToken token) =>
		await queries.SingleOrDefaultAsync((await calendars.GetQueryableAsync()).Where(x =>
			x.Id == id && x.TenantId == school && !x.IsDeleted), token);

	private static Publication Describe(AcademicCalendar calendar, NotificationSourceScopeDto scope, string revision) =>
		new(revision, calendar.Name, calendar.StartDate.Date, calendar.EndDate.Date, scope);
}
