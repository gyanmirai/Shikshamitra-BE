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
	public async Task<int> GetStudentsCountForEntranceExamSlotAsync(Guid slotId, Guid? excludingStudentId = null)
	{
		// Find count of students assigned to the given entrance exam slot
		var count = await studentRepository.CountAsync(s => s.AdditionalDetail != null
			&& s.AdditionalDetail.EntranceExamSlotId == slotId
			&& (!excludingStudentId.HasValue || s.Id != excludingStudentId.Value));
		return count;
	}

	public async Task ValidateScheduling(Guid studentId, Guid slotId)
	{
		var now = DateTimeOffset.UtcNow;
		var student = await studentRepository.GetInquiryForUpdateAsync(studentId);
		ValidateSchedulingStatus(student, studentId, now);

		var slot = await entranceExamSlotRepository.GetAsync(slotId);
		await ValidateScheduling(student!, slot, now);
	}

	public async Task ValidateScheduling(Student student, EntranceExamSlot slot, DateTimeOffset now)
	{
		ValidateSchedulingStatus(student, student.Id, now);

		if (slot.StartDate <= now)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					$"Entrance exam slot {slot.SlotName} has already started and cannot be selected for scheduling.")
				.WithData("EntranceExamSlotId", slot.Id)
				.WithData("SlotStartDate", slot.StartDate);
		}

		var enrolledCount = await GetStudentsCountForEntranceExamSlotAsync(slot.Id, student.Id);

		if (enrolledCount >= slot.StudentLimit)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					$"Entrance exam slot {slot.SlotName} is already full. Student limit: {slot.StudentLimit}.")
				.WithData("EntranceExamSlotId", slot.Id)
				.WithData("StudentLimit", slot.StudentLimit);
		}

		if (student.AdditionalDetail == null)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Student registration details are missing, so an entrance exam cannot be scheduled.")
				.WithData("StudentId", student.Id);
		}

		if (student.AdditionalDetail.EntranceExamSlotId.HasValue)
		{
			if (IsMissedUnfilledEntranceExam(student, now))
			{
				return;
			}

			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Student is already scheduled for an entrance exam slot.")
				.WithData("StudentId", student.Id)
				.WithData("CurrentEntranceExamSlotId", student.AdditionalDetail.EntranceExamSlotId.Value);
		}
	}

	public void ValidateSchedulingStatus(Student? student, Guid studentId, DateTimeOffset now)
	{
		if (student == null)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Student does not exist or cannot be scheduled for an entrance exam.")
				.WithData("StudentId", studentId);
		}

		if (student.Status != StudentLifeCycleStatusEnum.RegistrationFormIssued &&
		    student.Status != StudentLifeCycleStatusEnum.Registration)
		{
			if (IsMissedUnfilledEntranceExam(student, now))
			{
				return;
			}

			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					$"Student is not in a valid status for scheduling an entrance exam. Current status: {student.Status}.")
				.WithData("StudentId", studentId)
				.WithData("CurrentStatus", student.Status);
		}
	}

	private static bool IsMissedUnfilledEntranceExam(Student student, DateTimeOffset now)
	{
		return student.Status == StudentLifeCycleStatusEnum.EntranceExam &&
		       student.AdditionalDetail?.EntranceExamEndDateTime is { } endDate &&
		       endDate < now &&
		       !student.AdditionalDetail.EntranceExamObtainedMarks.HasValue;
	}

	public async Task ValidateResultUpload(Guid studentId, double totalMarks, double passingMarks, double obtainedMarks)
	{
		var student = await studentRepository.GetAsync(studentId);

		if (student.Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					$"Student is not in a valid status for uploading entrance exam results. Current status: {student.Status}.")
				.WithData("StudentId", studentId)
				.WithData("CurrentStatus", student.Status);
		}

		if (student.AdditionalDetail == null)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Student registration details are missing, so entrance exam results cannot be uploaded.")
				.WithData("StudentId", studentId);
		}

		if (!student.AdditionalDetail.EntranceExamStartDateTime.HasValue)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Student does not have a scheduled entrance exam.")
				.WithData("StudentId", studentId);
		}

		if (obtainedMarks > totalMarks)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Obtained marks cannot be greater than total marks.")
				.WithData("StudentId", studentId)
				.WithData("ObtainedMarks", obtainedMarks)
				.WithData("TotalMarks", totalMarks);
		}

		if (passingMarks > totalMarks)
		{
			throw new BusinessException(
					StudentErrorCodes.InvalidLifecycleTransition,
					"Passing marks cannot be greater than total marks.")
				.WithData("StudentId", studentId)
				.WithData("PassingMarks", passingMarks)
				.WithData("TotalMarks", totalMarks);
		}
	}
}
