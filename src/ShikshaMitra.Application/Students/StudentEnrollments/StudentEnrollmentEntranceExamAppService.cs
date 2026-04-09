using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for managing entrance exam operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentEnrollmentEntranceExamAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	IRepository<EntranceExamSlot, Guid> entranceExamSlotRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository) : ShikshaMitraAppService, IStudentEnrollmentEntranceExamAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<PagedResultDto<EntranceExamStudentDto>> GetListAsync(GetEntranceExamStudentsInput input)
	{
		var studentQueryable = await studentRepository.GetQueryableAsync();
		studentQueryable = studentQueryable.Where(x => x.AdditionalDetail.EntranceExamStartDateTime >= input.StartDate && x.AdditionalDetail.EntranceExamStartDateTime <= input.EndDate);

		if (!input.SearchTerm.IsNullOrWhiteSpace())
		{
			var searchTerm = input.SearchTerm.ToLower();
			studentQueryable = studentQueryable.Where(x =>
				(x.FirstName != null && x.FirstName.ToLower().Contains(searchTerm)) ||
				(x.MiddleName != null && x.MiddleName.ToLower().Contains(searchTerm)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(searchTerm)));
		}

		var totalCount = await AsyncExecuter.CountAsync(studentQueryable);

		if (!input.Sorting.IsNullOrWhiteSpace())
		{
			studentQueryable = studentQueryable.OrderBy(input.Sorting);
		}
		else
		{
			studentQueryable = studentQueryable.OrderBy(x => x.AdditionalDetail.EntranceExamStartDateTime).ThenBy(x => x.FirstName);
		}

		studentQueryable = studentQueryable.Skip(input.SkipCount).Take(input.MaxResultCount);

		var results = await AsyncExecuter.ToListAsync(studentQueryable);
		var items = results.Select(result =>
		{
			var startTime = result.AdditionalDetail.EntranceExamStartDateTime?.TimeOfDay;
			var endTime = result.AdditionalDetail.EntranceExamEndDateTime?.TimeOfDay;
			return new EntranceExamStudentDto
			{
				StudentId = result.Id,
				StudentName = BuildStudentName(result.FirstName, result.MiddleName, result.LastName),
				EducationBoard = result.AcademicBoard.Type.ToString(),
				EducationMedium = result.AcademicMedium.Name,
				Standard = result.Standard?.Name ?? string.Empty,
				ExamDate = result.AdditionalDetail.EntranceExamStartDateTime?.DateTime,
				ExamStartTime = startTime,
				ExamEndTime = endTime,
				ExamTime = startTime.HasValue && endTime.HasValue
					? $"{startTime.Value:hh\\:mm} to {endTime.Value:hh\\:mm}"
					: null,
				ExamRoomNo = result.AdditionalDetail.EntranceExamRoomNo,
				TotalMarks = result.AdditionalDetail.EntranceExamTotalMarks,
				PassingMarks = result.AdditionalDetail.EntranceExamPassingMarks,
				ObtainedMarks = result.AdditionalDetail.EntranceExamObtainedMarks,
				Percentage = result.AdditionalDetail.EntranceExamPercentage,
				ExamStatus = result.AdditionalDetail.EntranceExamStatus,
				ExamStatusDisplay = result.AdditionalDetail.EntranceExamStatus?.ToString() ?? "Not Scheduled",
				EnrollmentState = result.Status,
				Progress = CalculateRegistrationProgressPercentage(result.AdditionalDetail.StudentRegistrationState)
			};
		}).ToList();

		return new PagedResultDto<EntranceExamStudentDto>(totalCount, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<EntranceExamDetailDto> GetAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true) ?? throw new EntityNotFoundException(typeof(Student), studentId);

		return new EntranceExamDetailDto
		{
			StudentId = student.Id,
			StudentName = BuildStudentName(student.FirstName, student.MiddleName, student.LastName),
			InquiryNo = student.AdditionalDetail.InquiryNo,
			RegistrationFormNo = student.AdditionalDetail.RegistrationFormNo,
			EducationBoard = student.AcademicBoard.Type.ToString(),
			EducationMedium = student.AcademicMedium.Name,
			Standard = student.Standard?.Name ?? string.Empty,
			ExamScheduled = student.AdditionalDetail.EntranceExamStartDateTime.HasValue,
			EntranceExamSlotId = student.AdditionalDetail.EntranceExamSlotId,
			ExamDate = student.AdditionalDetail.EntranceExamStartDateTime?.DateTime,
			ExamStartTime = student.AdditionalDetail.EntranceExamStartDateTime?.TimeOfDay,
			ExamEndTime = student.AdditionalDetail.EntranceExamEndDateTime?.TimeOfDay,
			ExamRoomNo = student.AdditionalDetail.EntranceExamRoomNo,
			ResultUploaded = student.AdditionalDetail.EntranceExamObtainedMarks.HasValue,
			TotalMarks = student.AdditionalDetail.EntranceExamTotalMarks,
			PassingMarks = student.AdditionalDetail.EntranceExamPassingMarks,
			ObtainedMarks = student.AdditionalDetail.EntranceExamObtainedMarks,
			Percentage = student.AdditionalDetail.EntranceExamPercentage,
			ExamStatus = student.AdditionalDetail.EntranceExamStatus,
			ResultDate = student.AdditionalDetail.EntranceExamResultDate?.DateTime,
			EnrollmentState = student.Status,
			CanMoveToAdmission = student.AdditionalDetail.EntranceExamStatus == EntranceExamStatusEnum.Pass
		};
	}

	public async Task ScheduleExamAsync(Guid studentId, ScheduleEntranceExamDto input)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true) ?? throw new EntityNotFoundException(typeof(Student), studentId);

		if (student.Status != StudentLifeCycleStatusEnum.RegistrationFormIssued &&
		    student.Status != StudentLifeCycleStatusEnum.Registation)
		{
			throw new BusinessException("SM:EntranceExam:001")
				.WithData("currentStatus", student.Status.ToString())
				.WithData("requiredStatus", StudentLifeCycleStatusEnum.RegistrationFormIssued.ToString());
		}

		var slot = await entranceExamSlotRepository.GetAsync(input.EntranceExamSlotId);

		var enrolledCount = await GetEnrolledStudentsCountInSlotAsync(input.EntranceExamSlotId);
		if (enrolledCount >= slot.StudentLimit)
		{
			throw new BusinessException("SM:EntranceExam:002")
				.WithData("slotId", input.EntranceExamSlotId)
				.WithData("capacity", slot.StudentLimit);
		}

		if (student.AdditionalDetail?.EntranceExamSlotId.HasValue == true)
		{
			throw new BusinessException("SM:EntranceExam:003")
				.WithData("existingSlotId", student.AdditionalDetail.EntranceExamSlotId.Value);
		}

		student.ScheduleEntranceExam(slot.Id, slot.StartDate, slot.EndDate, slot.RoomNo);
		await studentRepository.UpdateAsync(student, autoSave: true);
	}

	public async Task<EntranceExamResultResponseDto> UploadResultAsync(Guid studentId, UploadEntranceExamResultDto input)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new EntityNotFoundException(typeof(Student), studentId);
		var additionalDetail = await studentAdditionalDetailRepository.GetAsync(studentId) ?? throw new EntityNotFoundException(typeof(StudentAdditionalDetail), studentId);

		if (student.Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new BusinessException("SM:EntranceExam:004")
				.WithData("currentStatus", student.Status.ToString())
				.WithData("requiredStatus", StudentLifeCycleStatusEnum.EntranceExam.ToString());
		}

		if (!additionalDetail.EntranceExamStartDateTime.HasValue)
		{
			throw new BusinessException("SM:EntranceExam:005")
				.WithData("studentId", studentId);
		}

		if (input.ObtainedMarks > input.TotalMarks)
		{
			throw new BusinessException("SM:EntranceExam:006")
				.WithData("obtainedMarks", input.ObtainedMarks)
				.WithData("totalMarks", input.TotalMarks);
		}

		if (input.PassingMarks > input.TotalMarks)
		{
			throw new BusinessException("SM:EntranceExam:007")
				.WithData("passingMarks", input.PassingMarks)
				.WithData("totalMarks", input.TotalMarks);
		}

		student.AdditionalDetail.RecordExamResult(input.TotalMarks, input.PassingMarks, input.ObtainedMarks);
		await studentRepository.UpdateAsync(student, autoSave: true);

		return new EntranceExamResultResponseDto
		{
			StudentId = student.Id,
			StudentName = BuildStudentName(student.FirstName, student.MiddleName, student.LastName),
			TotalMarks = input.TotalMarks,
			PassingMarks = input.PassingMarks,
			ObtainedMarks = input.ObtainedMarks,
			Percentage = additionalDetail.EntranceExamPercentage!.Value,
			ExamStatus = additionalDetail.EntranceExamStatus!.Value,
			ResultDate = additionalDetail.EntranceExamResultDate!.Value.DateTime
		};
	}

	public async Task MoveToAdmissionAsync(Guid studentId, Guid? academicYearId = null)
	{
		// OR-permission: user needs EntranceExam.Add OR RegistrationFormIssued.Add.
		// TODO: Replace with a named ABP authorization policy defined in the module setup.
		var hasEntranceExamPermission = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add);
		var hasRegistrationFormIssuedPermission = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_RegistrationFormIssued.Add);

		if (!hasEntranceExamPermission && !hasRegistrationFormIssuedPermission)
		{
			throw new AbpAuthorizationException(
				"You need either Enrollment_EntranceExam.Add or Enrollment_RegistrationFormIssued.Add permission.");
		}

		var student = await studentRepository.GetAsync(studentId, includeDetails: true);

		if (!IsStudentAllowedToMoveToAdmission(student))
		{
			throw new BusinessException("SM:EntranceExam:008")
				.WithData("currentStatus", student.Status.ToString());
		}

		student.MoveToAdmission();
		await studentRepository.UpdateAsync(student, autoSave: true);
	}

	public async Task<BulkMoveResultDto> BulkMoveToAdmissionAsync(BulkMoveToAdmissionDto input)
	{
		// Pre-load all students in a single query for name resolution — avoids per-iteration DB calls
		var studentQueryable = await studentRepository.GetQueryableAsync();
		var preLoaded = await AsyncExecuter.ToListAsync(
			studentQueryable.Where(s => input.StudentIds.Contains(s.Id)));
		var nameMap = preLoaded.ToDictionary(
			s => s.Id,
			s => BuildStudentName(s.FirstName, s.MiddleName, s.LastName));

		var result = new BulkMoveResultDto
		{
			TotalRequested = input.StudentIds.Count,
			SuccessCount = 0,
			FailureCount = 0,
			SucceededStudents = [],
			FailedStudents = []
		};

		foreach (var studentId in input.StudentIds)
		{
			var studentName = nameMap.GetValueOrDefault(studentId, "Unknown Student");
			try
			{
				await MoveToAdmissionAsync(studentId, input.AcademicYearId);
				result.SucceededStudents.Add(new StudentSummaryDto { StudentId = studentId, StudentName = studentName });
				result.SuccessCount++;
			}
			catch (Exception ex)
			{
				result.FailedStudents.Add(new FailedStudentDto { StudentId = studentId, StudentName = studentName, Reason = ex.Message });
				result.FailureCount++;
			}
		}

		return result;
	}

	private bool IsStudentAllowedToMoveToAdmission(Student student)
	{
		var progress = CalculateRegistrationProgressPercentage(student.AdditionalDetail!.StudentRegistrationState);

		if (student.Status == StudentLifeCycleStatusEnum.Registation &&
		    progress >= EnrollmentConsts.MoveToAdmissionThreasold)
		{
			return true;
		}

		if (student.Status == StudentLifeCycleStatusEnum.EntranceExam &&
		    progress >= EnrollmentConsts.MoveToAdmissionThreasold &&
		    student.AdditionalDetail.EntranceExamObtainedMarks >= student.AdditionalDetail.EntranceExamPassingMarks)
		{
			return true;
		}

		return false;
	}

	private async Task<int> GetEnrolledStudentsCountInSlotAsync(Guid slotId)
	{
		var queryable = await studentAdditionalDetailRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(queryable.Where(sd => sd.EntranceExamSlotId == slotId));
	}

	private static string BuildStudentName(string? firstName, string? middleName, string? lastName)
		=> new[] { firstName, middleName, lastName }
			.Where(s => !string.IsNullOrWhiteSpace(s))
			.DefaultIfEmpty(string.Empty)
			.Aggregate((a, b) => $"{a} {b}");

	private static double CalculateRegistrationProgressPercentage(
		StudentRegistrationStateEnum registrationState = StudentRegistrationStateEnum.Default)
	{
		var totalStates = (double)Enum.GetValues<StudentRegistrationStateEnum>()
			.Count(s => s != StudentRegistrationStateEnum.Default);
		return (double)registrationState / totalStates * 100;
	}
}
