using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for Subject management
/// </summary>
public interface ISubjectAppService
{
	/// <summary>
	/// Get a single subject by ID
	/// </summary>
	/// <param name="id">Subject ID</param>
	/// <returns>Subject details</returns>
	Task<SubjectDto> GetAsync(Guid id);

	/// <summary>
	/// Get list of subjects for a specific education medium
	/// </summary>
	/// <param name="educationMediumId">Education Medium ID</param>
	/// <param name="searchTerm">Optional search term to filter subjects by name</param>
	/// <returns>List of subjects</returns>
	Task<List<SubjectDto>> GetListAsync(Guid educationMediumId, string? searchTerm = null);

	/// <summary>
	/// Create a new subject
	/// </summary>
	/// <param name="createSubjectDto">Subject creation data</param>
	/// <returns>Created subject details</returns>
	Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto);

	Task<List<SubjectDto>> BulkCreateAsync(BulkCreateSubjectsDto input);

	/// <summary>
	/// Update an existing subject
	/// </summary>
	/// <param name="id">Subject ID</param>
	/// <param name="updateSubjectDto">Subject update data</param>
	/// <returns>Updated subject details</returns>
	Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto);

	Task<SubjectDto> ArchiveAsync(Guid id, SubjectStatusChangeDto input);

	Task<SubjectDto> RestoreAsync(Guid id);

	/// <summary>
	/// Delete a subject
	/// </summary>
	/// <param name="id">Subject ID</param>
	Task DeleteAsync(Guid id);

	/// <summary>
	/// Check whether a subject has any linked records (allocations, timetable slots, exams, etc.)
	/// before deciding to delete it.
	/// </summary>
	/// <param name="id">Subject ID</param>
	Task<SubjectUsageCheckDto> CheckUsagesAsync(Guid id);

	Task<SubjectDuplicateCheckResultDto> CheckDuplicateAsync(
		Guid educationMediumId,
		SubjectDuplicateCheckDto input
	);

	Task<SubjectUsageDetailDto> GetUsageDetailsAsync(Guid id);

	Task<List<SubjectAuditLogDto>> GetAuditLogsAsync(Guid id);
}
