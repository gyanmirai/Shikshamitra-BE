using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Domain service for managing chapter business logic and validation
/// </summary>
public class ChapterManager : DomainService
{
	private readonly IRepository<SubjectChapter, Guid> _chapterRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IGuidGenerator _guidGenerator;

	public ChapterManager(
		IRepository<SubjectChapter, Guid> chapterRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IGuidGenerator guidGenerator
	)
	{
		_chapterRepository = chapterRepository;
		_academicYearRepository = academicYearRepository;
		_guidGenerator = guidGenerator;
	}

	/// <summary>
	/// Validates that chapter number is unique within a subject
	/// </summary>
	public async Task ValidateChapterNumberAsync(
		Guid subjectId,
		int chapterNumber,
		Guid? excludeChapterId = null
	)
	{
		var query = await _chapterRepository.GetQueryableAsync();
		var exists = query.Any(c =>
			c.SubjectId == subjectId &&
			c.ChapterNumber == chapterNumber &&
			(excludeChapterId == null || c.Id != excludeChapterId.Value)
		);

		if (exists)
		{
			throw new BusinessException("Chapter:DuplicateChapterNumber")
				.WithData("ChapterNumber", chapterNumber)
				.WithData("SubjectId", subjectId);
		}
	}

	/// <summary>
	/// Validates that dates are within academic year boundaries
	/// </summary>
	public async Task ValidateDatesAsync(
		DateTime startDate,
		DateTime endDate,
		Guid? tenantId
	)
	{
		// Validate end date >= start date
		if (endDate < startDate)
		{
			throw new BusinessException("Chapter:InvalidDateRange")
				.WithData("StartDate", startDate.ToString("yyyy-MM-dd"))
				.WithData("EndDate", endDate.ToString("yyyy-MM-dd"));
		}

		// Get current active academic year for tenant (most recent one that contains current date)
		var now = DateTime.UtcNow;
		var query = await _academicYearRepository.GetQueryableAsync();
		var academicYear = query
			.Where(ay => ay.TenantId == tenantId && ay.StartDate <= now && ay.EndDate >= now)
			.OrderByDescending(ay => ay.CreationTime)
			.FirstOrDefault();

		if (academicYear == null)
		{
			throw new BusinessException("Chapter:NoActiveAcademicYear");
		}

		// Validate against academic year boundaries
		var ayStartDate = academicYear.StartDate.Date;
		var ayEndDate = academicYear.EndDate.Date;
		var chapterStartDate = startDate.Date;
		var chapterEndDate = endDate.Date;

		if (chapterStartDate < ayStartDate || chapterStartDate > ayEndDate)
		{
			throw new BusinessException("Chapter:StartDateOutOfRange")
				.WithData("MinDate", ayStartDate.ToString("yyyy-MM-dd"))
				.WithData("MaxDate", ayEndDate.ToString("yyyy-MM-dd"))
				.WithData("ProvidedDate", chapterStartDate.ToString("yyyy-MM-dd"));
		}

		if (chapterEndDate < ayStartDate || chapterEndDate > ayEndDate)
		{
			throw new BusinessException("Chapter:EndDateOutOfRange")
				.WithData("MinDate", ayStartDate.ToString("yyyy-MM-dd"))
				.WithData("MaxDate", ayEndDate.ToString("yyyy-MM-dd"))
				.WithData("ProvidedDate", chapterEndDate.ToString("yyyy-MM-dd"));
		}
	}

	/// <summary>
	/// Creates a new chapter with validation
	/// </summary>
	public async Task<SubjectChapter> CreateChapterAsync(
		Guid standardId,
		Guid subjectId,
		Guid educationMediumId,
		int chapterNumber,
		string chapterName,
		DateTime startDate,
		DateTime endDate,
		string? description,
		Guid? tenantId
	)
	{
		// Validate chapter number uniqueness
		await ValidateChapterNumberAsync(subjectId, chapterNumber);

		// Validate dates
		await ValidateDatesAsync(startDate, endDate, tenantId);

		// Create chapter
		var chapter = new SubjectChapter(
			id: _guidGenerator.Create(),
			standardId: standardId,
			subjectId: subjectId,
			educationMediumId: educationMediumId,
			chapterNumber: chapterNumber,
			chapterName: chapterName,
			chapterStartTime: new DateTimeOffset(startDate),
			chapterEndTime: new DateTimeOffset(endDate),
			description: description
		);

		return chapter;
	}

	/// <summary>
	/// Updates an existing chapter with validation
	/// </summary>
	public async Task UpdateChapterAsync(
		SubjectChapter chapter,
		int chapterNumber,
		string chapterName,
		DateTime startDate,
		DateTime endDate,
		string? description,
		Guid? tenantId
	)
	{
		// Validate chapter number uniqueness (exclude current chapter)
		await ValidateChapterNumberAsync(chapter.SubjectId, chapterNumber, chapter.Id);

		// Validate dates
		await ValidateDatesAsync(startDate, endDate, tenantId);

		// Update chapter
		chapter.Update(
			chapterName: chapterName,
			chapterStartTime: new DateTimeOffset(startDate),
			chapterEndTime: new DateTimeOffset(endDate),
			description: description
		);

		chapter.ChapterNumber = chapterNumber;
	}
}
