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
	private readonly StudentSubmissionManager _submissionManager;
	private readonly IAsyncQueryableExecuter _asyncExecuter;

	public HomeworkFeedbackAppService(
		IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
		IStudentSubmissionRepository submissionRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Subject, Guid> subjectRepository,
		StudentSubmissionManager submissionManager,
		IAsyncQueryableExecuter asyncExecuter
	)
	{
		_homeworkRepository = homeworkRepository;
		_submissionRepository = submissionRepository;
		_studentRepository = studentRepository;
		_standardRepository = standardRepository;
		_subjectRepository = subjectRepository;
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
		var homeworkQuery = await _homeworkRepository.WithDetailsAsync(
			h => h.DivisionSchedules,
			h => h.HomeworkType,
			h => h.HomeworkAssignmentType
		);
		var homework = await _asyncExecuter.FirstOrDefaultAsync(
			homeworkQuery.Where(h => h.Id == filter.HomeworkId && h.EducationMediumId == academicMediumId)
		);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", filter.HomeworkId);
		}

		// 2. Validate homework is assigned to the requested division
		// Support both V1 (direct DivisionId) and V2 (DivisionSchedules collection)
		var isAssignedToDivision = (homework.DivisionId.HasValue && homework.DivisionId.Value == filter.DivisionId)
			|| homework.DivisionSchedules.Any(ds => ds.DivisionId == filter.DivisionId);

		if (!homework.DivisionId.HasValue && !homework.DivisionSchedules.Any())
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidHomework")
				.WithData("Message", "Homework does not have a division assigned");
		}

		if (!isAssignedToDivision)
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
				&& s.Status == StudentLifeCycleStatusEnum.Enrolled
				&& s.AdmissionApprovalStatus == StudentEnrollmentApprovalState.Approved
				&& !s.IsDeleted
			)
		);

		// 4. Get existing submissions for this homework and division
		var submissions = await _submissionRepository.GetSubmissionsWithDetailsAsync(
			filter.HomeworkId,
			filter.DivisionId
		);

		// Use the latest row if legacy data already contains duplicate submissions.
		var submissionDict = submissions
			.GroupBy(s => s.StudentId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(s => s.SubmissionDate ?? DateTimeOffset.MinValue)
					.ThenByDescending(s => s.CreationTime)
					.First());

		// 5. Resolve homework type name: prefer new HomeworkType, fall back to legacy HomeworkAssignmentType
		var homeworkTypeName = homework.HomeworkType?.Name
			?? homework.HomeworkAssignmentType?.Name
			?? "Homework";

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
				StudentId = student.Id,
				RollNumber = student.RollNo?.ToString() ?? string.Empty,
				GrNumber = student.GrNo?.ToString() ?? string.Empty,
				StudentName =
					$"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
				Homework = homework.Title,
				HomeworkType = homeworkTypeName,
				SubmissionType = homework.SubmissionType.ToString(),
				SubmissionDate = submission?.SubmissionDate?.ToString("dd-MM-yyyy"),
				DueDate = homework.DueDate?.ToString("dd-MM-yyyy")
					?? homework.DivisionSchedules.FirstOrDefault(ds => ds.DivisionId == filter.DivisionId)?.DueDate.ToString("dd-MM-yyyy")
					?? string.Empty,
				Marks = submission?.Marks,
				TotalMarks = submission?.TotalMarks,
				Feedback = submission?.Feedback,
				Remarks = submission?.Remarks,
				Status = (int) (submission?.Status ?? SubmissionStatus.NotSubmitted),
				SubmissionStatusLabel = GetSubmissionStatusLabel(submission?.Status ?? SubmissionStatus.NotSubmitted),
				EvaluationStatus = GetEvaluationStatus(submission?.Status, submission?.Marks, submission?.Feedback),
				FeedbackStatus = string.IsNullOrWhiteSpace(submission?.Feedback) ? "Feedback Pending" : "Feedback Added",
				Percentage = CalculatePercentage(submission?.Marks, submission?.TotalMarks),
				SubmittedContent = submission?.SubmittedContent,
				SubmittedDocumentUrl = submission?.SubmittedDocumentUrl,
				OriginalFileName = submission?.OriginalFileName
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
			SubmissionType = (int) homework.SubmissionType,
			Summary = BuildSummary(submissionDtos),
			Data = submissionDtos
		};
	}

	private static EvaluationWorkspaceSummaryDto BuildSummary(List<HomeworkSubmissionDto> submissions)
	{
		var reviewedRows = submissions.Where(s => s.Marks.HasValue && s.TotalMarks.HasValue).ToList();

		return new EvaluationWorkspaceSummaryDto
		{
			TotalStudents = submissions.Count,
			Submitted = submissions.Count(s => s.Status is (int)SubmissionStatus.Submitted or (int)SubmissionStatus.Late or (int)SubmissionStatus.Reviewed or (int)SubmissionStatus.NeedsReview),
			NotSubmitted = submissions.Count(s => s.Status == (int)SubmissionStatus.NotSubmitted),
			Reviewed = submissions.Count(s => s.Status == (int)SubmissionStatus.Reviewed),
			NeedsReview = submissions.Count(s => s.Status is (int)SubmissionStatus.Submitted or (int)SubmissionStatus.Late or (int)SubmissionStatus.NeedsReview),
			Late = submissions.Count(s => s.Status == (int)SubmissionStatus.Late),
			TotalMarks = reviewedRows.Select(s => s.TotalMarks).FirstOrDefault(x => x.HasValue),
			AverageMarks = reviewedRows.Count == 0 ? null : Math.Round(reviewedRows.Average(s => s.Marks!.Value), 2)
		};
	}

	private static string GetEvaluationStatus(SubmissionStatus? status, decimal? marks, string? feedback)
	{
		if (status == SubmissionStatus.Reviewed)
		{
			return "Reviewed";
		}

		if (marks.HasValue || !string.IsNullOrWhiteSpace(feedback))
		{
			return "Draft";
		}

		return status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview
			? "Needs Review"
			: "Not Started";
	}

	private static string GetSubmissionStatusLabel(SubmissionStatus status)
	{
		return status switch
		{
			SubmissionStatus.NotSubmitted => "Not Submitted",
			SubmissionStatus.NeedsReview => "Needs Review",
			_ => status.ToString()
		};
	}

	private static decimal? CalculatePercentage(decimal? marks, decimal? totalMarks)
	{
		return marks.HasValue && totalMarks.HasValue && totalMarks.Value > 0
			? Math.Round(marks.Value / totalMarks.Value * 100, 2)
			: null;
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
		// 1. Validate homework (load with DivisionSchedules to support V2 homeworks)
		var homeworkQuery = await _homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
		var homework = await _asyncExecuter.FirstOrDefaultAsync(
			homeworkQuery.Where(h => h.Id == homeworkId && h.EducationMediumId == academicMediumId)
		);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", homeworkId);
		}
		EnsureHomeworkOpenForEvaluation(homework);

		// 2. Validate student exists
		var student = await _studentRepository.GetAsync(studentId);

		// 3. Validate feedback data
		_submissionManager.ValidateFeedback(input.Marks, input.TotalMarks);
		EnsureFeedbackStatusAllowed(input);

		// 4. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndHomeworkAsync(
			studentId,
			homeworkId
		);

		if (submission == null)
		{
			throw new BusinessException("ShikshaMitra.Submission:CannotEvaluateMissingSubmission")
				.WithData("StudentId", studentId)
				.WithData("HomeworkId", homeworkId);
		}

		EnsureSubmissionCanBeEvaluated(submission);

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
		// 1. Validate homework (load with DivisionSchedules to support V2 homeworks)
		var homeworkQuery = await _homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
		var homework = await _asyncExecuter.FirstOrDefaultAsync(
			homeworkQuery.Where(h => h.Id == homeworkId && h.EducationMediumId == academicMediumId)
		);

		if (homework == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotFound")
				.WithData("HomeworkId", homeworkId);
		}
		EnsureHomeworkOpenForEvaluation(homework);

		// 2. Get student to resolve division for V2 homeworks
		var student = await _studentRepository.GetAsync(studentId);

		// 3. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndHomeworkAsync(
			studentId,
			homeworkId
		);

		if (submission == null)
		{
			throw new BusinessException("ShikshaMitra.Submission:CannotUpdateMissingSubmission")
				.WithData("StudentId", studentId)
				.WithData("HomeworkId", homeworkId);
		}

		EnsureStatusTransitionAllowed(submission, input.Status);

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

	private static void EnsureHomeworkOpenForEvaluation(HomeworkAssignmentEntity homework)
	{
		var published = homework.Status == HomeworkStatus.Published || homework.IsPublished || homework.HasPublishedDivisions;
		if (!published)
		{
			throw new BusinessException("ShikshaMitra.Homework:NotPublished");
		}

		if (homework.Status == HomeworkStatus.Closed)
		{
			throw new BusinessException("ShikshaMitra.Homework:Closed");
		}
	}

	private static void EnsureFeedbackStatusAllowed(GiveFeedbackDto input)
	{
		if (input.Status == SubmissionStatus.Reviewed &&
			(!input.Marks.HasValue || !input.TotalMarks.HasValue || string.IsNullOrWhiteSpace(input.Feedback)))
		{
			throw new BusinessException("ShikshaMitra.Submission:ReviewRequiresMarksAndFeedback");
		}

		if (input.Status == SubmissionStatus.NeedsReview && string.IsNullOrWhiteSpace(input.Remarks))
		{
			throw new BusinessException("ShikshaMitra.Submission:ReviewChangeReasonRequired");
		}

		if (input.Status == SubmissionStatus.Excused && string.IsNullOrWhiteSpace(input.Remarks))
		{
			throw new BusinessException("ShikshaMitra.Submission:ExcuseReasonRequired");
		}
	}

	private static void EnsureSubmissionCanBeEvaluated(StudentSubmissionEntity submission)
	{
		if (submission.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft)
		{
			throw new BusinessException("ShikshaMitra.Submission:NotSubmittedYet")
				.WithData("SubmissionId", submission.Id);
		}
	}

	private static void EnsureStatusTransitionAllowed(StudentSubmissionEntity submission, SubmissionStatus requestedStatus)
	{
		if (submission.Status == SubmissionStatus.Reviewed && requestedStatus != SubmissionStatus.Reviewed)
		{
			throw new BusinessException("ShikshaMitra.Submission:ReviewedSubmissionLocked")
				.WithData("SubmissionId", submission.Id);
		}

		if (requestedStatus == SubmissionStatus.Reviewed)
		{
			throw new BusinessException("ShikshaMitra.Submission:UseFeedbackToReview");
		}

		if (requestedStatus == SubmissionStatus.NotSubmitted && submission.SubmissionDate.HasValue)
		{
			throw new BusinessException("ShikshaMitra.Submission:CannotMarkSubmittedWorkNotSubmitted")
				.WithData("SubmissionId", submission.Id);
		}
	}
}
