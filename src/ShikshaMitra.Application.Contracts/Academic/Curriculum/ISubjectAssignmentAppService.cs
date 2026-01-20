using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for Subject Assignment operations
/// </summary>
public interface ISubjectAssignmentAppService : IApplicationService
{
	/// <summary>
	/// Creates a new subject assignment group
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (from query parameter)</param>
	/// <param name="createSubjectAssignmentDto">Create input DTO</param>
	/// <returns>Created assignment group</returns>
	Task CreateAsync(
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto createSubjectAssignmentDto
	);

	/// <summary>
	/// Gets all subject assignment groups for a standard with optional filters
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="classId">Standard/Class ID</param>
	/// <param name="page">Page number (0-indexed)</param>
	/// <param name="pageSize">Items per page</param>
	/// <returns>Paginated list of assignment groups</returns>
	Task<SubjectAssignmentPagedResponse> GetListAsync(
		Guid classId,
		int page,
		int pageSize
	);

	/// <summary>
	/// Gets a single assignment group by ID
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Assignment group details</returns>
	Task<SubjectAssignmentDto> GetAsync(Guid assignmentId);

	/// <summary>
	/// Updates an existing subject assignment group
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="input">Update input DTO</param>
	/// <returns>Updated assignment group</returns>
	Task<SubjectAssignmentDto> UpdateAsync(
		Guid assignmentId,
		Guid academicMediumId,
		CreateUpdateSubjectAssignmentDto input
	);

	/// <summary>
	/// Changes the status of a subject assignment
	/// </summary>
	/// <param name="assignmentId">Assignment ID</param>
	/// <param name="input">Status change request</param>
	/// <returns>Message response</returns>
	Task ChangeStatusAsync(Guid assignmentId, ChangeStatusRequestDto input);

	/// <summary>
	/// Publishes a draft assignment (changes status to Published)
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	/// <returns>Publish response with new status</returns>
	Task<PublishResponseDto> PublishAsync(List<Guid> assignmentId);

	/// <summary>
	/// Deletes an assignment group and all its subjects
	/// </summary>
	/// <param name="assignmentId">Assignment group ID</param>
	Task DeleteAsync(Guid assignmentId);

	/// <summary>
	/// Gets core and elective subjects for a given academic medium
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <returns>List of core and elective subjects</returns>
	Task<List<CoreElectiveSubjectResponseDto>> GetCoreElectiveSubjectsAsync(Guid academicMediumId);
}
