using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotManager(IEntranceExamSlotRepository entranceExamSlotRepository) : DomainService
{
	/// <summary>
	/// Validates that there is no exam slot overlapping with the specified start and end dates.
	/// If an overlapping slot is found (optionally excluding the slot with excludingSlotId), a BusinessException is thrown.
	/// </summary>
	/// <param name="startDate">The start time of the exam slot.</param>
	/// <param name="endDate">The end time of the exam slot.</param>
	/// <param name="excludingSlotId">Optional slot id to exclude from the check (used during updates).</param>
	public async Task ValidateNoOverlappingSlotAsync(DateTimeOffset startDate, DateTimeOffset endDate, Guid? excludingSlotId = null)
	{
		var existingSlot = await entranceExamSlotRepository.FindAsync(slot =>
				slot.StartDate < endDate && startDate < slot.EndDate);

		if (existingSlot != null && (!excludingSlotId.HasValue || existingSlot.Id != excludingSlotId.Value))
		{
			throw new BusinessException("There is an overlapping exam slot with the specified start and end times.");
		}
	}
}
