using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Admissions;
using ShikshaMitra.Students.Enrollments.EntranceExams;
using ShikshaMitra.Students.StudentEnrollments.Managers;
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
	IRepository<EducationBoard, Guid> educationBoardRepository,
	EntranceExamManager entranceExamManager) : ShikshaMitraAppService, IStudentEnrollmentEntranceExamAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<PagedResultDto<EntranceExamStudentDto>> GetListAsync(GetEntranceExamStudentsInput input)
	{
		var studentQueryable = await studentRepository.GetQueryableAsync();
		studentQueryable = studentQueryable.Where(x => x.AcademicMediumId == input.EducationMediumId && x.AdditionalDetail.EntranceExamStartDateTime >= input.StartDate && x.AdditionalDetail.EntranceExamStartDateTime <= input.EndDate && x.Status == StudentLifeCycleStatusEnum.EntranceExam);

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

		var students = await AsyncExecuter.ToListAsync(studentQueryable);

		var results = await AsyncExecuter.ToListAsync(studentQueryable.Select(x => new EntranceExamStudentDto
		{
			StudentId = x.Id,
			StudentName = x.FirstName + " " + x.MiddleName + " " + x.LastName,
			EducationBoard = x.AcademicBoard.Type.ToString(),
			EducationMedium = x.AcademicMedium.Name,
			Standard = x.Standard != null ? x.Standard.Name : string.Empty,
			ExamStartTime = x.AdditionalDetail.EntranceExamStartDateTime,
			ExamEndTime = x.AdditionalDetail.EntranceExamEndDateTime,
			ExamRoomNo = x.AdditionalDetail.EntranceExamRoomNo,
			TotalMarks = x.AdditionalDetail.EntranceExamTotalMarks,
			PassingMarks = x.AdditionalDetail.EntranceExamPassingMarks,
			ObtainedMarks = x.AdditionalDetail.EntranceExamObtainedMarks,
			Percentage = x.AdditionalDetail.EntranceExamPercentage,
			ExamStatus = x.AdditionalDetail.EntranceExamStatus,
			Progress = CalculateRegistrationProgressPercentage(x.AdditionalDetail.StudentRegistrationState),
			ExamStatusDisplay = x.AdditionalDetail.EntranceExamStatus != null ? x.AdditionalDetail.EntranceExamStatus.ToString() : "Not Scheduled",
			EnrollmentState = x.Status
		}));

		return new PagedResultDto<EntranceExamStudentDto>(totalCount, results);
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

		var slot = await entranceExamSlotRepository.GetAsync(input.EntranceExamSlotId);

		await entranceExamManager.ValidateScheduling(student.Id, slot.Id);

		student.ScheduleEntranceExam(slot.Id, slot.StartDate, slot.EndDate, slot.RoomNo);
		await studentRepository.UpdateAsync(student, autoSave: true);
	}

	public async Task<EntranceExamResultResponseDto> UploadResultAsync(Guid studentId, UploadEntranceExamResultDto input)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new EntityNotFoundException(typeof(Student), studentId);

		await entranceExamManager.ValidateResultUpload(student.Id, input.TotalMarks, input.PassingMarks, input.ObtainedMarks);

		student.AdditionalDetail.RecordExamResult(input.TotalMarks, input.PassingMarks, input.ObtainedMarks);
		await studentRepository.UpdateAsync(student, autoSave: true);

		return new EntranceExamResultResponseDto
		{
			StudentId = student.Id,
			StudentName = BuildStudentName(student.FirstName, student.MiddleName, student.LastName),
			TotalMarks = input.TotalMarks,
			PassingMarks = input.PassingMarks,
			ObtainedMarks = input.ObtainedMarks,
			Percentage = student.AdditionalDetail.EntranceExamPercentage!.Value,
			ExamStatus = student.AdditionalDetail.EntranceExamStatus!.Value,
			ResultDate = student.AdditionalDetail.EntranceExamResultDate!.Value.DateTime
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
			throw new UserFriendlyException("Student is not in a valid state to move to admission.");
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

		if (student.Status == StudentLifeCycleStatusEnum.Registration &&
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
