using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record AcademicSummaryCounts(int Homework = 0, int Assignments = 0);

[RemoteService(false)]
public sealed class AcademicSummaryBuilder(IRepository<StudentSubmissionEntity, Guid> submissions,
	LearningTaskNotificationRecordReader records, LearningTaskNotificationRoster rosters, IAsyncQueryableExecuter queries)
	: ITransientDependency
{
	private const int MaximumDivisionFacts = 5000;

	internal async Task<IReadOnlyDictionary<Guid, AcademicSummaryCounts>?> ReadAsync(Guid school, NotificationSourceScopeDto scope,
		LearningTaskDigestWindow week, CancellationToken token, Guid? selectedStudent = null)
	{
		var division = scope.DivisionId!.Value;
		var submitted = LearningTaskSubmissionFacts.InWindow(await submissions.GetQueryableAsync(), school, week.Start, week.End)
			.Where(x => x.DivisionId == division && x.AssignmentId.HasValue != x.HomeworkAssignmentId.HasValue);
		if (selectedStudent.HasValue) submitted = submitted.Where(x => x.StudentId == selectedStudent.Value);
		var facts = await queries.ToListAsync(submitted
			.Select(x => new { x.StudentId, x.AssignmentId, x.HomeworkAssignmentId, x.SubmissionDate })
			.Take(MaximumDivisionFacts + 1), token);
		if (facts.Count > MaximumDivisionFacts) return null;
		var totals = new Dictionary<Guid, AcademicSummaryCounts>();
		foreach (var item in facts.GroupBy(x => (x.AssignmentId, x.HomeworkAssignmentId)))
		{
			var assignment = item.Key.AssignmentId.HasValue;
			var record = assignment
				? await records.FindAssignmentAsync(school, item.Key.AssignmentId!.Value, week.End, token, includeClosed: true)
				: await records.FindHomeworkAsync(school, item.Key.HomeworkAssignmentId!.Value, week.End, token, includeClosed: true);
			if (record == null || record.Task.SubmissionType == SubmissionType.NoSubmissionRequired ||
				LearningTaskNotificationContent.Scope(record, division) != scope ||
				record.Schedules.SingleOrDefault(x => x.DivisionId == division) is not { } schedule) continue;

			var eligible = item.Where(fact => !schedule.PublishedAt.HasValue || fact.SubmissionDate >= schedule.PublishedAt)
				.Select(x => x.StudentId).Distinct().ToArray();
			if (eligible.Length == 0) continue;
			var learners = await rosters.FindAsync(school, record, [division], eligible, pendingOnly: false, token);
			if (learners == null) return null;
			foreach (var learner in learners)
			{
				var previous = totals.GetValueOrDefault(learner.StudentId) ?? new();
				totals[learner.StudentId] = assignment
					? previous with { Assignments = previous.Assignments + 1 }
					: previous with { Homework = previous.Homework + 1 };
			}
		}
		return totals;
	}
}
