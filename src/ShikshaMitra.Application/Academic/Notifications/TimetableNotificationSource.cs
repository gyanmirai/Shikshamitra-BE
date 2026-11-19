using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Notifications;

internal sealed class TimetableNotificationScope : IUserScopedEntity
{
	public Guid Id { get; init; }
	public Guid YearId { get; init; }
	public Guid BoardId { get; init; }
	public Guid MediumId { get; init; }
	public Guid StageId { get; init; }
	public Guid StandardId { get; init; }
	public Guid DivisionId { get; init; }
	public required string ClassName { get; init; }
	public DateTime AcademicYearStart { get; init; }
	public DateTime AcademicYearEnd { get; init; }
	internal NotificationSourceScopeDto Scope => new(YearId, BoardId, MediumId, StandardId, DivisionId);
	internal NotificationRuleScope RuleScope => NotificationRuleScope.Create(YearId, BoardId, MediumId, StandardId, DivisionId);
	Guid? IUserScopedEntity.AcademicYearId => YearId;
	Guid? IUserScopedEntity.EducationBoardId => BoardId;
	Guid? IUserScopedEntity.EducationMediumId => MediumId;
	Guid? IUserScopedEntity.EducationMediumStageId => StageId;
	Guid? IUserScopedEntity.StandardId => StandardId;
	Guid? IUserScopedEntity.DivisionId => DivisionId;
}

[RemoteService(false), ExposeServices(typeof(TimetableNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class TimetableNotificationSource(ITimetableRepository timetables, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions, TimetableNotificationAudience audience, TimetableDayReminder dayReminder,
	ICurrentTenant tenant, ITenantStore tenants,
	NotificationSchoolTimeZone schoolTimeZone, IClock clock, IAsyncQueryableExecuter queries) : INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Timetable";
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.TimetablePublished, AcademicNotificationEventKeys.TimetableChanged,
		AcademicNotificationEventKeys.TimetableWithdrawn, AcademicNotificationEventKeys.TimetableDayReminder
	});
	public IReadOnlyCollection<string> EventKeys => SupportedEvents;
	public string SourceType => Type;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || tenant.Id != context.TenantId || context.SourceType != Type ||
			!EventKeys.Contains(context.EventKey) || context.SourceId == Guid.Empty)
			return LearningTaskNotificationContent.Unavailable();

		var scope = await FindScopeAsync(context.TenantId, context.SourceId, token);
		if (scope == null || (context.SourceScope != null && context.SourceScope != scope.Scope))
			return LearningTaskNotificationContent.Unavailable();
		var timetable = await timetables.FindByStandardAndDivisionAsync(scope.StandardId, scope.DivisionId);
		if (timetable == null || timetable.Id != context.SourceId || timetable.TenantId != context.TenantId)
			return LearningTaskNotificationContent.Unavailable();
		if (context.EventKey == AcademicNotificationEventKeys.TimetableDayReminder)
		{
			var schoolName = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
			return await dayReminder.LoadAsync(context, timetable, scope, schoolName, token);
		}
		var transition = TimetableNotificationTransition.Read(timetable);
		var withdrawn = context.EventKey == AcademicNotificationEventKeys.TimetableWithdrawn;
		if (transition?.EventKey != context.EventKey ||
			(withdrawn ? timetable.Status != TimetableStatus.Draft : timetable.Status != TimetableStatus.Published))
			return LearningTaskNotificationContent.Unavailable();
		var schedule = withdrawn ? [] : TimetableNotificationTransition.Schedule(timetable);
		if (!withdrawn && TimetableNotificationTransition.Fingerprint(schedule) != transition.ScheduleHash)
			return LearningTaskNotificationContent.Unavailable();

		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		var expiry = await ResolveExpiryAsync(scope, token);
		if (string.IsNullOrWhiteSpace(school) || !expiry.HasValue || expiry <= new DateTimeOffset(clock.Now.ToUniversalTime()))
			return LearningTaskNotificationContent.Unavailable();

		var learners = await audience.FindLearnersAsync(context.TenantId, scope, transition, token);
		if (learners == null) return LearningTaskNotificationContent.Unavailable();
		var staff = await audience.FindTeachersAsync(context.TenantId, transition.Teachers, token);
		var recipients = learners.Concat(staff).ToArray();
		if (recipients.Length == 0) return LearningTaskNotificationContent.Unavailable();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["class_name"] = new(NotificationVariableType.Text, scope.ClassName)
		};
		var former = staff.Where(x => !schedule.Any(slot => slot.TeacherId.HasValue &&
			(slot.TeacherId == x.EmployeeId || slot.TeacherId == x.UserId))).Select(x =>
			new NotificationSourceRecipientContentDto(x, new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
			{
				["school_name"] = variables["school_name"]
			}, LearningTaskNotificationContent.Link)).ToArray();
		return new(true, null, transition.Revision, LearningTaskNotificationContent.Link, variables, recipients, expiry,
			[new(scope.Scope, recipients)], former);
	}

	internal async Task<TimetableNotificationScope?> FindScopeAsync(Guid tenantId, Guid id, CancellationToken token)
	{
		var timetablesById = (await timetables.GetQueryableAsync()).Where(x => x.Id == id);
		var scoped = await ScopedRowsAsync(tenantId, timetablesById);
		return await queries.SingleOrDefaultAsync(scoped, token);
	}

	internal async Task<IQueryable<TimetableNotificationScope>> ScopedRowsAsync(Guid tenantId, IQueryable<TimetableEntity> candidates) =>
		from timetable in candidates
		join standard in await standards.GetQueryableAsync() on timetable.StandardId equals standard.Id
		join division in await divisions.GetQueryableAsync() on timetable.DivisionId equals division.Id
		let stage = standard.EducationMediumStage
		let medium = stage.EducationMedium
		let board = medium.EducationBoard
		let year = board.AcademicYear
		where timetable.TenantId == tenantId && !timetable.IsDeleted &&
			standard.TenantId == tenantId && division.TenantId == tenantId && division.StandardId == standard.Id &&
			stage.TenantId == tenantId && medium.TenantId == tenantId && board.TenantId == tenantId && year.TenantId == tenantId
		select new TimetableNotificationScope
		{
			Id = timetable.Id, YearId = year.Id, BoardId = board.Id, MediumId = medium.Id, StageId = stage.Id,
			StandardId = standard.Id, DivisionId = division.Id, ClassName = standard.Name,
			AcademicYearStart = year.StartDate, AcademicYearEnd = year.EndDate
		};

	private async Task<DateTimeOffset?> ResolveExpiryAsync(TimetableNotificationScope scope, CancellationToken token)
	{
		if (scope.AcademicYearEnd.Date == DateTime.MaxValue.Date) return DateTimeOffset.MaxValue;
		try
		{
			var zone = await schoolTimeZone.GetAsync(token);
			return NotificationDeliveryTimingPolicy.Boundary(scope.AcademicYearEnd.Date.AddDays(1), zone, later: true);
		}
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}
}
