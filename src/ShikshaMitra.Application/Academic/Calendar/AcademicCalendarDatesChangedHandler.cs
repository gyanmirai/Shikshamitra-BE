using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarDatesChangedHandler(AcademicCalendarAuditWriter auditWriter)
	: ILocalEventHandler<AcademicCalendarDatesChanged>, ITransientDependency
{
	public Task HandleEventAsync(AcademicCalendarDatesChanged eventData) =>
		auditWriter.WriteAsync(
			eventData.CalendarId,
			CalendarAuditAction.CalendarUpdated,
			JsonSerializer.Serialize(new { StartDate = eventData.PreviousStartDate, EndDate = eventData.PreviousEndDate }),
			JsonSerializer.Serialize(new { eventData.StartDate, eventData.EndDate }),
			entityType: "Academic Calendar",
			entityName: eventData.CalendarName,
			reason: "Calendar dates synchronized with institute setup.");
}
