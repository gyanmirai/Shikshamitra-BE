using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Domain service for managing student submissions
/// </summary>
public class StudentSubmissionManager : DomainService
{
	private readonly IStudentSubmissionRepository _submissionRepository;

	public StudentSubmissionManager(IStudentSubmissionRepository submissionRepository)
	{
		_submissionRepository = submissionRepository;
	}

	/// <summary>
	/// Creates a new submission for a student if it doesn't exist (Homework)
	/// </summary>
	public async Task<StudentSubmissionEntity> CreateOrGetSubmissionAsync(
		Guid homeworkId,
		Guid studentId,
		Guid divisionId,
		DateTime dueDate,
		Guid? tenantId = null
	)
	{
		// Check if submission already exists
		var existingSubmission = await _submissionRepository
			.GetSubmissionByStudentAndHomeworkAsync(studentId, homeworkId);

		if (existingSubmission != null)
		{
			return existingSubmission;
		}

		// Create new submission
		var submission = new StudentSubmissionEntity(
			GuidGenerator.Create(),
			homeworkId,
			studentId,
			divisionId,
			dueDate,
			tenantId
		);

		return await _submissionRepository.InsertAsync(submission);
	}

	/// <summary>
	/// Creates a new submission for a student if it doesn't exist (Assignment)
	/// </summary>
	public async Task<StudentSubmissionEntity> CreateOrGetSubmissionForAssignmentAsync(
		Guid assignmentId,
		Guid studentId,
		Guid divisionId,
		DateTime dueDate,
		Guid? tenantId = null
	)
	{
		// Check if submission already exists
		var existingSubmission = await _submissionRepository
			.GetSubmissionByStudentAndAssignmentAsync(studentId, assignmentId);

		if (existingSubmission != null)
		{
			return existingSubmission;
		}

		// Create new submission for assignment
		var submission = new StudentSubmissionEntity(
			GuidGenerator.Create(),
			assignmentId,
			studentId,
			divisionId,
			dueDate,
			tenantId,
			isAssignment: true
		);

		return await _submissionRepository.InsertAsync(submission);
	}

	/// <summary>
	/// Validates feedback data
	/// </summary>
	public void ValidateFeedback(decimal? marks, decimal? totalMarks)
	{
		if (marks.HasValue && totalMarks.HasValue && marks.Value > totalMarks.Value)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidMarks")
				.WithData("Marks", marks)
				.WithData("TotalMarks", totalMarks)
				.WithData("message", $"Marks ({marks}) cannot exceed total marks ({totalMarks}).");
		}

		if (marks.HasValue && marks.Value < 0)
		{
			throw new BusinessException("ShikshaMitra.Homework:NegativeMarks")
				.WithData("message", "Marks cannot be negative.");
		}

		if (totalMarks.HasValue && totalMarks.Value < 0)
		{
			throw new BusinessException("ShikshaMitra.Homework:NegativeTotalMarks")
				.WithData("message", "Total marks cannot be negative.");
		}
	}
}
