using System;
using System.Linq.Expressions;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;

namespace ShikshaMitra.Academic.Mobile;

public static class MobileSubmissionRules
{
	// Also consumed as an EF expression by the count reader: keep command
	// eligibility and badge eligibility identical without hydrating submissions.
	public static Expression<Func<StudentSubmissionEntity, bool>> BlocksFirstSubmission { get; } = submission =>
		submission.SubmissionDate.HasValue || submission.ReviewedAt.HasValue ||
		(submission.Status != SubmissionStatus.Draft && submission.Status != SubmissionStatus.NotSubmitted &&
		 submission.Status != SubmissionStatus.NeedsReview && submission.Status != SubmissionStatus.Submitted &&
		 submission.Status != SubmissionStatus.Late);
	private static readonly Func<StudentSubmissionEntity, bool> BlocksFirstSubmissionCompiled = BlocksFirstSubmission.Compile();

	public static bool CanSubmit(StudentSubmissionEntity? submission, bool closed) =>
		!closed && (submission == null || !BlocksFirstSubmissionCompiled(submission));

	public static bool CanEdit(StudentSubmissionEntity? submission, DateTime dueDate, DateTime now, bool closed) =>
		now <= dueDate && IsOpen(submission, closed);

	private static bool IsOpen(StudentSubmissionEntity? submission, bool closed) =>
		!closed && submission?.ReviewedAt == null && (submission == null || submission.Status is SubmissionStatus.Draft or SubmissionStatus.NotSubmitted ||
			(submission.Status == SubmissionStatus.NeedsReview && submission.SubmissionDate == null && submission.ReviewedAt == null) ||
			(submission.Status is SubmissionStatus.Submitted or SubmissionStatus.Late && !submission.ReviewedAt.HasValue));

	public static void Validate(StudentSubmissionEntity? submission, DateTime dueDate, DateTime now, bool closed,
		SubmissionType type, bool draft, bool replace, string? text, int attachments, bool acknowledged = false)
	{
		if (draft && submission?.SubmissionDate != null) throw new BusinessException(MobileSubmissionErrorCodes.DraftAfterSubmission);
		if (submission?.SubmissionDate != null ? !CanEdit(submission, dueDate, now, closed) : !CanSubmit(submission, closed))
			throw new BusinessException(MobileSubmissionErrorCodes.SubmissionLocked);
		if (!draft && replace != (submission?.SubmissionDate != null)) throw new BusinessException(MobileSubmissionErrorCodes.SubmissionConflict);
		if (attachments is < 0 or > 5 || text?.Length > 100000) throw new BusinessException(MobileSubmissionErrorCodes.SubmissionTooLarge);
		if (!draft && (type is SubmissionType.InPersonSubmission or SubmissionType.NoSubmissionRequired) && !acknowledged)
			throw new BusinessException(MobileSubmissionErrorCodes.AcknowledgmentRequired);
		if (!draft && ((type == SubmissionType.OnlineText && string.IsNullOrWhiteSpace(text)) ||
			(type == SubmissionType.FileUpload && attachments == 0))) throw new BusinessException(MobileSubmissionErrorCodes.SubmissionRequired);
	}
}
