using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicNotificationCatalogue : ITransientDependency
{
	private static readonly IReadOnlyList<NotificationEventDefinition> Definitions = AcademicNotificationDefinitions.Create();
	private static readonly IReadOnlyDictionary<string, NotificationEventDefinition> ByKey =
		Definitions.ToDictionary(x => x.EventKey, StringComparer.Ordinal);
	private readonly HashSet<string> _supportedEvents;

	public AcademicNotificationCatalogue(IEnumerable<INotificationSourceAdapter> adapters)
	{
		_supportedEvents = adapters.SelectMany(x => x.EventKeys).ToHashSet(StringComparer.Ordinal);
	}

	public IReadOnlyList<NotificationEventDefinition> GetAll() => Definitions;

	public NotificationEventDefinition Get(string eventKey) => eventKey != null && ByKey.TryGetValue(eventKey, out var definition)
		? definition
		: throw new BusinessException(NotificationErrorCodes.EventUnknown);

	public NotificationSourceAvailability GetAvailability(string eventKey)
	{
		Get(eventKey);
		return _supportedEvents.Contains(eventKey)
			? new(true, null)
			: new(false, NotificationErrorCodes.SourceUnavailable);
	}

	public void EnsureAvailable(string eventKey)
	{
		if (!GetAvailability(eventKey).IsAvailable)
			throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
	}
}
