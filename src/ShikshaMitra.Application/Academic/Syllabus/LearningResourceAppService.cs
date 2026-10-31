using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Syllabus;

[Authorize]
[RemoteService(IsEnabled = false)]
public class LearningResourceAppService(
	IRepository<LearningResource, Guid> learningResourceRepository,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IRepository<AssignedSubjectChapter, Guid> chapterRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<Division, Guid> divisionRepository
) : ApplicationService, ILearningResourceAppService
{
	private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"application/pdf",
		"image/png",
		"image/jpeg",
		"image/jpg",
		"application/vnd.openxmlformats-officedocument.wordprocessingml.document",
		"application/vnd.openxmlformats-officedocument.presentationml.presentation",
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
		"text/plain",
		"text/csv",
		"video/mp4"
	};

	public async Task<LearningResourceDto> CreateAsync(CreateUpdateLearningResourceDto input)
	{
		await EnsureTargetIsValidAsync(input.StandardId, input.SubjectId, input.ChapterId, input.DivisionId);
		var file = ResolveFile(input);
		EnsureFileIsAllowed(file.ContentType, file.Size);

		var resource = new LearningResource(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.StandardId,
			input.SubjectId,
			input.ChapterId,
			input.DivisionId,
			input.ResourceTitle,
			input.Topic,
			input.ResourceType,
			file.BlobName,
			file.FileName,
			file.ContentType,
			file.Size,
			input.Visibility);

		await learningResourceRepository.InsertAsync(resource, autoSave: true);
		return await MapAsync(resource);
	}

	public async Task<List<LearningResourceDto>> GetListAsync(GetLearningResourcesInput input)
	{
		var resources = await learningResourceRepository.GetListAsync(r =>
			(!input.StandardId.HasValue || r.StandardId == input.StandardId.Value) &&
			(!input.SubjectId.HasValue || r.SubjectId == input.SubjectId.Value) &&
			(!input.ChapterId.HasValue || r.ChapterId == input.ChapterId.Value) &&
			(!input.DivisionId.HasValue || !r.DivisionId.HasValue || r.DivisionId == input.DivisionId.Value) &&
			(!input.Status.HasValue || r.Status == input.Status.Value) &&
			(!input.Visibility.HasValue || r.Visibility == input.Visibility.Value));

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			resources = resources
				.Where(r =>
					r.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(r.Topic?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
					r.FileName.Contains(search, StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		return await MapManyAsync(resources.OrderByDescending(r => r.CreationTime).ToList());
	}

	public async Task<LearningResourceDto> GetAsync(Guid id)
	{
		return await MapAsync(await learningResourceRepository.GetAsync(id));
	}

	public async Task<LearningResourceDto> UpdateAsync(Guid id, CreateUpdateLearningResourceDto input)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		await EnsureTargetIsValidAsync(input.StandardId, input.SubjectId, input.ChapterId, input.DivisionId);
		var file = ResolveFile(input, resource);
		EnsureFileIsAllowed(file.ContentType, file.Size);

		resource.Update(
			input.ResourceTitle,
			input.Topic,
			input.ResourceType,
			file.BlobName,
			file.FileName,
			file.ContentType,
			file.Size,
			input.Visibility);

		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
		return await MapAsync(resource);
	}

	public async Task SubmitAsync(Guid id)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		resource.Submit(CurrentUser.Id);
		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
	}

	public async Task PublishAsync(Guid id, LearningResourceReviewDto input)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		resource.Publish(CurrentUser.Id, input.Remarks);
		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
	}

	public async Task RejectAsync(Guid id, LearningResourceReviewDto input)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		resource.Reject(CurrentUser.Id, input.Remarks ?? string.Empty);
		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
	}

	public async Task ArchiveAsync(Guid id, LearningResourceArchiveDto input)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		resource.Archive(input.Reason);
		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
	}

	public async Task RestoreAsync(Guid id)
	{
		var resource = await learningResourceRepository.GetAsync(id);
		await EnsureTargetIsValidAsync(resource.StandardId, resource.SubjectId, resource.ChapterId, resource.DivisionId);
		resource.Restore();
		await learningResourceRepository.UpdateAsync(resource, autoSave: true);
	}

	public async Task<LearningResourceHealthDto> GetHealthAsync(GetLearningResourcesInput input)
	{
		var resources = await GetListRawAsync(input);
		var warnings = new List<string>();
		var broken = 0;

		foreach (var resource in resources)
		{
			var targetExists = await TargetExistsAsync(resource.StandardId, resource.SubjectId, resource.ChapterId, resource.DivisionId);
			if (!targetExists)
			{
				broken++;
				warnings.Add($"Resource '{resource.Title}' points to a missing or inactive syllabus target.");
			}

			if (resource.Status == LearningResourceStatus.Published && resource.Visibility == LearningResourceVisibility.Private)
			{
				warnings.Add($"Published resource '{resource.Title}' is still private.");
			}
		}

		return new LearningResourceHealthDto
		{
			TotalCount = resources.Count,
			DraftCount = resources.Count(r => r.Status == LearningResourceStatus.Draft),
			SubmittedCount = resources.Count(r => r.Status == LearningResourceStatus.Submitted),
			PublishedCount = resources.Count(r => r.Status == LearningResourceStatus.Published),
			ArchivedCount = resources.Count(r => r.Status == LearningResourceStatus.Archived),
			BrokenReferenceCount = broken,
			Warnings = warnings.Take(50).ToList()
		};
	}

	private async Task<List<LearningResource>> GetListRawAsync(GetLearningResourcesInput input)
	{
		return await learningResourceRepository.GetListAsync(r =>
			(!input.StandardId.HasValue || r.StandardId == input.StandardId.Value) &&
			(!input.SubjectId.HasValue || r.SubjectId == input.SubjectId.Value) &&
			(!input.ChapterId.HasValue || r.ChapterId == input.ChapterId.Value) &&
			(!input.DivisionId.HasValue || !r.DivisionId.HasValue || r.DivisionId == input.DivisionId.Value) &&
			(!input.Status.HasValue || r.Status == input.Status.Value) &&
			(!input.Visibility.HasValue || r.Visibility == input.Visibility.Value));
	}

	private async Task EnsureTargetIsValidAsync(Guid standardId, Guid subjectId, Guid chapterId, Guid? divisionId)
	{
		if (!await TargetExistsAsync(standardId, subjectId, chapterId, divisionId))
		{
			throw new BusinessException("LearningResource:InvalidSyllabusTarget")
				.WithData("StandardId", standardId)
				.WithData("SubjectId", subjectId)
				.WithData("ChapterId", chapterId)
				.WithData("DivisionId", divisionId!);
		}
	}

	private async Task<bool> TargetExistsAsync(Guid standardId, Guid subjectId, Guid chapterId, Guid? divisionId)
	{
		var assignedSubject = await assignedSubjectRepository.FirstOrDefaultAsync(a =>
			a.StandardId == standardId &&
			a.SubjectId == subjectId &&
			a.Status != SubjectStatus.Archived);
		if (assignedSubject == null)
		{
			return false;
		}

		var chapterExists = await chapterRepository.AnyAsync(c =>
			c.Id == chapterId &&
			c.AssignedSubjectId == assignedSubject.Id);
		if (!chapterExists)
		{
			return false;
		}

		return !divisionId.HasValue || await divisionRepository.AnyAsync(d =>
			d.Id == divisionId.Value &&
			d.StandardId == standardId);
	}

	private static (string BlobName, string FileName, string ContentType, long Size) ResolveFile(
		CreateUpdateLearningResourceDto input,
		LearningResource? existing = null)
	{
		var blobName = input.BlobName ?? existing?.BlobName ?? input.LearningResource?.FileName;
		var fileName = input.FileName ?? existing?.FileName ?? input.LearningResource?.FileName;
		var contentType = input.ContentType ?? existing?.ContentType ?? input.LearningResource?.ContentType;
		var size = input.Size ?? existing?.Size ?? input.LearningResource?.Size ?? 0;

		return (
			blobName ?? string.Empty,
			fileName ?? string.Empty,
			contentType ?? "application/octet-stream",
			size);
	}

	private static void EnsureFileIsAllowed(string contentType, long size)
	{
		if (size <= 0)
		{
			throw new BusinessException("LearningResource:FileRequired");
		}

		const long maxSize = 50 * 1024 * 1024;
		if (size > maxSize)
		{
			throw new BusinessException("LearningResource:FileTooLarge")
				.WithData("MaxBytes", maxSize);
		}

		if (!AllowedContentTypes.Contains(contentType))
		{
			throw new BusinessException("LearningResource:FileTypeNotAllowed")
				.WithData("ContentType", contentType);
		}
	}

	private async Task<List<LearningResourceDto>> MapManyAsync(List<LearningResource> resources)
	{
		var standardIds = resources.Select(r => r.StandardId).Distinct().ToList();
		var subjectIds = resources.Select(r => r.SubjectId).Distinct().ToList();
		var chapterIds = resources.Select(r => r.ChapterId).Distinct().ToList();
		var divisionIds = resources.Where(r => r.DivisionId.HasValue).Select(r => r.DivisionId!.Value).Distinct().ToList();

		var standards = standardIds.Count == 0 ? [] : await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
		var subjects = subjectIds.Count == 0 ? [] : await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
		var chapters = chapterIds.Count == 0 ? [] : await chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id));
		var divisions = divisionIds.Count == 0 ? [] : await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));

		var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
		var subjectLookup = subjects.ToDictionary(s => s.Id, s => s.Name);
		var chapterLookup = chapters.ToDictionary(c => c.Id, c => c.ChapterName);
		var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);

		return resources.Select(r => new LearningResourceDto
		{
			Id = r.Id,
			StandardId = r.StandardId,
			StandardName = standardLookup.GetValueOrDefault(r.StandardId, string.Empty),
			SubjectId = r.SubjectId,
			SubjectName = subjectLookup.GetValueOrDefault(r.SubjectId, string.Empty),
			ChapterId = r.ChapterId,
			ChapterName = chapterLookup.GetValueOrDefault(r.ChapterId, string.Empty),
			DivisionId = r.DivisionId,
			DivisionName = r.DivisionId.HasValue ? divisionLookup.GetValueOrDefault(r.DivisionId.Value) : null,
			Title = r.Title,
			Topic = r.Topic,
			ResourceType = r.ResourceType,
			BlobName = r.BlobName,
			FileName = r.FileName,
			ContentType = r.ContentType,
			Size = r.Size,
			Status = r.Status,
			Visibility = r.Visibility,
			SubmittedAt = r.SubmittedAt,
			ReviewedAt = r.ReviewedAt,
			PublishedAt = r.PublishedAt,
			ReviewRemarks = r.ReviewRemarks,
			ArchiveReason = r.ArchiveReason,
			CreationTime = r.CreationTime,
			LastModificationTime = r.LastModificationTime
		}).ToList();
	}

	private async Task<LearningResourceDto> MapAsync(LearningResource resource)
	{
		return (await MapManyAsync([resource])).Single();
	}
}
