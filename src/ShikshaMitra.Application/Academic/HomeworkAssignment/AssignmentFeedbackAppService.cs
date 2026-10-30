using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
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
		private readonly IUserScopeService _userScopeService;
		private readonly IAsyncQueryableExecuter _asyncExecuter;


	public AssignmentFeedbackAppService(
		IRepository<AssignmentEntity, Guid> assignmentRepository,
		IStudentSubmissionRepository submissionRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<Subject, Guid> subjectRepository,
			IAssignmentTypeRepository assignmentTypeRepository,
			StudentSubmissionManager submissionManager,
			IUserScopeService userScopeService,
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
			_userScopeService = userScopeService;
			_asyncExecuter = asyncExecuter;

	}

	/// <summary>
	/// Gets all submissions for an assignment - shows ALL students in division
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View)]
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

			EnsureEvaluationRequired(assignment);
			await EnsureCurrentUserCanAccessDivisionAsync(assignment, filter.DivisionId, nameof(GetSubmissionsAsync));
			// 2. Validate requested division is in assignment.DivisionSchedules

		var divisionSchedule = assignment.DivisionSchedules
			.FirstOrDefault(d => d.DivisionId == filter.DivisionId);

		if (divisionSchedule == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotAssignedToDivision")
				.WithData("DivisionId", filter.DivisionId);
		}

		// Current onboarding activates enrollment without setting the legacy admission approval flag.
		var studentsQuery = await _studentRepository.GetQueryableAsync();
		var students = await _asyncExecuter.ToListAsync(
			studentsQuery.Where(s =>
				s.DivisionId == filter.DivisionId
				&& s.StandardId == filter.ClassId
				&& s.AcademicMediumId == academicMediumId
				&& s.Status == StudentLifeCycleStatusEnum.Enrolled
				&& !s.IsDeleted
			).OrderBy(student => student.Id).Select(student => new
			{
				student.Id, student.RollNo, student.GrNo, student.GrNumber, student.FirstName, student.MiddleName, student.LastName
			})
		);

		// 4. Get existing submissions for this assignment and division
		var submissions = await _submissionRepository.GetSubmissionsByAssignmentAndDivisionAsync(
			filter.AssignmentId,
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
				StudentId = student.Id,
				RollNumber = student.RollNo?.ToString() ?? string.Empty,
				GrNumber = student.GrNumber ?? student.GrNo?.ToString() ?? string.Empty,
				StudentName =
					$"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
				Homework = assignment.Title,
				HomeworkType = assignmentTypeName,
				SubmissionType = (int) assignment.SubmissionType,
				SubmissionDate = submission?.SubmissionDate?.ToString("dd-MM-yyyy"),
				DueDate = divisionSchedule.DueDate.ToString("dd-MM-yyyy"), // Division-specific due date
				Marks = submission?.Marks,
				TotalMarks = submission?.TotalMarks ?? assignment.TotalMarks,
				Feedback = submission?.Feedback,
				Remarks = submission?.Remarks,
					Status = (int) (submission?.Status ??
						(assignment.SubmissionType == SubmissionType.NoSubmissionRequired
							? SubmissionStatus.Submitted
							: SubmissionStatus.NotSubmitted)),
					SubmissionStatusLabel = assignment.SubmissionType == SubmissionType.NoSubmissionRequired && submission == null
						? "No Submission Required"
						: GetSubmissionStatusLabel(submission?.Status ?? SubmissionStatus.NotSubmitted),

				EvaluationStatus = GetEvaluationStatus(submission?.Status, submission?.Marks, submission?.Feedback),
				FeedbackStatus = string.IsNullOrWhiteSpace(submission?.Feedback) ? "Feedback Pending" : "Feedback Added",
				Percentage = CalculatePercentage(submission?.Marks, submission?.TotalMarks ?? assignment.TotalMarks),
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
		return new AssignmentSubmissionsResponseDto
		{
			AssignmentId = assignment.Id.ToString(),
			AssignmentTitle = assignment.Title,
			ClassId = filter.ClassId.ToString(),
			DivisionId = filter.DivisionId.ToString(),
			SubjectId = filter.SubjectId.ToString(),
			DueDate = divisionSchedule.DueDate.ToString("dd-MM-yyyy"),
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

		if (status == SubmissionStatus.NeedsReview)
		{
			return "Needs Review";
		}

		if (marks.HasValue || !string.IsNullOrWhiteSpace(feedback))
		{
			return "Reviewed";
		}

		return status is SubmissionStatus.Submitted or SubmissionStatus.Late
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
	/// Gives or updates feedback for a student's assignment
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
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
		EnsureAssignmentOpenForEvaluation(assignment);

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
			await EnsureCurrentUserCanAccessDivisionAsync(assignment, divisionSchedule.DivisionId, nameof(GiveFeedbackAsync));

			// 4. Validate feedback data
			var evaluation = AssignmentEvaluationResolver.Resolve(assignment, assignment.AssignmentType);
			EnsureFeedbackMatchesEvaluation(input, evaluation);
			_submissionManager.ValidateFeedback(input.Marks, input.TotalMarks);
			EnsureFeedbackStatusAllowed(input, evaluation);


		// 5. Get or create submission
		var submission = await _submissionRepository.GetSubmissionByStudentAndAssignmentAsync(
			studentId,
			assignmentId
		);

			if (submission == null)
			{
				if (assignment.SubmissionType is not (SubmissionType.InPersonSubmission or SubmissionType.NoSubmissionRequired))

			{
				throw new BusinessException("ShikshaMitra.Submission:CannotEvaluateMissingSubmission")
					.WithData("StudentId", studentId)
					.WithData("AssignmentId", assignmentId);
			}

			submission = await _submissionManager.CreateOrGetSubmissionForAssignmentAsync(
				assignmentId,
				studentId,
				divisionSchedule.DivisionId,
				divisionSchedule.DueDate,
				CurrentTenant.Id
			);
		}

		EnsureSubmissionCanBeEvaluated(submission);

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
				SubmissionId = submission.Id.ToString(),
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
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
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
		EnsureAssignmentOpenForEvaluation(assignment);

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
			await EnsureCurrentUserCanAccessDivisionAsync(assignment, divisionSchedule.DivisionId, nameof(UpdateStatusAsync));

			// 4. Get or create submission

		var submission = await _submissionRepository.GetSubmissionByStudentAndAssignmentAsync(
			studentId,
			assignmentId
		);

		if (submission == null)
		{
			throw new BusinessException("ShikshaMitra.Submission:CannotUpdateMissingSubmission")
				.WithData("StudentId", studentId)
				.WithData("AssignmentId", assignmentId);
		}

		EnsureStatusTransitionAllowed(submission, input.Status);

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

		private static void EnsureEvaluationRequired(AssignmentEntity assignment)
		{
			if (!AssignmentEvaluationResolver.Resolve(assignment, assignment.AssignmentType).IsRequired)
			{
				throw new BusinessException("ShikshaMitra.Assignment:EvaluationNotRequired")
					.WithData("AssignmentId", assignment.Id);
			}
		}

		private async Task EnsureCurrentUserCanAccessDivisionAsync(
			AssignmentEntity assignment,
			Guid divisionId,
			string operation)
		{
			if (!CurrentUser.Id.HasValue)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied);
			}

			var scope = await _userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
			if (!AssignmentScopePolicy.CanAccessDivision(scope, assignment, divisionId))
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied)
					.WithData("AssignmentId", assignment.Id)
					.WithData("DivisionId", divisionId)
					.WithData("Operation", operation);
			}
		}


	private static void EnsureAssignmentOpenForEvaluation(AssignmentEntity assignment)
	{
		EnsureEvaluationRequired(assignment);
		var published = assignment.Status == AssignmentStatus.Published || assignment.HasPublishedDivisions;
		if (!published)
		{
			throw new BusinessException("ShikshaMitra.Assignment:NotPublished");
		}

		if (assignment.Status == AssignmentStatus.Closed)
		{
			throw new BusinessException("ShikshaMitra.Assignment:Closed");
		}
	}

		private static void EnsureFeedbackMatchesEvaluation(
			GiveFeedbackDto input,
			EffectiveAssignmentEvaluation evaluation)
		{
			if (evaluation.Method != EvaluationMethod.Marks &&
				(input.Marks.HasValue || input.TotalMarks.HasValue))
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.IncompatibleEvaluationMarks)
					.WithData("EvaluationMethod", evaluation.Method);
			}
		}

		private static void EnsureFeedbackStatusAllowed(
			GiveFeedbackDto input,
			EffectiveAssignmentEvaluation evaluation)
		{
			if (input.Status == SubmissionStatus.Reviewed &&
				(string.IsNullOrWhiteSpace(input.Feedback) ||
				 (evaluation.Method == EvaluationMethod.Marks &&
				  (!input.Marks.HasValue || !input.TotalMarks.HasValue))))
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
