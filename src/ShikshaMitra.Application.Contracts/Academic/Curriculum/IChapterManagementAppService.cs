using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service interface for Chapter operations
/// Aligned with FE API contract
/// </summary>
public interface IChapterManagementAppService : IApplicationService
{
	/// <summary>
	/// Creates a new chapter for a subject
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID (from query parameter)</param>
	/// <param name="classId">Standard/Class ID (from query parameter)</param>
	/// <param name="subjectId">Subject ID (from query parameter)</param>
	/// <param name="input">Create input DTO</param>
	/// <returns>Created chapter</returns>
	Task<ChapterResponseDto> CreateAsync(
		Guid academicMediumId,
		Guid classId,
		Guid subjectId,
		CreateChapterDto input
	);

	/// <summary>
	/// Gets all chapters for a specific subject
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="classId">Standard/Class ID</param>
	/// <param name="subjectId">Subject ID</param>
	/// <returns>List of chapters ordered by chapter number</returns>
	Task<List<ChapterResponseDto>> GetListAsync(
		Guid academicMediumId,
		Guid classId,
		Guid subjectId
	);

	/// <summary>
	/// Gets a single chapter by ID
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	/// <returns>Chapter details</returns>
	Task<ChapterResponseDto> GetAsync(Guid chapterId);

	/// <summary>
	/// Updates an existing chapter
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="classId">Standard/Class ID</param>
	/// <param name="subjectId">Subject ID</param>
	/// <param name="input">Update input DTO</param>
	/// <returns>Updated chapter</returns>
	Task<ChapterResponseDto> UpdateAsync(
		Guid chapterId,
		Guid academicMediumId,
		Guid classId,
		Guid subjectId,
		UpdateChapterDto input
	);

	/// <summary>
	/// Deletes a chapter and its documents
	/// </summary>
	/// <param name="chapterId">Chapter ID</param>
	Task DeleteAsync(Guid chapterId);
}
