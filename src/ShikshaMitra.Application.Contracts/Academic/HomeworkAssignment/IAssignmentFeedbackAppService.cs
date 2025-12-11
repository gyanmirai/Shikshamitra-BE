using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service interface for assignment feedback and assessment
/// </summary>
public interface IAssignmentFeedbackAppService : IApplicationService
{
	/// <summary>
	/// Gets all submissions for an assignment filtered by class, division, and subject
	/// Shows all students in the division, even if they haven't submitted
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="filter">Filter parameters</param>
	/// <returns>List of submissions with student details</returns>
	Task<AssignmentSubmissionsResponseDto> GetSubmissionsAsync(
		Guid academicMediumId,
		GetAssignmentSubmissionsFilterDto filter
	);

	/// <summary>
	/// Gives or updates feedback for a student's assignment submission
	/// </summary>
	/// <param name="assignmentId">Assignment ID</param>
	/// <param name="studentId">Student ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Feedback data</param>
	/// <returns>Feedback response with updated data</returns>
	Task<FeedbackResponseDto> GiveFeedbackAsync(
		Guid assignmentId,
		Guid studentId,
		Guid academicMediumId,
		GiveFeedbackDto input
	);

	/// <summary>
	/// Updates only the submission status
	/// </summary>
	/// <param name="assignmentId">Assignment ID</param>
	/// <param name="studentId">Student ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Status data</param>
	/// <returns>Message response</returns>
	Task<MessageResponseDto> UpdateStatusAsync(
		Guid assignmentId,
		Guid studentId,
		Guid academicMediumId,
		UpdateSubmissionStatusDto input
	);
}
