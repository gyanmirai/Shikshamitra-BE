using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public partial class HomeworkAppService
{
    private async Task<Dictionary<Guid, LearningTaskSubmissionSummary>> GetHomeworkSubmissionSummariesAsync(IEnumerable<Guid> homeworkIds)
    {
        var ids = homeworkIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var query = await studentSubmissionRepository.GetQueryableAsync();
        var counts = await asyncExecuter.ToListAsync(query.Where(submission => submission.HomeworkAssignmentId.HasValue &&
                ids.Contains(submission.HomeworkAssignmentId.Value))
            .GroupBy(submission => submission.HomeworkAssignmentId!.Value)
            .Select(group => new
            {
                Id = group.Key,
                Submitted = group.Count(submission => submission.Status == SubmissionStatus.Submitted ||
                    submission.Status == SubmissionStatus.Late || submission.Status == SubmissionStatus.Reviewed),
                Late = group.Count(submission => submission.Status == SubmissionStatus.Late),
                Checked = group.Count(submission => submission.Status == SubmissionStatus.Reviewed),
                EvaluationPending = group.Count(submission => submission.Status == SubmissionStatus.Submitted ||
                    submission.Status == SubmissionStatus.Late || submission.Status == SubmissionStatus.NeedsReview)
            }));
        return counts.ToDictionary(count => count.Id, count => new LearningTaskSubmissionSummary(
            0, count.Submitted, 0, count.Late, count.Checked, count.EvaluationPending));
    }
}
