using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

/// <summary>
/// API controller for homework feedback and assessment
/// </summary>
[ApiController]
[Authorize]
[RemoteService(Name = "Academic")]
[Area("academic")]
[Route("api/homework")]
public class HomeworkFeedbackController : AbpControllerBase
{
	private readonly IHomeworkFeedbackAppService _feedbackAppService;

	public HomeworkFeedbackController(IHomeworkFeedbackAppService feedbackAppService)
	{
		_feedbackAppService = feedbackAppService;
	}

	/// <summary>
	/// GET: api/homework/submissions?academicMediumId=xxx&classId=xxx&divisionId=xxx&subjectId=xxx&homeworkId=xxx
	/// Gets all submissions for a homework with filters
	/// Shows all students in the division, even if they haven't submitted
	/// </summary>
	[HttpGet("submissions")]
	public async Task<HomeworkSubmissionsResponseDto> GetSubmissions(
		[FromQuery] Guid academicMediumId,
		[FromQuery] GetHomeworkSubmissionsFilterDto filter
	)
	{
		return await _feedbackAppService.GetSubmissionsAsync(academicMediumId, filter);
	}

	/// <summary>
	/// PATCH: api/homework/{homeworkId}/submissions/{studentId}/feedback?academicMediumId=xxx
	/// Gives or updates feedback for a student's homework submission
	/// </summary>
	[HttpPatch("{homeworkId}/submissions/{studentId}/feedback")]
	public async Task<FeedbackResponseDto> GiveFeedback(
		Guid homeworkId,
		Guid studentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] GiveFeedbackDto input
	)
	{
		return await _feedbackAppService.GiveFeedbackAsync(
			homeworkId,
			studentId,
			academicMediumId,
			input
		);
	}

	/// <summary>
	/// PATCH: api/homework/{homeworkId}/submissions/{studentId}/status?academicMediumId=xxx
	/// Updates only the submission status
	/// </summary>
	[HttpPatch("{homeworkId}/submissions/{studentId}/status")]
	public async Task<MessageResponseDto> UpdateStatus(
		Guid homeworkId,
		Guid studentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] UpdateSubmissionStatusDto input
	)
	{
		return await _feedbackAppService.UpdateStatusAsync(
			homeworkId,
			studentId,
			academicMediumId,
			input
		);
	}
}
