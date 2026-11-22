using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using static ShikshaMitra.Communication.Notifications.AcademicNotificationEventKeys;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class RoomNotificationSource(IRepository<RoomReservation, Guid> reservations, IRepository<Room, Guid> rooms,
	RoomNotificationScopeReader placement, RoomNotificationReviewer reviewer, TimetableNotificationAudience staff,
	NotificationSchoolTimeZone schoolZone, ICurrentTenant tenant, ITenantStore tenants, IClock clock, IAsyncQueryableExecuter queries)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Room";
	internal const string RoomWorkspace = "/academic/roomAvailability";
	internal static IReadOnlyCollection<string> SupportedEvents { get; } =
		Array.AsReadOnly(new[] { RoomApprovalRequested, RoomApproved, RoomRejected, RoomChanged, RoomCancelled });

	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !SupportedEvents.Contains(context.EventKey))
			return Unavailable();
		var reservation = await reservations.FindAsync(x => x.Id == context.SourceId && x.TenantId == context.TenantId && !x.IsDeleted,
			cancellationToken: token);
		if (reservation == null || reservation.CreatorId is not Guid requester || requester == Guid.Empty)
			return Unavailable();
		var revision = RoomReservationNoticeRevision.Read(reservation);
		if (revision == null || revision.EventKey != context.EventKey || !MatchesState(revision.EventKey, reservation.Status) ||
			(!string.IsNullOrEmpty(context.SourceRevision) && revision.Revision != context.SourceRevision))
			return Unavailable();

		var scope = await placement.ReadAsync(context.TenantId, reservation, token);
		if (scope == null || (context.SourceScope != null && scope != context.SourceScope)) return Unavailable();
		var pending = revision.EventKey == RoomApprovalRequested;
		var expiresAt = await ResolveExpiryAsync(reservation, revision.EventKey, token);
		if (expiresAt == null || expiresAt <= new DateTimeOffset(clock.Now.ToUniversalTime())) return Unavailable();
		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		var roomName = await queries.SingleOrDefaultAsync((await rooms.GetQueryableAsync()).Where(x =>
			x.Id == reservation.RoomId && x.TenantId == context.TenantId).Select(x => x.RoomName), token);
		if (string.IsNullOrWhiteSpace(school) || string.IsNullOrWhiteSpace(roomName)) return Unavailable();

		var common = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["room_name"] = new(NotificationVariableType.Text, roomName),
			["session_date"] = new(NotificationVariableType.Date, reservation.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
			["start_time"] = new(NotificationVariableType.Text, reservation.StartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture)),
			["end_time"] = new(NotificationVariableType.Text, reservation.EndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture))
		};
		if (pending)
		{
			var target = await reviewer.FindAsync(reservation, requester, token);
			if (target == null) return Unavailable();
			return new(true, null, revision.Revision, RoomWorkspace, common, [target], expiresAt, [new(scope, [target])]);
		}

		var audience = new List<NotificationRecipientRefDto> { new("Requester", UserId: requester) };
		var safeContent = new List<NotificationSourceRecipientContentDto>();
		if (revision.EventKey is RoomChanged or RoomCancelled)
		{
			var references = new[] { reservation.TeacherId, revision.PreviousTeacherId }.OfType<Guid>().Distinct().ToArray();
			var teachers = await staff.FindTeachersAsync(context.TenantId, references, token);
			foreach (var teacher in teachers)
			{
				audience.Add(teacher);
				if (teacher.UserId != reservation.TeacherId && teacher.EmployeeId != reservation.TeacherId)
					safeContent.Add(new(teacher, new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
					{
						["school_name"] = common["school_name"]
					}, Link));
			}
		}
		return new(true, null, revision.Revision, Link, common, audience, expiresAt, [new(scope, audience)], safeContent);
	}

	private static bool MatchesState(string eventKey, RoomReservationStatus status) => eventKey switch
	{
		RoomApprovalRequested => status == RoomReservationStatus.PendingApproval,
		RoomRejected => status == RoomReservationStatus.Rejected,
		RoomCancelled => status == RoomReservationStatus.Cancelled,
		_ => RoomReservation.IsConfirmed(status)
	};

	private async Task<DateTimeOffset?> ResolveExpiryAsync(RoomReservation reservation, string eventKey, CancellationToken token)
	{
		var pending = eventKey == RoomApprovalRequested;
		var time = pending ? reservation.StartTime : reservation.EndTime;
		if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return null;
		var date = reservation.Date.Date;
		if (eventKey != RoomChanged && reservation.IsRecurring && reservation.RecurringConfigId is Guid seriesId)
		{
			var occurrences = (await reservations.GetQueryableAsync()).Where(x => x.TenantId == reservation.TenantId && !x.IsDeleted &&
				x.RecurringConfigId == seriesId && x.CreatorId == reservation.CreatorId && x.Status == reservation.Status && x.Date > date);
			var lastDate = await queries.FirstOrDefaultAsync(occurrences.OrderByDescending(x => x.Date).Select(x => (DateTime?)x.Date), token);
			if (lastDate.HasValue)
			{
				var times = await queries.ToListAsync(occurrences.Where(x => x.Date == lastDate)
					.Select(x => pending ? x.StartTime : x.EndTime), token);
				if (times.Count > 0)
				{
					date = lastDate.Value.Date;
					time = times.Max();
				}
			}
		}
		if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return null;
		var local = DateTime.SpecifyKind(date.Add(time), DateTimeKind.Unspecified);
		try { return NotificationDeliveryTimingPolicy.Boundary(local, await schoolZone.GetAsync(token), later: true); }
		catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { return null; }
	}
}
