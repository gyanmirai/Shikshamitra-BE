using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

/// <summary>
/// API controller for assignment feedback and assessment
/// </summary>
[ApiController]
[Authorize]
[RemoteService(Name = "Academic")]
[Area("academic")]
[Route("api/assignments")]
public class AssignmentFeedbackController : AbpControllerBase
{
	private readonly IAssignmentFeedbackAppService _feedbackAppService;

	public AssignmentFeedbackController(IAssignmentFeedbackAppService feedbackAppService)
	{
		_feedbackAppService = feedbackAppService;
	}

	/// <summary>
	/// GET: api/assignments/submissions?academicMediumId=xxx&classId=xxx&divisionId=xxx&subjectId=xxx&assignmentId=xxx
	/// Gets all submissions for an assignment with filters
	/// Shows all students in the division, even if they haven't submitted
	/// </summary>
	[HttpGet("submissions")]
	public async Task<AssignmentSubmissionsResponseDto> GetSubmissions(
		[FromQuery] Guid academicMediumId,
		[FromQuery] GetAssignmentSubmissionsFilterDto filter
	)
	{
		return await _feedbackAppService.GetSubmissionsAsync(academicMediumId, filter);
	}

	/// <summary>
	/// PATCH: api/assignments/{assignmentId}/submissions/{studentId}/feedback?academicMediumId=xxx
	/// Gives or updates feedback for a student's assignment submission
	/// </summary>
	[HttpPatch("{assignmentId}/submissions/{studentId}/feedback")]
	public async Task<FeedbackResponseDto> GiveFeedback(
		Guid assignmentId,
		Guid studentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] GiveFeedbackDto input
	)
	{
		return await _feedbackAppService.GiveFeedbackAsync(
			assignmentId,
			studentId,
			academicMediumId,
			input
		);
	}

	/// <summary>
	/// PATCH: api/assignments/{assignmentId}/submissions/{studentId}/status?academicMediumId=xxx
	/// Updates only the submission status
	/// </summary>
	[HttpPatch("{assignmentId}/submissions/{studentId}/status")]
	public async Task<MessageResponseDto> UpdateStatus(
		Guid assignmentId,
		Guid studentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] UpdateSubmissionStatusDto input
	)
	{
		return await _feedbackAppService.UpdateStatusAsync(
			assignmentId,
			studentId,
			academicMediumId,
			input
		);
	}
}
