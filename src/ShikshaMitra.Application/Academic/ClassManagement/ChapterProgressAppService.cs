using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.ClassManagement.ChapterProgress;

[RemoteService(IsEnabled = false)]
public class ChapterProgressAppService(
	IRepository<StandardSubjectAssignment, Guid> subjectAssignmentRepository,
	IRepository<SubjectChapter, Guid> chapterRepository,
	IRepository<Syllabus.ChapterProgress, Guid> progressRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<Standard, Guid> standardRepository,
	IGuidGenerator guidGenerator,
	ILogger<ChapterProgressAppService> logger
) : ApplicationService, IChapterProgressAppService
{
	public async Task<List<ClassDivisionSubjectDto>> GetSubjectsForClassDivisionAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId
	)
	{
		// Validate required parameters
		if (academicMediumId == Guid.Empty)
		{
			throw new ArgumentException("Academic medium ID cannot be empty", nameof(academicMediumId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		if (divisionId == Guid.Empty)
		{
			throw new ArgumentException("Division ID cannot be empty", nameof(divisionId));
		}

		logger.LogInformation(
			"Getting subjects for class {ClassId}, division {DivisionId}",
			classId, divisionId
		);

		// Get all subject assignments for the class
		var assignments = await subjectAssignmentRepository.GetListAsync(a =>
			a.StandardId == classId &&
			a.EducationMediumId == academicMediumId
		);

		var result = new List<ClassDivisionSubjectDto>();
		if (!assignments.Any())
		{
			return result;
		}

		var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var subjectLookup = subjects.ToDictionary(s => s.Id);

		var chapters = await chapterRepository.GetListAsync(c => subjectIds.Contains(c.SubjectId));
		var chaptersBySubject = chapters
			.GroupBy(c => c.SubjectId)
			.ToDictionary(g => g.Key, g => g.ToList());
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var progresses = chapterIds.Count == 0
			? new List<Syllabus.ChapterProgress>()
			: await progressRepository.GetListAsync(p =>
				chapterIds.Contains(p.ChapterId) &&
				p.DivisionId == divisionId
			);
		// Group by ChapterId and select most recent to handle duplicates
		var progressLookup = progresses
			.GroupBy(p => p.ChapterId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(p => p.CreationTime).First()
			);

		foreach (var assignment in assignments)
		{
			if (!subjectLookup.TryGetValue(assignment.SubjectId, out var subject))
			{
				continue;
			}

			if (!chaptersBySubject.TryGetValue(subject.Id, out var subjectChapters))
			{
				subjectChapters = new List<SubjectChapter>();
			}

			var totalChapters = subjectChapters.Count;
			var completedChapters = 0;
			var progressSum = 0;

			foreach (var chapter in subjectChapters)
			{
				if (!progressLookup.TryGetValue(chapter.Id, out var progress))
				{
					continue;
				}

				progressSum += progress.Progress;
				if (progress.Progress == 100)
				{
					completedChapters++;
				}
			}

			var progressPercentage = totalChapters > 0
				? (int)Math.Round(progressSum / (double)totalChapters)
				: 0;

			result.Add(new ClassDivisionSubjectDto
			{
				SubjectId = subject.Id,
				SubjectName = subject.Name,
				SubjectCode = assignment.SubjectCode,
				TotalChapters = totalChapters,
				CompletedChapters = completedChapters,
				ProgressPercentage = progressPercentage
			});
		}

		return result;
	}

	public async Task<PaginatedChapterProgressResponseDto> GetChapterProgressListAsync(
		Guid academicMediumId,
		Guid classId,
		Guid divisionId,
		Guid subjectId,
		string? search = null
	)
	{
		// Validate required parameters
		if (academicMediumId == Guid.Empty)
		{
			throw new ArgumentException("Academic medium ID cannot be empty", nameof(academicMediumId));
		}

		if (classId == Guid.Empty)
		{
			throw new ArgumentException("Class ID cannot be empty", nameof(classId));
		}

		logger.LogInformation(
			"Getting chapter progress for subject {SubjectId}, division {DivisionId}, class {ClassId}, academicMedium {AcademicMediumId}",
			subjectId, divisionId, classId, academicMediumId
		);

		// Get all chapters for the subject
		var chaptersQuery = await chapterRepository.GetQueryableAsync();
		chaptersQuery = chaptersQuery.Where(c => c.SubjectId == subjectId);

		if (!string.IsNullOrWhiteSpace(search))
		{
			// Use culture-invariant, DB-friendly search
			// Try to parse search as number for ChapterNumber comparison
			var isNumericSearch = int.TryParse(search, out var searchNumber);
			
			// Build pattern for case-insensitive LIKE search
			var searchPattern = $"%{search}%";
			
			chaptersQuery = chaptersQuery.Where(c =>
				c.ChapterName.ToLower().Contains(search.ToLower()) ||
				(isNumericSearch && c.ChapterNumber == searchNumber)
			);
		}

		var chapters = await AsyncExecuter.ToListAsync(chaptersQuery.OrderBy(c => c.ChapterNumber));

		// Get progress for all chapters
		var chapterIds = chapters.Select(c => c.Id).ToList();
		var progresses = await progressRepository.GetListAsync(p =>
			chapterIds.Contains(p.ChapterId) &&
			p.DivisionId == divisionId
		);
		// Group by ChapterId and select most recent to handle duplicates
		var progressLookup = progresses
			.GroupBy(p => p.ChapterId)
			.ToDictionary(
				g => g.Key,
				g => g.OrderByDescending(p => p.CreationTime).First()
			);

		var result = new PaginatedChapterProgressResponseDto
		{
			TotalRecords = chapters.Count,
			Records = chapters.Select(chapter =>
			{
				progressLookup.TryGetValue(chapter.Id, out var progress);

				return new ChapterProgressRecordDto
				{
					ChapterId = chapter.Id,
					ChapterNumber = chapter.ChapterNumber,
					ChapterName = chapter.ChapterName,
					StartDate = progress?.StartDate,
					EndDate = progress?.CompletionDate,
					Progress = progress?.Progress ?? 0
				};
			}).ToList()
		};

		return result;
	}

	public async Task<ApiSuccessResponseDto> UpdateChapterProgressAsync(
		Guid chapterId,
		UpdateChapterProgressDto input
	)
	{
		logger.LogInformation(
			"Updating progress for chapter {ChapterId} in division {DivisionId} to {Progress}%",
			chapterId, input.DivisionId, input.Progress
		);

		try
		{
			// Retry logic to handle race conditions when inserting new progress records
			// If two concurrent requests try to insert for the same (ChapterId, DivisionId),
			// one will fail with a duplicate key error. We retry by reloading the record.
			const int maxRetries = 3;
			for (int attempt = 1; attempt <= maxRetries; attempt++)
			{
				try
				{
					// Find existing progress record for the chapter and division
					var progress = await progressRepository.FirstOrDefaultAsync(p => 
						p.ChapterId == chapterId && 
						p.DivisionId == input.DivisionId
					);

					if (progress != null)
					{
						// Update existing record
						progress.UpdateProgress(input.Progress, input.Description);
						await progressRepository.UpdateAsync(progress);
						
						logger.LogDebug(
							"Updated existing progress for chapter {ChapterId}, division {DivisionId}",
							chapterId, input.DivisionId
						);
						break; // Success - exit retry loop
					}
					else
					{
						// Create new progress record
						var newProgress = new Syllabus.ChapterProgress(
							guidGenerator.Create(),
							chapterId,
							input.DivisionId
						);
						newProgress.UpdateProgress(input.Progress, input.Description);
						await progressRepository.InsertAsync(newProgress);
						
						logger.LogDebug(
							"Created new progress for chapter {ChapterId}, division {DivisionId}",
							chapterId, input.DivisionId
						);
						break; // Success - exit retry loop
					}
				}
				catch (Exception ex) when (
					attempt < maxRetries && 
					IsDuplicateKeyException(ex)
				)
				{
					// Concurrent insert detected - another request created the record
					// Retry by reloading the existing record and updating it
					logger.LogWarning(
						ex,
						"Concurrent insert detected for chapter {ChapterId}, division {DivisionId}. Retry attempt {Attempt}/{MaxRetries}",
						chapterId, input.DivisionId, attempt, maxRetries
					);
					
					// Brief delay to allow the other transaction to complete
					await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));
					
					// Continue to next iteration - will reload and update existing record
				}
			}

			return new ApiSuccessResponseDto
			{
				Success = true,
				Message = "Chapter progress updated successfully."
			};
		}
		catch (ArgumentOutOfRangeException ex)
		{
			throw new BusinessException("ClassManagement:InvalidProgress")
				.WithData("Message", ex.Message);
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex, 
				"Failed to update chapter progress for chapter {ChapterId}, division {DivisionId}",
				chapterId, input.DivisionId
			);
			throw new UserFriendlyException(
				"Failed to update chapter progress. Please try again later."
			);
		}
	}

	/// <summary>
	/// Determines if the exception is a duplicate key/unique constraint violation.
	/// Handles database-specific exception types using type checking, error codes, and message patterns.
	/// </summary>
	private bool IsDuplicateKeyException(Exception ex)
	{
		if (ex == null)
		{
			return false;
		}

		// Check exception type names for database-specific types
		var exceptionTypeName = ex.GetType().Name;
		var innerExceptionTypeName = ex.InnerException?.GetType().Name ?? string.Empty;
		
		// Common DB exception types that indicate constraint violations
		if (exceptionTypeName.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase) ||
		    innerExceptionTypeName.Contains("PostgresException", StringComparison.OrdinalIgnoreCase) ||
		    innerExceptionTypeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase) ||
		    innerExceptionTypeName.Contains("MySqlException", StringComparison.OrdinalIgnoreCase))
		{
			// Check for PostgreSQL-specific SqlState "23505" (unique violation)
			if (ex.InnerException != null)
			{
				var innerEx = ex.InnerException;
				
				// Try to get SqlState property using reflection
				var sqlStateProperty = innerEx.GetType().GetProperty("SqlState");
				if (sqlStateProperty != null)
				{
					var sqlState = sqlStateProperty.GetValue(innerEx)?.ToString();
					if (sqlState == "23505") // PostgreSQL unique violation
					{
						return true;
					}
				}
				
				// Try to get ConstraintName property using reflection
				var constraintNameProperty = innerEx.GetType().GetProperty("ConstraintName");
				if (constraintNameProperty != null)
				{
					var constraintName = constraintNameProperty.GetValue(innerEx)?.ToString();
					if (!string.IsNullOrEmpty(constraintName))
					{
						return true;
					}
				}
				
				// Check SQL Server error number (2627 = unique constraint, 2601 = unique index)
				var numberProperty = innerEx.GetType().GetProperty("Number");
				if (numberProperty != null)
				{
					var errorNumber = numberProperty.GetValue(innerEx);
					if (errorNumber is int number && (number == 2627 || number == 2601))
					{
						return true;
					}
				}
			}
		}

		// Fallback: Check exception messages using culture-invariant comparison
		var message = ex.Message ?? string.Empty;
		var innerMessage = ex.InnerException?.Message ?? string.Empty;
		
		return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
		       message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
		       message.Contains("unique index", StringComparison.OrdinalIgnoreCase) ||
		       message.Contains("cannot insert duplicate", StringComparison.OrdinalIgnoreCase) ||
		       message.Contains("violation of unique key", StringComparison.OrdinalIgnoreCase) ||
		       innerMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
		       innerMessage.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
		       innerMessage.Contains("unique index", StringComparison.OrdinalIgnoreCase);
	}
}
