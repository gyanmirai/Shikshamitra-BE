using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
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
/// Application service for assignment feedback and assessment
/// </summary>
[Authorize]
public class AssignmentFeedbackAppService : ApplicationService, IAssignmentFeedbackAppService
{
	private readonly IRepository<AssignmentEntity, Guid> _assignmentRepository;
	private readonly IStudentSubmissionRepository _submissionRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IAssignmentTypeRepository _assignmentTypeRepository;
	private readonly StudentSubmissionManager _submissionManager;
	private readonly IAsyncQueryableExecuter _asyncExecuter;

	public AssignmentFeedbackAppService(
		IRepository<AssignmentEntity, Guid> assignmentRepository,
		IStudentSubmissionRepository submissionRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Subject, Guid> subjectRepository,
		IAssignmentTypeRepository assignmentTypeRepository,
		StudentSubmissionManager submissionManager,
		IAsyncQueryableExecuter asyncExecuter
	)
	{
		_assignmentRepository = assignmentRepository;
		_submissionRepository = submissionRepository;
		_studentRepository = studentRepository;
		_standardRepository = standardRepository;
		_subjectRepository = subjectRepository;
		_assignmentTypeRepository = assignmentTypeRepository;
		_submissionManager = submissionManager;
		_asyncExecuter = asyncExecuter;
	}

	/// <summary>
	/// Gets all submissions for an assignment - shows ALL students in division
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
	public async Task<AssignmentSubmissionsResponseDto> GetSubmissionsAsync(
		Guid academicMediumId,
		GetAssignmentSubmissionsFilterDto filter
	)
	{
		// 1. Get assignment with division schedules (eager loading)
		var queryable = await _assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules, a => a.AssignmentType);
		var assignment = await _asyncExecuter.FirstOrDefaultAsync(
			queryable.Where(a => a.Id == filter.AssignmentId && a.EducationMediumId == academicMediumId)
		);

		if (assignment == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound")
				.WithData("AssignmentId", filter.AssignmentId);
		}

		// 2. Validate requested division is in assignment.DivisionSchedules
		var divisionSchedule = assignment.DivisionSchedules
			.FirstOrDefault(d => d.DivisionId == filter.DivisionId);

		if (divisionSchedule == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotAssignedToDivision")
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

		// 4. Get existing submissions for this assignment and division
		var submissions = await _submissionRepository.GetSubmissionsByAssignmentAndDivisionAsync(
			filter.AssignmentId,
			filter.DivisionId
		);

		// Create a dictionary for quick lookup
		var submissionDict = submissions.ToDictionary(s => s.StudentId);

		// 5. Get assignment type name
		var assignmentTypeName = assignment.AssignmentType?.Name ?? "Assignment";

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
				Homework = assignment.Title,
				HomeworkType = assignmentTypeName,
				SubmissionType = assignment.SubmissionType.ToString(),
				SubmissionDate = submission?.SubmissionDate?.ToString("dd-MM-yyyy"),
				DueDate = divisionSchedule.DueDate.ToString("dd-MM-yyyy"), // Division-specific due date
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
		return new AssignmentSubmissionsResponseDto
		{
			AssignmentId = assignment.Id.ToString(),
			AssignmentTitle = assignment.Title,
			ClassId = filter.ClassId.ToString(),
			DivisionId = filter.DivisionId.ToString(),
			SubjectId = filter.SubjectId.ToString(),
			Data = submissionDtos
		};
	}

	/// <summary>
	/// Gives or updates feedback for a student's assignment
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
	public async Task<FeedbackResponseDto> GiveFeedbackAsync(
		Guid assignmentId,
		Guid studentId,
		Guid academicMediumId,
		GiveFeedbackDto input
	)
	{
		// 1. Get assignment with division schedules
		var queryable = await _assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
		var assignment = await _asyncExecuter.FirstOrDefaultAsync(
			queryable.Where(a => a.Id == assignmentId && a.EducationMediumId == academicMediumId)
		);

		if (assignment == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound")
				.WithData("AssignmentId", assignmentId);
		}

		// 2. Validate student exists and get their division
		var student = await _studentRepository.GetAsync(studentId);

		// 3. Get division schedule for student's division
		var divisionSchedule = assignment.DivisionSchedules
			.FirstOrDefault(d => d.DivisionId == student.DivisionId);

		if (divisionSchedule == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:StudentNotInAssignedDivision")
				.WithData("StudentId", studentId)
				.WithData("DivisionId", student.DivisionId);
		}

		// 4. Validate feedback data
		_submissionManager.ValidateFeedback(input.Marks, input.TotalMarks);

		// 5. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndAssignmentAsync(
			studentId,
			assignmentId
		);

		if (submission == null)
		{
			// Create new submission
			submission = await _submissionManager.CreateOrGetSubmissionForAssignmentAsync(
				assignmentId,
				studentId,
				student.DivisionId.Value,
				divisionSchedule.DueDate,
				CurrentTenant.Id
			);
		}

		// 6. Update feedback
		submission.UpdateFeedback(
			input.Marks,
			input.TotalMarks,
			input.Feedback,
			input.Remarks,
			input.Status,
			CurrentUser.Id!.Value
		);

		// 7. Save changes
		await _submissionRepository.UpdateAsync(submission, autoSave: true);

		// 8. Build and return response
		return new FeedbackResponseDto
		{
			Message = L["ShikshaMitra.Assignment:FeedbackUpdatedSuccessfully"],
			Data = new FeedbackDataDto
			{
				StudentId = studentId.ToString(),
				HomeworkId = assignmentId.ToString(),
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
		Guid assignmentId,
		Guid studentId,
		Guid academicMediumId,
		UpdateSubmissionStatusDto input
	)
	{
		// 1. Get assignment with division schedules
		var queryable = await _assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
		var assignment = await _asyncExecuter.FirstOrDefaultAsync(
			queryable.Where(a => a.Id == assignmentId && a.EducationMediumId == academicMediumId)
		);

		if (assignment == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotFound")
				.WithData("AssignmentId", assignmentId);
		}

		// 2. Validate student exists
		var student = await _studentRepository.GetAsync(studentId);

		// 3. Get division schedule for student's division
		var divisionSchedule = assignment.DivisionSchedules
			.FirstOrDefault(d => d.DivisionId == student.DivisionId);

		if (divisionSchedule == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:StudentNotInAssignedDivision")
				.WithData("StudentId", studentId);
		}

		// 4. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndAssignmentAsync(
			studentId,
			assignmentId
		);

		if (submission == null)
		{
			// Create new submission
			submission = await _submissionManager.CreateOrGetSubmissionForAssignmentAsync(
				assignmentId,
				studentId,
				student.DivisionId.Value,
				divisionSchedule.DueDate,
				CurrentTenant.Id
			);
		}

		// 5. Update status
		submission.UpdateStatus(input.Status);

		// 6. Save changes
		await _submissionRepository.UpdateAsync(submission, autoSave: true);

		// 7. Return response
		return new MessageResponseDto
		{
			Message = L["ShikshaMitra.Assignment:StatusUpdatedSuccessfully"]
		};
	}
}
