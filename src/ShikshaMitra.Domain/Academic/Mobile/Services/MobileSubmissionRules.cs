using System;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;

namespace ShikshaMitra.Academic.Mobile;

public static class MobileSubmissionRules
{
	public static bool CanEdit(StudentSubmissionEntity? submission, DateTime dueDate, DateTime now, bool closed) =>
		!closed && now <= dueDate && (submission == null || submission.Status is SubmissionStatus.Draft or SubmissionStatus.NotSubmitted ||
			(submission.Status == SubmissionStatus.NeedsReview && submission.SubmissionDate == null && submission.ReviewedAt == null) ||
			(submission.Status is SubmissionStatus.Submitted or SubmissionStatus.Late && !submission.ReviewedAt.HasValue));

	public static void Validate(StudentSubmissionEntity? submission, DateTime dueDate, DateTime now, bool closed,
		SubmissionType type, bool draft, bool replace, string? text, int attachments)
	{
		if (!CanEdit(submission, dueDate, now, closed)) throw new BusinessException("MobileAcademics:SubmissionLocked");
		if (type is SubmissionType.InPersonSubmission or SubmissionType.NoSubmissionRequired)
			throw new BusinessException("MobileAcademics:SubmissionNotSupported");
		if (!draft && replace != (submission?.SubmissionDate != null)) throw new BusinessException("MobileAcademics:SubmissionConflict");
		if (attachments is < 0 or > 5 || text?.Length > 100000) throw new BusinessException("MobileAcademics:SubmissionTooLarge");
		if (!draft && ((type == SubmissionType.OnlineText && string.IsNullOrWhiteSpace(text)) ||
			(type == SubmissionType.FileUpload && attachments == 0))) throw new BusinessException("MobileAcademics:SubmissionRequired");
	}
}
