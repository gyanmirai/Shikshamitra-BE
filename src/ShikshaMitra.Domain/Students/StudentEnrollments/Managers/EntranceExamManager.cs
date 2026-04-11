using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Managers;

public class EntranceExamManager(
	IStudentRepository studentRepository,
	IRepository<EntranceExamSlot, Guid> entranceExamSlotRepository) : DomainService
{
	public async Task<int> GetStudentsCountForEntranceExamSlotAsync(Guid slotId)
	{
		// Find count of students assigned to the given entrance exam slot
		var count = await studentRepository.CountAsync(s => s.AdditionalDetail != null
			&& s.AdditionalDetail.EntranceExamSlotId == slotId);
		return count;
	}

	public async Task ValidateScheduling(Guid studentId, Guid slotId)
	{
		// if student not in registration phase, prevent scheduling
		var student = await studentRepository.GetAsync(studentId);

		if (student.Status != StudentLifeCycleStatusEnum.RegistrationFormIssued &&
		    student.Status != StudentLifeCycleStatusEnum.Registration)
		{
			throw new Exception(
				$"Student {studentId} is not in a valid status for scheduling an entrance exam. Current status: {student.Status}");
		}

		var slot = await entranceExamSlotRepository.GetAsync(slotId);
		if (slot == null)
		{
			throw new ArgumentException($"Entrance exam slot with ID {slotId} does not exist.");
		}
		var enrolledCount = await GetStudentsCountForEntranceExamSlotAsync(slotId);

		if (enrolledCount >= slot.StudentLimit)
		{
			throw new Exception($"Entrance exam slot {slot.SlotName} is already full. Student limit: {slot.StudentLimit}");
		}

		// if student is already scheduled for an exam, prevent scheduling to another slot

		if (student.AdditionalDetail.EntranceExamSlotId.HasValue)
		{
			throw new Exception(
				$"Student {studentId} is already scheduled for an entrance exam slot. Current slot ID: {student.AdditionalDetail.EntranceExamSlotId.Value}");
		}
	}

	public async Task ValidateResultUpload(Guid studentId, double totalMarks, double passingMarks, double obtainedMarks)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new Exception("Student not found");

		if (student.Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new Exception(
				$"Student {studentId} is not in a valid status for uploading entrance exam results. Current status: {student.Status}");
		}

		if (!student.AdditionalDetail.EntranceExamStartDateTime.HasValue)
		{
			throw new Exception(
				$"Student {studentId} does not have a scheduled entrance exam.");
		}

		if (obtainedMarks > totalMarks)
		{
			throw new Exception(
				$"Obtained marks cannot be greater than total marks for student {studentId}. Obtained marks: {obtainedMarks}, Total marks: {totalMarks}");
		}

		if (passingMarks > totalMarks)
		{
			throw new Exception(
				$"Passing marks cannot be greater than total marks for student {studentId}. Passing marks: {passingMarks}, Total marks: {totalMarks}");
		}
	}
}