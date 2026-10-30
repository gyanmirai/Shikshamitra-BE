using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
	// Only the authorized Admissions planning path can supply these server-resolved offerings.
	// The public room booking DTO cannot override the calendar scope.
	internal async Task<RoomReservationDto> CreateAssessmentReservationAsync(
		CreateRoomReservationDto input, IReadOnlyCollection<AdmissionOffering> offerings)
	{
		if (input.IsRecurring)
			throw AdmissionAssessmentSchedulingService.Invalid("Assessment sessions cannot use recurring room bookings.");
		var scopes = await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentCalendarScopeService>().GetAsync(offerings);
		return await CreateReservationCoreAsync(input, scopes);
	}
}
