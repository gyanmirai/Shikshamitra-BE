using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for homework feedback and assessment
/// </summary>
public interface IHomeworkFeedbackAppService : IApplicationService
{
	/// <summary>
	/// Gets all submissions for a homework filtered by class, division, and subject
	/// Shows all students in the division, even if they haven't submitted
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="filter">Filter parameters</param>
	/// <returns>List of submissions with student details</returns>
	Task<HomeworkSubmissionsResponseDto> GetSubmissionsAsync(
		Guid academicMediumId,
		GetHomeworkSubmissionsFilterDto filter
	);

	/// <summary>
	/// Gives or updates feedback for a student's homework submission
	/// </summary>
	/// <param name="homeworkId">Homework ID</param>
	/// <param name="studentId">Student ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Feedback data</param>
	/// <returns>Feedback response with updated data</returns>
	Task<FeedbackResponseDto> GiveFeedbackAsync(
		Guid homeworkId,
		Guid studentId,
		Guid academicMediumId,
		GiveFeedbackDto input
	);

	/// <summary>
	/// Updates only the submission status
	/// </summary>
	/// <param name="homeworkId">Homework ID</param>
	/// <param name="studentId">Student ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Status data</param>
	/// <returns>Message response</returns>
	Task<MessageResponseDto> UpdateStatusAsync(
		Guid homeworkId,
		Guid studentId,
		Guid academicMediumId,
		UpdateSubmissionStatusDto input
	);
}
