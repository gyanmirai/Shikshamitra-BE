using System;

namespace ShikshaMitra.HR.Calendar.Events;

internal static class EventFactory
{
	public static Event Create(
		Guid? tenantId,
		Guid academicYearId,
		Guid eventTypeId,
		EventDetails details)
	{
		return new Event(
			Guid.NewGuid(),
			tenantId,
			academicYearId,
			eventTypeId,
			details);
	}
}
