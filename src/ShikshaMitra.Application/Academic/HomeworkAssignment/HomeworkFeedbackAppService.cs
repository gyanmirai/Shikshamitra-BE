using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service for homework feedback and assessment
/// </summary>
[Authorize]
public class HomeworkFeedbackAppService : ApplicationService, IHomeworkFeedbackAppService
{
	private readonly IRepository<HomeworkAssignmentEntity, Guid> _homeworkRepository;
	private readonly IStudentSubmissionRepository _submissionRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<HomeworkAssignmentType, Guid> _homeworkTypeRepository;
	private readonly StudentSubmissionManager _submissionManager;
	private readonly IAsyncQueryableExecuter _asyncExecuter;

	public HomeworkFeedbackAppService(
		IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
		IStudentSubmissionRepository submissionRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Subject, Guid> subjectRepository,
		IRepository<HomeworkAssignmentType, Guid> homeworkTypeRepository,
		StudentSubmissionManager submissionManager,
		IAsyncQueryableExecuter asyncExecuter
	)
	{
		_homeworkRepository = homeworkRepository;
		_submissionRepository = submissionRepository;
		_studentRepository = studentRepository;
		_standardRepository = standardRepository;
		_subjectRepository = subjectRepository;
		_homeworkTypeRepository = homeworkTypeRepository;
		_submissionManager = submissionManager;
		_asyncExecuter = asyncExecuter;
	}

	/// <summary>
	/// Gets all submissions for a homework - shows ALL students in division
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
	public async Task<HomeworkSubmissionsResponseDto> GetSubmissionsAsync(
		Guid academicMediumId,
		GetHomeworkSubmissionsFilterDto filter
	)
	{
		// 1. Validate homework exists and belongs to medium
		var homework = await _homeworkRepository
			.FirstOrDefaultAsync(h =>
				h.Id == filter.HomeworkId && h.EducationMediumId == academicMediumId
			);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", filter.HomeworkId);
		}

		// 2. Validate homework is assigned to the requested division
		// V1 homework must have DivisionId
		if (!homework.DivisionId.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidHomework")
				.WithData("Message", "Homework does not have a division assigned");
		}

		if (homework.DivisionId.Value != filter.DivisionId)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotAssignedToDivision")
				.WithData("DivisionId", filter.DivisionId);
		}

		// 3. Get ALL students in the division (approved students only)
		var studentsQuery = await _studentRepository.GetQueryableAsync();
		var students = await _asyncExecuter.ToListAsync(
			studentsQuery.Where(s =>
				s.DivisionId == filter.DivisionId
				&& s.StandardId == filter.ClassId
				&& s.AcademicMediumId == academicMediumId
				&& s.Status == StudentLifeCycleStatusEnum.Admission
				&& s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved
				&& !s.IsDeleted
			)
		);

		// 4. Get existing submissions for this homework and division
		var submissions = await _submissionRepository.GetSubmissionsWithDetailsAsync(
			filter.HomeworkId,
			filter.DivisionId
		);

		// Create a dictionary for quick lookup
		var submissionDict = submissions.ToDictionary(s => s.StudentId);

		// 5. Get homework type name
		var homeworkType = homework.HomeworkAssignmentTypeId.HasValue
			? await _homeworkTypeRepository.FindAsync(homework.HomeworkAssignmentTypeId.Value)
			: null;
		var homeworkTypeName = homeworkType?.Name ?? "Homework";

		// 6. Build response - Left join students with submissions
		var submissionDtos = new List<HomeworkSubmissionDto>();

		foreach (var student in students)
		{
			var submission = submissionDict.ContainsKey(student.Id)
				? submissionDict[student.Id]
				: null;

			var dto = new HomeworkSubmissionDto
			{
				Id = submission?.Id.ToString() ?? Guid.Empty.ToString(),
				RollNumber = student.RollNo?.ToString() ?? string.Empty,
				GrNumber = student.GrNo?.ToString() ?? string.Empty,
				StudentName =
					$"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
				Homework = homework.Title,
				HomeworkType = homeworkTypeName,
				SubmissionType = homework.SubmissionType.ToString(),
				SubmissionDate = submission?.SubmissionDate?.ToString("dd-MM-yyyy"),
				DueDate = homework.DueDate?.ToString("dd-MM-yyyy") ?? string.Empty,
				Marks = submission?.Marks,
				TotalMarks = submission?.TotalMarks,
				Feedback = submission?.Feedback,
				Remarks = submission?.Remarks,
				Status = (int)(submission?.Status ?? SubmissionStatus.NeedsReview),
				SubmittedContent = submission?.SubmittedContent
			};

			submissionDtos.Add(dto);
		}

		// 7. Sort by roll number
		submissionDtos = submissionDtos.OrderBy(s =>
		{
			if (int.TryParse(s.RollNumber, out var rollNo))
				return rollNo;
			return int.MaxValue;
		}).ToList();

		// 8. Build and return response
		return new HomeworkSubmissionsResponseDto
		{
			HomeworkId = homework.Id.ToString(),
			HomeworkTitle = homework.Title,
			ClassId = filter.ClassId.ToString(),
			DivisionId = filter.DivisionId.ToString(),
			SubjectId = filter.SubjectId.ToString(),
			SubmissionType = (int)homework.SubmissionType,
			Data = submissionDtos
		};
	}

	/// <summary>
	/// Gives or updates feedback for a student's homework
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
	public async Task<FeedbackResponseDto> GiveFeedbackAsync(
		Guid homeworkId,
		Guid studentId,
		Guid academicMediumId,
		GiveFeedbackDto input
	)
	{
		// 1. Validate homework
		var homework = await _homeworkRepository.FirstOrDefaultAsync(h =>
			h.Id == homeworkId && h.EducationMediumId == academicMediumId
		);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", homeworkId);
		}

		// 2. Validate student exists
		var student = await _studentRepository.GetAsync(studentId);

		// 3. Validate feedback data
		_submissionManager.ValidateFeedback(input.Marks, input.TotalMarks);

		// V1 homework must have DivisionId and DueDate
		if (!homework.DivisionId.HasValue || !homework.DueDate.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidHomework")
				.WithData("Message", "Homework does not have division or due date assigned");
		}

		// 4. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndHomeworkAsync(
			studentId,
			homeworkId
		);

		if (submission == null)
		{
			// Create new submission
			submission = await _submissionManager.CreateOrGetSubmissionAsync(
				homeworkId,
				studentId,
				homework.DivisionId.Value,
				homework.DueDate.Value,
				CurrentTenant.Id
			);
		}

		// 5. Update feedback
		submission.UpdateFeedback(
			input.Marks,
			input.TotalMarks,
			input.Feedback,
			input.Remarks,
			input.Status,
			CurrentUser.Id!.Value
		);

		// 6. Save changes
		await _submissionRepository.UpdateAsync(submission, autoSave: true);

		// 7. Build and return response
		return new FeedbackResponseDto
		{
			Message = L["ShikshaMitra.Homework:FeedbackUpdatedSuccessfully"],
			Data = new FeedbackDataDto
			{
				StudentId = studentId.ToString(),
				HomeworkId = homeworkId.ToString(),
				Marks = input.Marks,
				TotalMarks = input.TotalMarks,
				Feedback = input.Feedback,
				Remarks = input.Remarks,
				Status = input.Status.ToString()
			}
		};
	}

	/// <summary>
	/// Updates submission status only
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
	public async Task<MessageResponseDto> UpdateStatusAsync(
		Guid homeworkId,
		Guid studentId,
		Guid academicMediumId,
		UpdateSubmissionStatusDto input
	)
	{
		// 1. Validate homework
		var homework = await _homeworkRepository.FirstOrDefaultAsync(h =>
			h.Id == homeworkId && h.EducationMediumId == academicMediumId
		);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", homeworkId);
		}

		// V1 homework must have DivisionId and DueDate
		if (!homework.DivisionId.HasValue || !homework.DueDate.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidHomework")
				.WithData("Message", "Homework does not have division or due date assigned");
		}

		// 2. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndHomeworkAsync(
			studentId,
			homeworkId
		);

		if (submission == null)
		{
			// Create new submission
			submission = await _submissionManager.CreateOrGetSubmissionAsync(
				homeworkId,
				studentId,
				homework.DivisionId.Value,
				homework.DueDate.Value,
				CurrentTenant.Id
			);
		}

		// 3. Update status
		submission.UpdateStatus(input.Status);

		// 4. Save changes
		await _submissionRepository.UpdateAsync(submission, autoSave: true);

		// 5. Return response
		return new MessageResponseDto
		{
			Message = L["ShikshaMitra.Homework:StatusUpdatedSuccessfully"]
		};
	}
}
