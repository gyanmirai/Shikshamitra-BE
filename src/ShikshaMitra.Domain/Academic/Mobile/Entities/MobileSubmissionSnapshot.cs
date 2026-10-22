using System;
using System.Text.Json;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Mobile;

/// <summary>Append-only final response history. Files remain protected after later replacements.</summary>
public class MobileSubmissionSnapshot : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SubmissionId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid TaskId { get; private set; }
	public string Revision { get; private set; } = "";
	public string? TextAnswer { get; private set; }
	public string? TextAnswerHtml { get; private set; }
	public bool Acknowledged { get; private set; }
	public string? AttachmentsJson { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

	private MobileSubmissionSnapshot() { }
	public MobileSubmissionSnapshot(Guid id, StudentSubmissionEntity submission) : base(id)
	{
		TenantId = submission.TenantId; SubmissionId = submission.Id; StudentId = submission.StudentId;
		TaskId = (submission.AssignmentId ?? submission.HomeworkAssignmentId)!.Value;
		Revision = submission.ConcurrencyStamp; TextAnswer = submission.SubmittedContent;
		TextAnswerHtml = submission.SubmittedContentHtml; AttachmentsJson = submission.MobileAttachmentsJson;
		Acknowledged = submission.Acknowledged;
		if (string.IsNullOrEmpty(AttachmentsJson) && !string.IsNullOrWhiteSpace(submission.SubmittedDocumentUrl))
			AttachmentsJson = JsonSerializer.Serialize(new[] { new { Name = submission.OriginalFileName, BlobName = submission.SubmittedDocumentUrl } });
		CreatedAt = submission.MobileUpdatedAt ?? submission.SubmissionDate ?? DateTimeOffset.UtcNow;
	}
}
