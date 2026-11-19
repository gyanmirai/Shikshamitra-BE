using System;
using System.Linq;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Notifications;

internal static class LearningTaskSubmissionFacts
{
	internal static IQueryable<StudentSubmissionEntity> InWindow(IQueryable<StudentSubmissionEntity> submissions, Guid tenantId,
		DateTimeOffset start, DateTimeOffset end) =>
		submissions.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SubmissionDate.HasValue &&
			x.SubmissionDate.Value.CompareTo(start) >= 0 && x.SubmissionDate.Value.CompareTo(end) < 0 &&
			(x.Status == SubmissionStatus.Submitted || x.Status == SubmissionStatus.Late ||
			 x.Status == SubmissionStatus.Reviewed || x.Status == SubmissionStatus.NeedsReview));
}
