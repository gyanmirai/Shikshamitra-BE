using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service for Chapter Management operations
/// </summary>
[RemoteService(IsEnabled = false)]
public class ChapterManagementAppService(
	IRepository<SubjectChapter, Guid> chapterRepository,
	IRepository<ChapterDocument, Guid> documentRepository,
	ChapterManager chapterManager,
	IGuidGenerator guidGenerator,
	ILogger<ChapterManagementAppService> logger
) : ApplicationService, IChapterManagementAppService
{
	public async Task<ChapterResponseDto> CreateAsync(
		Guid academicMediumId,
		Guid classId,
		Guid subjectId,
		CreateChapterDto input
	)
	{
		logger.LogInformation(
			"Creating chapter '{ChapterName}' (No. {ChapterNumber}) for subject {SubjectId} in class {ClassId}",
			input.ChapterName,
			input.ChapterNo,
			subjectId,
			classId
		);

		try
		{
			// Create chapter using domain manager (with validation)
			var chapter = await chapterManager.CreateChapterAsync(
				standardId: classId,
				subjectId: subjectId,
				educationMediumId: academicMediumId,
				chapterNumber: input.ChapterNo,
				chapterName: input.ChapterName,
				startDate: input.StartDate,
				endDate: input.EndDate,
				description: input.Description,
				tenantId: CurrentTenant.Id
			);

			// Insert chapter
			await chapterRepository.InsertAsync(chapter, autoSave: false);

			// Add documents if provided
			if (input.Documents?.Any() == true)
			{
				foreach (var docDto in input.Documents)
				{
					var document = new ChapterDocument(
						id: guidGenerator.Create(),
						chapterId: chapter.Id,
						fileName: docDto.Name,
						fileUrl: docDto.Url,
						fileSize: 0, // Will be populated by frontend
						contentType: "application/octet-stream" // Default, will be determined by file extension
					);

					await documentRepository.InsertAsync(document, autoSave: false);
					chapter.AddDocument(document);
				}
			}

			// Save all changes
			await CurrentUnitOfWork.SaveChangesAsync();

			logger.LogInformation(
				"Successfully created chapter {ChapterId} with {DocumentCount} documents",
				chapter.Id,
				input.Documents?.Count ?? 0
			);

			return await MapToResponseDtoAsync(chapter);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to create chapter '{ChapterName}' for subject {SubjectId}",
				input.ChapterName,
				subjectId
			);
			throw new UserFriendlyException(
				"Failed to create chapter. Please try again later."
			);
		}
	}

	public async Task<List<ChapterResponseDto>> GetListAsync(
		Guid academicMediumId,
		Guid classId,
		Guid subjectId
	)
	{
		logger.LogInformation(
			"Getting chapters for subject {SubjectId} in class {ClassId}",
			subjectId,
			classId
		);

		var query = await chapterRepository.GetQueryableAsync();

		// Load chapters asynchronously using ABP's AsyncExecuter
		var chapters = await AsyncExecuter.ToListAsync(
			query
				.Where(c =>
					c.SubjectId == subjectId &&
					c.StandardId == classId &&
					c.EducationMediumId == academicMediumId
				)
				.OrderBy(c => c.ChapterNumber)
		);

		// If no chapters, return empty list
		if (!chapters.Any())
		{
			return new List<ChapterResponseDto>();
		}

		// Batch-load all documents for these chapters in a single query
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var allDocuments = await documentRepository.GetListAsync(d => chapterIds.Contains(d.ChapterId));

		// Group documents by chapter ID for efficient lookup
		var documentsByChapterId = allDocuments
			.GroupBy(d => d.ChapterId)
			.ToDictionary(g => g.Key, g => g.ToList());

		// Map chapters to response DTOs using pre-fetched documents
		var responseDtos = chapters.Select(chapter =>
			MapToResponseDto(chapter, documentsByChapterId.GetValueOrDefault(chapter.Id) ?? new List<ChapterDocument>())
		).ToList();

		return responseDtos;
	}

	public async Task<ChapterResponseDto> GetAsync(Guid chapterId)
	{
		logger.LogInformation("Getting chapter {ChapterId}", chapterId);

		var chapter = await chapterRepository.FindAsync(chapterId);

		if (chapter == null)
		{
			throw new BusinessException("Chapter:NotFound")
				.WithData("ChapterId", chapterId);
		}

		return await MapToResponseDtoAsync(chapter);
	}

	public async Task<ChapterResponseDto> UpdateAsync(
		Guid chapterId,
		Guid academicMediumId,
		Guid classId,
		Guid subjectId,
		UpdateChapterDto input
	)
	{
		logger.LogInformation(
			"Updating chapter {ChapterId}",
			chapterId
		);

		try
		{
			var chapter = await chapterRepository.GetAsync(chapterId);

			// Verify chapter belongs to the specified subject/class/medium
			if (chapter.SubjectId != subjectId ||
				chapter.StandardId != classId ||
				chapter.EducationMediumId != academicMediumId)
			{
				throw new BusinessException("Chapter:InvalidContext")
					.WithData("ChapterId", chapterId);
			}

			// Update chapter using domain manager (with validation)
			await chapterManager.UpdateChapterAsync(
				chapter: chapter,
				chapterNumber: input.ChapterNo,
				chapterName: input.ChapterName,
				startDate: input.StartDate,
				endDate: input.EndDate,
				description: input.Description,
				tenantId: CurrentTenant.Id
			);

			// Update documents (remove old, add new)
			var existingDocuments = await documentRepository.GetListAsync(d => d.ChapterId == chapterId);
			await documentRepository.DeleteManyAsync(existingDocuments, autoSave: false);

			if (input.Documents?.Any() == true)
			{
				foreach (var docDto in input.Documents)
				{
					var document = new ChapterDocument(
						id: guidGenerator.Create(),
						chapterId: chapter.Id,
						fileName: docDto.Name,
						fileUrl: docDto.Url,
						fileSize: 0,
						contentType: "application/octet-stream"
					);

					await documentRepository.InsertAsync(document, autoSave: false);
				}
			}

			// Save changes
			await chapterRepository.UpdateAsync(chapter, autoSave: false);
			await CurrentUnitOfWork.SaveChangesAsync();

			logger.LogInformation(
				"Successfully updated chapter {ChapterId}",
				chapterId
			);

			return await MapToResponseDtoAsync(chapter);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to update chapter {ChapterId}",
				chapterId
			);
			throw new UserFriendlyException(
				"Failed to update chapter. Please try again later."
			);
		}
	}

	public async Task DeleteAsync(Guid chapterId)
	{
		logger.LogInformation("Deleting chapter {ChapterId}", chapterId);

		var chapter = await chapterRepository.FindAsync(chapterId);

		if (chapter == null)
		{
			throw new BusinessException("Chapter:NotFound")
				.WithData("ChapterId", chapterId);
		}

		// Documents will be cascade deleted by EF Core configuration
		await chapterRepository.DeleteAsync(chapter, autoSave: true);

		logger.LogInformation(
			"Successfully deleted chapter {ChapterId}",
			chapterId
		);
	}

	private async Task<ChapterResponseDto> MapToResponseDtoAsync(SubjectChapter chapter)
	{
		// Get documents for this chapter
		var documents = await documentRepository.GetListAsync(d => d.ChapterId == chapter.Id);

		return MapToResponseDto(chapter, documents);
	}

	private ChapterResponseDto MapToResponseDto(SubjectChapter chapter, List<ChapterDocument> documents)
	{
		var response = new ChapterResponseDto
		{
			Id = chapter.Id,
			ChapterNo = chapter.ChapterNumber,
			ChapterName = chapter.ChapterName,
			Description = chapter.Description,
			StartDate = chapter.ChapterStartTime.DateTime,
			EndDate = chapter.ChapterEndTime.DateTime,
			AcademicMediumId = chapter.EducationMediumId,
			ClassId = chapter.StandardId,
			SubjectId = chapter.SubjectId,
			CreatedAt = chapter.CreationTime,
			Documents = documents.Select(d => new ChapterDocumentDto
			{
				Name = d.FileName,
				Url = d.FileUrl
			}).ToList()
		};

		return response;
	}
}
