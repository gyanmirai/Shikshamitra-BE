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
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Application service for managing entrance exam operations
/// </summary>
[Authorize]
[RemoteService(false)]
public class StudentEnrollmentEntranceExamAppService : ShikshaMitraAppService, IStudentEnrollmentEntranceExamAppService
{
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<StudentAdditionalDetail, Guid> _studentAdditionalDetailRepository;
	private readonly IRepository<EntranceExamSlot, Guid> _entranceExamSlotRepository;
	private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;

	public StudentEnrollmentEntranceExamAppService(
		IRepository<Student, Guid> studentRepository,
		IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
		IRepository<EntranceExamSlot, Guid> entranceExamSlotRepository,
		IRepository<EducationMedium, Guid> educationMediumRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository)
	{
		_studentRepository = studentRepository;
		_studentAdditionalDetailRepository = studentAdditionalDetailRepository;
		_entranceExamSlotRepository = entranceExamSlotRepository;
		_educationMediumRepository = educationMediumRepository;
		_standardRepository = standardRepository;
		_educationBoardRepository = educationBoardRepository;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<PagedResultDto<EntranceExamStudentDto>> GetListAsync(GetEntranceExamStudentsInput input)
	{
		// Get queryable with necessary includes
		var studentQueryable = await _studentRepository.GetQueryableAsync();
		var additionalDetailQueryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		var mediumQueryable = await _educationMediumRepository.GetQueryableAsync();
		var standardQueryable = await _standardRepository.GetQueryableAsync();
		var boardQueryable = await _educationBoardRepository.GetQueryableAsync();

		// Build the query with joins
		var query = from student in studentQueryable
					join additionalDetail in additionalDetailQueryable
						on student.Id equals additionalDetail.Id
					join medium in mediumQueryable
						on student.AcademicMediumId equals medium.Id
					join standard in standardQueryable
						on student.StandardId equals standard.Id into standardGroup
					from standard in standardGroup.DefaultIfEmpty()
					join board in boardQueryable
						on medium.EducationBoardId equals board.Id
					where student.TenantId == CurrentTenant.Id &&
						  (student.Status == StudentLifeCycleStatusEnum.EntranceExam ||
						   student.Status == StudentLifeCycleStatusEnum.RegistrationFormIssued)
					select new { student, additionalDetail, medium, standard, board };

		// Apply filters
		if (input.StartDate.HasValue)
		{
			query = query.Where(x => x.additionalDetail.ExamDate.HasValue &&
									 x.additionalDetail.ExamDate.Value.Date >= input.StartDate.Value.Date);
		}

		if (input.EndDate.HasValue)
		{
			query = query.Where(x => x.additionalDetail.ExamDate.HasValue &&
									 x.additionalDetail.ExamDate.Value.Date <= input.EndDate.Value.Date);
		}

		if (input.StandardId.HasValue)
		{
			query = query.Where(x => x.student.StandardId == input.StandardId.Value);
		}

		if (input.EducationMediumId.HasValue)
		{
			query = query.Where(x => x.student.AcademicMediumId == input.EducationMediumId.Value);
		}

		if (input.ExamStatus.HasValue)
		{
			query = query.Where(x => x.additionalDetail.ExamStatus == input.ExamStatus.Value);
		}

		if (!input.SearchTerm.IsNullOrWhiteSpace())
		{
			var searchTerm = input.SearchTerm.ToLower();
			query = query.Where(x =>
				(x.student.FirstName != null && x.student.FirstName.ToLower().Contains(searchTerm)) ||
				(x.student.MiddleName != null && x.student.MiddleName.ToLower().Contains(searchTerm)) ||
				(x.student.LastName != null && x.student.LastName.ToLower().Contains(searchTerm)));
		}

		// Get total count
		var totalCount = await AsyncExecuter.CountAsync(query);

		// Apply sorting
		if (!input.Sorting.IsNullOrWhiteSpace())
		{
			query = query.OrderBy(input.Sorting);
		}
		else
		{
			query = query.OrderBy(x => x.additionalDetail.ExamDate).ThenBy(x => x.student.FirstName);
		}

		// Apply paging
		query = query.Skip(input.SkipCount).Take(input.MaxResultCount);

		// Execute query and map to DTOs
		var results = await AsyncExecuter.ToListAsync(query);
		var items = results.Select(result =>
		{
			var dto = new EntranceExamStudentDto
			{
				StudentId = result.student.Id,
				StudentName = $"{result.student.FirstName} {result.student.MiddleName ?? ""} {result.student.LastName}".Trim(),
				EducationBoard = result.board.Type.ToString(),
				EducationMedium = result.medium.Name,
				Standard = result.standard?.Name ?? "",
				ExamDate = result.additionalDetail.ExamDate?.DateTime,
				ExamStartTime = result.additionalDetail.ExamStartTime,
				ExamEndTime = result.additionalDetail.ExamEndTime,
				ExamTime = result.additionalDetail.ExamStartTime.HasValue && result.additionalDetail.ExamEndTime.HasValue
					? $"{result.additionalDetail.ExamStartTime.Value:hh\\:mm} to {result.additionalDetail.ExamEndTime.Value:hh\\:mm}"
					: null,
				ExamRoomNo = result.additionalDetail.ExamRoomNo,
				TotalMarks = result.additionalDetail.ExamTotalMarks,
				PassingMarks = result.additionalDetail.ExamPassingMarks,
				ObtainedMarks = result.additionalDetail.ExamObtainedMarks,
				Percentage = result.additionalDetail.ExamPercentage,
				ExamStatus = result.additionalDetail.ExamStatus,
				ExamStatusDisplay = result.additionalDetail.ExamStatus?.ToString() ?? "Not Scheduled",
				EnrollmentState = result.student.Status,
				Progress = CalculateEnrollmentProgress(result.student.Status)
			};
			return dto;
		}).ToList();

		return new PagedResultDto<EntranceExamStudentDto>(totalCount, items);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.View)]
	public async Task<EntranceExamDetailDto> GetAsync(Guid studentId)
	{
		var student = await _studentRepository.GetAsync(studentId);
		var additionalDetail = await _studentAdditionalDetailRepository.GetAsync(studentId);
		var medium = await _educationMediumRepository.GetAsync(student.AcademicMediumId);
		var board = await _educationBoardRepository.GetAsync(medium.EducationBoardId);
		
		Standard standard = null;
		if (student.StandardId.HasValue)
		{
			standard = await _standardRepository.GetAsync(student.StandardId.Value);
		}

		var dto = new EntranceExamDetailDto
		{
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName ?? ""} {student.LastName}".Trim(),
			InquiryNo = additionalDetail.InquiryNo,
			RegistrationFormNo = additionalDetail.RegistrationFormNo,
			EducationBoard = board.Type.ToString(),
			EducationMedium = medium.Name,
			Standard = standard?.Name ?? "",
			ExamScheduled = additionalDetail.ExamDate.HasValue,
			EntranceExamSlotId = additionalDetail.EntranceExamSlotId,
			ExamDate = additionalDetail.ExamDate?.DateTime,
			ExamStartTime = additionalDetail.ExamStartTime,
			ExamEndTime = additionalDetail.ExamEndTime,
			ExamRoomNo = additionalDetail.ExamRoomNo,
			ResultUploaded = additionalDetail.ExamObtainedMarks.HasValue,
			TotalMarks = additionalDetail.ExamTotalMarks,
			PassingMarks = additionalDetail.ExamPassingMarks,
			ObtainedMarks = additionalDetail.ExamObtainedMarks,
			Percentage = additionalDetail.ExamPercentage,
			ExamStatus = additionalDetail.ExamStatus,
			ResultDate = additionalDetail.ExamResultDate?.DateTime,
			EnrollmentState = student.Status,
			CanMoveToAdmission = additionalDetail.ExamStatus == EntranceExamStatusEnum.Pass
		};

		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task ScheduleExamAsync(Guid studentId, ScheduleEntranceExamDto input)
	{
		// Get student and validate
		var student = await _studentRepository.GetAsync(studentId);

		if (student.Status != StudentLifeCycleStatusEnum.RegistrationFormIssued)
		{
			throw new BusinessException("SM:EntranceExam:001")
				.WithData("currentStatus", student.Status.ToString())
				.WithData("requiredStatus", StudentLifeCycleStatusEnum.RegistrationFormIssued.ToString());
		}

		// Get and validate slot
		var slot = await _entranceExamSlotRepository.GetAsync(input.EntranceExamSlotId);

		// Check if slot has available capacity
		var enrolledCount = await GetEnrolledStudentsCountInSlotAsync(input.EntranceExamSlotId);
		if (enrolledCount >= slot.StudentLimit)
		{
			throw new BusinessException("SM:EntranceExam:002")
				.WithData("slotId", input.EntranceExamSlotId)
				.WithData("capacity", slot.StudentLimit);
		}

		// Check if student is already scheduled
		var additionalDetail = await _studentAdditionalDetailRepository.GetAsync(studentId);
		if (additionalDetail.EntranceExamSlotId.HasValue)
		{
			throw new BusinessException("SM:EntranceExam:003")
				.WithData("existingSlotId", additionalDetail.EntranceExamSlotId.Value);
		}

		// Schedule the exam using domain method
		student.ScheduleEntranceExam(
			slot.Id,
			slot.StartDate,
			slot.EndDate,
			slot.RoomNo
		);

		await _studentRepository.UpdateAsync(student, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<EntranceExamResultResponseDto> UploadResultAsync(Guid studentId, UploadEntranceExamResultDto input)
	{
		// Get student and validate
		var student = await _studentRepository.GetAsync(studentId);
		var additionalDetail = await _studentAdditionalDetailRepository.GetAsync(studentId);

		if (student.Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new BusinessException("SM:EntranceExam:004")
				.WithData("currentStatus", student.Status.ToString())
				.WithData("requiredStatus", StudentLifeCycleStatusEnum.EntranceExam.ToString());
		}

		if (!additionalDetail.ExamDate.HasValue)
		{
			throw new BusinessException("SM:EntranceExam:005")
				.WithData("studentId", studentId);
		}

		// Additional validation
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

		// Record exam result using domain method
		additionalDetail.RecordExamResult(input.TotalMarks, input.PassingMarks, input.ObtainedMarks);

		await _studentAdditionalDetailRepository.UpdateAsync(additionalDetail, autoSave: true);

		// Return response
		return new EntranceExamResultResponseDto
		{
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName ?? ""} {student.LastName}".Trim(),
			TotalMarks = input.TotalMarks,
			PassingMarks = input.PassingMarks,
			ObtainedMarks = input.ObtainedMarks,
			Percentage = additionalDetail.ExamPercentage.Value,
			ExamStatus = additionalDetail.ExamStatus.Value,
			ResultDate = additionalDetail.ExamResultDate.Value.DateTime
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task MoveToAdmissionAsync(Guid studentId, Guid? academicYearId = null)
	{
		var student = await _studentRepository.GetAsync(studentId);

		if (student.Status != StudentLifeCycleStatusEnum.EntranceExam)
		{
			throw new BusinessException("SM:EntranceExam:008")
				.WithData("currentStatus", student.Status.ToString())
				.WithData("requiredStatus", StudentLifeCycleStatusEnum.EntranceExam.ToString());
		}

		var additionalDetail = await _studentAdditionalDetailRepository.GetAsync(studentId);
		if (additionalDetail.ExamStatus != EntranceExamStatusEnum.Pass)
		{
			throw new BusinessException("SM:EntranceExam:009")
				.WithData("currentExamStatus", additionalDetail.ExamStatus?.ToString() ?? "Not Set");
		}

		// Move to admission using domain method
		student.MoveToAdmission(academicYearId);

		await _studentRepository.UpdateAsync(student, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_EntranceExam.Add)]
	public async Task<BulkMoveResultDto> BulkMoveToAdmissionAsync(BulkMoveToAdmissionDto input)
	{
		var result = new BulkMoveResultDto
		{
			TotalRequested = input.StudentIds.Count,
			SuccessCount = 0,
			FailureCount = 0,
			SucceededStudents = new List<StudentSummaryDto>(),
			FailedStudents = new List<FailedStudentDto>()
		};

		foreach (var studentId in input.StudentIds)
		{
			try
			{
			var student = await _studentRepository.GetAsync(studentId);
			var studentName = $"{student.FirstName} {student.MiddleName ?? ""} {student.LastName}".Trim();

			// Validate and move
			await MoveToAdmissionAsync(studentId, input.AcademicYearId);

			result.SucceededStudents.Add(new StudentSummaryDto
				{
					StudentId = studentId,
					StudentName = studentName
				});
				result.SuccessCount++;
			}
			catch (Exception ex)
			{
				var student = await _studentRepository.FindAsync(studentId);
				var studentName = student != null
					? $"{student.FirstName} {student.MiddleName ?? ""} {student.LastName}".Trim()
					: "Unknown Student";

				result.FailedStudents.Add(new FailedStudentDto
				{
					StudentId = studentId,
					StudentName = studentName,
					Reason = ex.Message
				});
				result.FailureCount++;
			}
		}

		return result;
	}

	private async Task<int> GetEnrolledStudentsCountInSlotAsync(Guid slotId)
	{
		var queryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(
			queryable.Where(sd => sd.EntranceExamSlotId == slotId));
	}

	private decimal CalculateEnrollmentProgress(StudentLifeCycleStatusEnum status)
	{
		// Calculate progress based on enrollment stages
		// Assuming 7 stages total: New, Inquiry, RegistrationFormIssued, EntranceExam, Registration, Admission, Enrolled
		return status switch
		{
			StudentLifeCycleStatusEnum.New => 0m,
			StudentLifeCycleStatusEnum.Inquiry => 14.29m,
			StudentLifeCycleStatusEnum.RegistrationFormIssued => 28.57m,
			StudentLifeCycleStatusEnum.EntranceExam => 42.86m,
			StudentLifeCycleStatusEnum.Registation => 57.14m,
			StudentLifeCycleStatusEnum.Admission => 71.43m,
			StudentLifeCycleStatusEnum.Enrolled => 100m,
			_ => 0m
		};
	}
}
