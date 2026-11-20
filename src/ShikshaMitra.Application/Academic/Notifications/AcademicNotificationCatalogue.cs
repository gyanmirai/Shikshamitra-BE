using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

[ExposeServices(typeof(AcademicNotificationCatalogue), typeof(INotificationEventDefinitionProvider))]
public sealed class AcademicNotificationCatalogue : INotificationEventDefinitionProvider, ITransientDependency
{
	private static readonly IReadOnlyList<NotificationEventDefinition> Definitions = AcademicNotificationDefinitions.Create();
	private static readonly IReadOnlyDictionary<string, NotificationEventDefinition> ByKey =
		Definitions.ToDictionary(x => x.EventKey, StringComparer.Ordinal);
	private readonly HashSet<string> _supportedEvents;
	private readonly HashSet<(string SourceType, string EventKey)> _scheduledEvents;

	public AcademicNotificationCatalogue(IEnumerable<INotificationSourceAdapter> adapters,
		IEnumerable<INotificationDueSource>? dueSources = null)
	{
		_supportedEvents = adapters.SelectMany(x => x.EventKeys).ToHashSet(StringComparer.Ordinal);
		_scheduledEvents = (dueSources ?? []).SelectMany(source =>
		{
			var keys = source.ScheduledEventKeys ?? [];
			return Definitions.Where(x => x.SourceType == source.SourceType && x.TriggerType == NotificationTriggerType.Due &&
				(keys.Count == 0 || keys.Contains(x.EventKey))).Select(x => (x.SourceType, x.EventKey));
		}).ToHashSet();
	}

	public IReadOnlyList<NotificationEventDefinition> GetAll() => Definitions;

	public NotificationEventDefinition Get(string eventKey) => GetDefinition(eventKey);

	internal static NotificationEventDefinition GetDefinition(string eventKey) => eventKey != null && ByKey.TryGetValue(eventKey, out var definition)
		? definition
		: throw new BusinessException(NotificationErrorCodes.EventUnknown);

	public NotificationSourceAvailability GetAvailability(string eventKey)
	{
		var definition = Get(eventKey);
		return _supportedEvents.Contains(eventKey) &&
			(definition.TriggerType != NotificationTriggerType.Due || _scheduledEvents.Contains((definition.SourceType, eventKey)))
			? new(true, null)
			: new(false, NotificationErrorCodes.SourceUnavailable);
	}

	public void EnsureAvailable(string eventKey)
	{
		if (!GetAvailability(eventKey).IsAvailable)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
	}

	public NotificationSourceAvailability GetManualAvailability(string eventKey)
	{
		Get(eventKey);
		return eventKey is not (AcademicNotificationEventKeys.TimetableDayReminder or AcademicNotificationEventKeys.ExamReminder or
			AcademicNotificationEventKeys.CalendarEventReminder or AcademicNotificationEventKeys.ProgressBehindSchedule or
			AcademicNotificationEventKeys.ProgressMilestoneReached) &&
			_supportedEvents.Contains(eventKey)
			? new(true, null)
			: new(false, NotificationErrorCodes.SourceUnavailable);
	}

	public void EnsureSourceAvailable(string eventKey)
	{
		if (!GetManualAvailability(eventKey).IsAvailable)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
	}
}
