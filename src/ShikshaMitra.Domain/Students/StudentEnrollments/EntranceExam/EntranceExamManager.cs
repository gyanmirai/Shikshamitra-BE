using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System.Linq;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamManager(IEntranceExamRepository entranceExamRepository, IRepository<EntranceExamSlot, Guid> entranceExamSlotRepository) : DomainService
{
	public async Task ValidateSlotCapacityAsync(Guid entranceExamSlotId, DateTimeOffset examDate, Guid? excludingExamId = null)
	{
		// Get the slot to check its maximum capacity
		var slot = await entranceExamSlotRepository.FindAsync(entranceExamSlotId) ?? throw new BusinessException("The specified entrance exam slot does not exist.");

		// Count students already assigned to this slot on this date
		var queryable = await entranceExamRepository.GetQueryableAsync();
		var dateOnly = examDate.Date;

		var query = queryable.Where(e =>
			e.EntranceExamSlotId == entranceExamSlotId &&
			e.ExamDate.Date == dateOnly);

		// Exclude the current exam if it's an update operation
		if (excludingExamId.HasValue)
		{
			query = query.Where(e => e.Id != excludingExamId.Value);
		}

		var currentCount = await AsyncExecuter.CountAsync(query);

		// Check if adding one more student would exceed the slot's capacity
		if (currentCount >= slot.StudentLimit)
		{
			throw new BusinessException(
				"The selected exam slot has reached its maximum capacity for the specified date. " +
				"Please choose another slot or date."
			);
		}
	}
}
