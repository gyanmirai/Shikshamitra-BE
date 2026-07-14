using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Syllabus;

public class LearningResource : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid SubjectId { get; private set; }
	public Guid ChapterId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Topic { get; private set; }
	public string ResourceType { get; private set; } = "Document";
	public string BlobName { get; private set; } = string.Empty;
	public string FileName { get; private set; } = string.Empty;
	public string ContentType { get; private set; } = "application/octet-stream";
	public long Size { get; private set; }
	public LearningResourceStatus Status { get; private set; }
	public LearningResourceVisibility Visibility { get; private set; }
	public Guid? SubmittedBy { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? ReviewedBy { get; private set; }
	public DateTime? ReviewedAt { get; private set; }
	public string? ReviewRemarks { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public string? ArchiveReason { get; private set; }

	private LearningResource()
	{
	}

	public LearningResource(
		Guid id,
		Guid? tenantId,
		Guid standardId,
		Guid subjectId,
		Guid chapterId,
		Guid? divisionId,
		string title,
		string? topic,
		string resourceType,
		string blobName,
		string fileName,
		string contentType,
		long size,
		LearningResourceVisibility visibility)
		: base(id)
	{
		TenantId = tenantId;
		StandardId = standardId;
		SubjectId = subjectId;
		ChapterId = chapterId;
		DivisionId = divisionId;
		SetDetails(title, topic, resourceType, blobName, fileName, contentType, size, visibility);
		Status = LearningResourceStatus.Draft;
	}

	public void Update(
		string title,
		string? topic,
		string resourceType,
		string blobName,
		string fileName,
		string contentType,
		long size,
		LearningResourceVisibility visibility)
	{
		if (Status is LearningResourceStatus.Published or LearningResourceStatus.Archived)
		{
			throw new BusinessException("LearningResource:CannotEditPublishedOrArchived")
				.WithData("Status", Status);
		}

		SetDetails(title, topic, resourceType, blobName, fileName, contentType, size, visibility);
	}

	public void Submit(Guid? userId)
	{
		if (Status != LearningResourceStatus.Draft && Status != LearningResourceStatus.Rejected)
		{
			throw new BusinessException("LearningResource:InvalidSubmitStatus")
				.WithData("Status", Status);
		}

		Status = LearningResourceStatus.Submitted;
		SubmittedBy = userId;
		SubmittedAt = DateTime.UtcNow;
		ReviewRemarks = null;
	}

	public void Publish(Guid? reviewerId, string? remarks)
	{
		if (Status != LearningResourceStatus.Submitted && Status != LearningResourceStatus.Draft)
		{
			throw new BusinessException("LearningResource:InvalidPublishStatus")
				.WithData("Status", Status);
		}

		Status = LearningResourceStatus.Published;
		ReviewedBy = reviewerId;
		ReviewedAt = DateTime.UtcNow;
		PublishedAt = DateTime.UtcNow;
		ReviewRemarks = NormalizeNullable(remarks, 1024);
		ArchiveReason = null;
	}

	public void Reject(Guid? reviewerId, string remarks)
	{
		if (Status != LearningResourceStatus.Submitted)
		{
			throw new BusinessException("LearningResource:InvalidRejectStatus")
				.WithData("Status", Status);
		}

		if (string.IsNullOrWhiteSpace(remarks))
		{
			throw new BusinessException("LearningResource:RejectReasonRequired");
		}

		Status = LearningResourceStatus.Rejected;
		ReviewedBy = reviewerId;
		ReviewedAt = DateTime.UtcNow;
		ReviewRemarks = Normalize(remarks, 1024);
	}

	public void Archive(string reason)
	{
		if (Status == LearningResourceStatus.Archived)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new BusinessException("LearningResource:ArchiveReasonRequired");
		}

		Status = LearningResourceStatus.Archived;
		ArchiveReason = Normalize(reason, 1024);
	}

	public void Restore()
	{
		if (Status != LearningResourceStatus.Archived)
		{
			throw new BusinessException("LearningResource:OnlyArchivedCanBeRestored")
				.WithData("Status", Status);
		}

		Status = LearningResourceStatus.Draft;
		ArchiveReason = null;
		PublishedAt = null;
	}

	private void SetDetails(
		string title,
		string? topic,
		string resourceType,
		string blobName,
		string fileName,
		string contentType,
		long size,
		LearningResourceVisibility visibility)
	{
		Title = Normalize(title, 256);
		Topic = NormalizeNullable(topic, 256);
		ResourceType = Normalize(resourceType, 64);
		BlobName = Normalize(blobName, 512);
		FileName = Normalize(fileName, 256);
		ContentType = Normalize(contentType, 128);
		Size = size;
		Visibility = visibility;
	}

	private static string Normalize(string value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new BusinessException("LearningResource:RequiredFieldMissing");
		}

		var normalized = value.Trim();
		return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
	}

	private static string? NormalizeNullable(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var normalized = value.Trim();
		return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
	}
}
