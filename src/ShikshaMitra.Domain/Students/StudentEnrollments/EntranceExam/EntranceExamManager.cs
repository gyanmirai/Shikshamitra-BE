using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamManager(IEntranceExamRepository entranceExamRepository) : DomainService
{
	/// <summary>
	/// Validates that there is no exam overlapping the specified exam slot and exam date.
	/// If an overlapping exam is found, a BusinessException is thrown.
	/// </summary>
	/// <param name="entranceExamSlotId">The exam slot identifier.</param>
	/// <param name="examDate">The date of the exam.</param>
	/// <param name="excludingExamId">
	/// Optional exam Id to exclude from the overlapping check (useful during updates).
	/// </param>
	public async Task ValidateNoOverlappingExamAsync(Guid entranceExamSlotId, DateTimeOffset examDate, Guid? excludingExamId = null)
	{
		// The following repository method is assumed to exist.
		// It should return an EntranceExam (if any) scheduled for the same slot that overlaps with the exam date.
		// Adjust the query logic in the repository based on your specific overlapping criteria.
		var existingExam = await entranceExamRepository.FindAsync(exam =>
				exam.EntranceExamSlotId == entranceExamSlotId &&
				exam.ExamDate.Date == examDate.Date);

		if (existingExam != null && (!excludingExamId.HasValue || existingExam.Id != excludingExamId.Value))
		{
			throw new BusinessException("There is an existing exam scheduled for the given slot and date which causes an overlapping conflict.");
		}
	}
}
