using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Syllabus;

public class LearningResourceDto
{
	public Guid Id { get; set; }
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;
	public Guid ChapterId { get; set; }
	public string ChapterName { get; set; } = string.Empty;
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Topic { get; set; }
	public string ResourceType { get; set; } = string.Empty;
	public string BlobName { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
	public LearningResourceStatus Status { get; set; }
	public LearningResourceVisibility Visibility { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public DateTime? PublishedAt { get; set; }
	public string? ReviewRemarks { get; set; }
	public string? ArchiveReason { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
}

public class GetLearningResourcesInput
{
	public Guid? StandardId { get; set; }
	public Guid? SubjectId { get; set; }
	public Guid? ChapterId { get; set; }
	public Guid? DivisionId { get; set; }
	public LearningResourceStatus? Status { get; set; }
	public LearningResourceVisibility? Visibility { get; set; }
	public string? Search { get; set; }
}

public class LearningResourceReviewDto
{
	public string? Remarks { get; set; }
}

public class LearningResourceArchiveDto
{
	public string Reason { get; set; } = string.Empty;
}

public class LearningResourceHealthDto
{
	public int TotalCount { get; set; }
	public int DraftCount { get; set; }
	public int SubmittedCount { get; set; }
	public int PublishedCount { get; set; }
	public int ArchivedCount { get; set; }
	public int BrokenReferenceCount { get; set; }
	public List<string> Warnings { get; set; } = [];
}

public interface ILearningResourceAppService : IApplicationService
{
	Task<LearningResourceDto> CreateAsync(CreateUpdateLearningResourceDto input);
	Task<List<LearningResourceDto>> GetListAsync(GetLearningResourcesInput input);
	Task<LearningResourceDto> GetAsync(Guid id);
	Task<LearningResourceDto> UpdateAsync(Guid id, CreateUpdateLearningResourceDto input);
	Task SubmitAsync(Guid id);
	Task PublishAsync(Guid id, LearningResourceReviewDto input);
	Task RejectAsync(Guid id, LearningResourceReviewDto input);
	Task ArchiveAsync(Guid id, LearningResourceArchiveDto input);
	Task RestoreAsync(Guid id);
	Task<LearningResourceHealthDto> GetHealthAsync(GetLearningResourcesInput input);
}
