using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common.Responses;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

/// <summary>
/// Controller for Subject Assignment operations
/// Aligned with FE contract: /api/subject-assignments
/// </summary>
[Route("api/academics/curriculum")]
[Authorize]
[ApiController]
public class SubjectAssignmentController(
	ISubjectAssignmentAppService subjectAssignmentAppService
) : AbpController
{
	/// <summary>
	/// Creates a new subject assignment group (Draft or Published)
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="createSubjectAssignmentDto">Subject assignment group details</param>
	/// <returns>Created assignment group</returns>
	/// <response code="201">Assignment group created successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="401">Unauthorized</response>
	[HttpPost("subject-assignments")]
	[ProducesResponseType(typeof(SubjectAssignmentDto), 201)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public async Task<Result> CreateAsync(
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	)
	{
		await subjectAssignmentAppService.CreateAsync(academicMediumId, createSubjectAssignmentDto);
		return Result.Success();
	}

	/// <summary>
	/// Gets all subject assignment groups by class (paginated)
	/// </summary>
	/// <param name="standardId">Standard/Class ID (required)</param>
	/// <param name="page">Page number (default 0)</param>
	/// <param name="pageSize">Page size (default 10)</param>
	/// <param name="status">Optional assignment status filter. Defaults to Published when omitted.</param>
	/// <param name="includeArchived">Return every status, including archived, when no status filter is supplied.</param>
	/// <returns>Paginated list of assignment groups</returns>
	/// <response code="200">Subject assignments fetched</response>
	[HttpGet("{standardId}/subject-assignments")]
	[ProducesResponseType(typeof(SubjectAssignmentPagedResponse), 200)]
	public async Task<Result<SubjectAssignmentPagedResponse>> GetListAsync(
		[FromRoute] Guid standardId,
		[FromQuery] int page = 0,
		[FromQuery] int pageSize = 10,
		[FromQuery] SubjectStatus? status = null,
		[FromQuery] bool includeArchived = false
	)
	{
		var result = await subjectAssignmentAppService.GetListAsync(
			standardId,
			page,
			pageSize,
			status,
			includeArchived
		);

		return Result.Success(result);
	}

	/// <summary>
	/// Gets a single subject assignment group by ID
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Assignment group details</returns>
	/// <response code="200">Returns the assignment group</response>
	/// <response code="404">Assignment not found</response>
	[HttpGet("subject-assignments/{assignmentId}")]
	[ProducesResponseType(typeof(SubjectAssignmentDto), 200)]
	[ProducesResponseType(404)]
	public async Task<Result<SubjectAssignmentDto>> GetAsync([FromRoute] Guid assignmentId)
	{
		var result = await subjectAssignmentAppService.GetAsync(assignmentId);
		return Result.Success(result);
	}

	/// <summary>
	/// Updates an existing subject assignment group
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <param name="academicMediumId">Academic medium ID (required query parameter)</param>
	/// <param name="input">Updated assignment details</param>
	/// <returns>Updated assignment group</returns>
	/// <response code="200">Assignment updated successfully</response>
	/// <response code="400">Validation failed</response>
	/// <response code="404">Assignment not found</response>
	[HttpPut("subject-assignments/{assignmentId}")]
	[ProducesResponseType(typeof(SubjectAssignmentDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(404)]
	public async Task<Result<SubjectAssignmentDto>> UpdateAsync(
		[FromRoute] Guid assignmentId,
		[FromQuery] Guid academicMediumId,
		[FromBody] CreateUpdateSubjectAssignmentDto input
	)
	{
		var result = await subjectAssignmentAppService.UpdateAsync(
			assignmentId,
			academicMediumId,
			input
		);

		return Result.Success(result);
	}

	/// <summary>
	/// Change subject assignment status
	/// </summary>
	/// <param name="assignmentId">Assignment Id</param>
	/// <param name="request">Status change request</param>
	/// <returns>Status updated message</returns>
	/// <response code="200">Status updated</response>
	[HttpPatch("subject-assignments/{assignmentId}/status")]
	public async Task<Result> ChangeStatusAsync(
		[FromRoute] Guid assignmentId,
		[FromBody] ChangeStatusRequestDto request
	)
	{
		await subjectAssignmentAppService.ChangeStatusAsync(assignmentId, request);
		return Result.Success();
	}

	/// <summary>
	/// Deletes a subject assignment group and all its subjects
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>No content on success</returns>
	/// <response code="204">Assignment deleted successfully</response>
	/// <response code="404">Assignment not found</response>
	[HttpDelete("subject-assignments/{assignmentId}")]
	[ProducesResponseType(204)]
	[ProducesResponseType(404)]
	public async Task<Result> DeleteAsync([FromRoute] Guid assignmentId)
	{
		await subjectAssignmentAppService.DeleteAsync(assignmentId);
		return Result.Success();
	}

	// API controller to get core and elective subjects
	[HttpGet("{academicMediumId}/core-elective-subjects")]
	[ProducesResponseType(typeof(List<CoreElectiveSubjectResponseDto>), 200)]
	[ProducesResponseType(400)]
	public async Task<Result<List<CoreElectiveSubjectResponseDto>>> GetCoreElectiveSubjectsAsync(
		[FromRoute] Guid academicMediumId
	)
	{
		if (academicMediumId == Guid.Empty)
		{
			return Result.Failure<List<CoreElectiveSubjectResponseDto>>(
				new Error("Validation failed", "academicMediumId is required")
			);
		}

		var result = await subjectAssignmentAppService.GetCoreElectiveSubjectsAsync(academicMediumId);
		return Result.Success(result);
	}

	/// <summary>Returns setup health score and missing items for a single subject assignment</summary>
	[HttpGet("subject-assignments/{assignmentId}/setup-health")]
	[ProducesResponseType(typeof(SetupHealthDto), 200)]
	[ProducesResponseType(404)]
	public async Task<Result<SetupHealthDto>> GetSetupHealthAsync([FromRoute] Guid assignmentId)
	{
		var result = await subjectAssignmentAppService.GetSetupHealthAsync(assignmentId);
		return Result.Success(result);
	}

	/// <summary>Returns publish readiness report for all subjects in a class</summary>
	[HttpGet("{classId}/publish-readiness")]
	[ProducesResponseType(typeof(PublishReadinessDto), 200)]
	public async Task<Result<PublishReadinessDto>> GetPublishReadinessAsync([FromRoute] Guid classId)
	{
		var result = await subjectAssignmentAppService.GetPublishReadinessAsync(classId);
		return Result.Success(result);
	}

	/// <summary>Returns workload summary for a teacher</summary>
	[HttpGet("teachers/{teacherId}/workload")]
	[ProducesResponseType(typeof(TeacherWorkloadDto), 200)]
	[ProducesResponseType(404)]
	public async Task<Result<TeacherWorkloadDto>> GetTeacherWorkloadAsync([FromRoute] Guid teacherId)
	{
		var result = await subjectAssignmentAppService.GetTeacherWorkloadAsync(teacherId);
		return Result.Success(result);
	}

	/// <summary>Returns KPI summary cards data for a class</summary>
	[HttpGet("{classId}/subject-kpi")]
	[ProducesResponseType(typeof(SubjectKpiDto), 200)]
	public async Task<Result<SubjectKpiDto>> GetKpiAsync([FromRoute] Guid classId)
	{
		var result = await subjectAssignmentAppService.GetKpiAsync(classId);
		return Result.Success(result);
	}

	/// <summary>Archives a subject assignment (safe alternative to delete when subject is in use)</summary>
	[HttpPost("subject-assignments/{assignmentId}/archive")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	public async Task<Result> ArchiveAsync([FromRoute] Guid assignmentId)
	{
		await subjectAssignmentAppService.ArchiveAsync(assignmentId);
		return Result.Success();
	}

	/// <summary>Restores an archived subject assignment as Draft after conflict checks</summary>
	[HttpPost("subject-assignments/{assignmentId}/restore")]
	[ProducesResponseType(200)]
	[ProducesResponseType(404)]
	public async Task<Result> RestoreAsync([FromRoute] Guid assignmentId)
	{
		await subjectAssignmentAppService.RestoreAsync(assignmentId);
		return Result.Success();
	}
}
