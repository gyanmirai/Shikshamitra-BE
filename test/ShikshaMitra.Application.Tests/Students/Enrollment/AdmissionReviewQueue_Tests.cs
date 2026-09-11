using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_queue_filters_assignee_and_overdue_before_paging_without_writing_on_reads() =>
		RunCertificationScenarioAsync(async () =>
		{
			var assigned = await CreateReviewUserAsync();
			var overdue = await CreateReviewFixtureAsync(ReviewPolicyJson(dueInDays: 3));
			var future = await CreateReviewFixtureAsync();
			var unassigned = await CreateReviewFixtureAsync();
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var first = await service.GetAsync(overdue.ApplicationId, overdue.StageId);
			first.DueAt.ShouldNotBeNull();
			first.IsOverdue.ShouldBeFalse();
			first.LatestRecord.ShouldBeNull();
			(await service.GetHistoryAsync(overdue.ApplicationId, overdue.StageId)).ShouldBeEmpty();
			foreach (var fixture in new[] { overdue, future })
			{
				var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
				await service.AssignAsync(fixture.ApplicationId, fixture.StageId, new()
				{
					ConcurrencyStamp = state.ConcurrencyStamp, AssignedReviewerId = assigned,
					DueAt = fixture == overdue ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(2),
					Reason = "Assigned with the recorded review deadline."
				});
			}
			var stamp = (await service.GetAsync(overdue.ApplicationId, overdue.StageId)).ConcurrencyStamp;
			using (_principalAccessor.Change(EligibilityPrincipal(assigned, "assigned-reviewer")))
			{
				var queue = await service.GetQueueAsync(new() { AssignedToMe = true, OverdueOnly = true, MaxResultCount = 1 });
				queue.TotalCount.ShouldBe(1);
				queue.Items.Single().ApplicationId.ShouldBe(overdue.ApplicationId);
				queue.Items.Single().IsOverdue.ShouldBeTrue();
				queue.Items.Single().AssignedReviewerId.ShouldBe(assigned);
				var paged = await service.GetQueueAsync(new() { AssignedToMe = true, MaxResultCount = 1, SkipCount = 1 });
				paged.TotalCount.ShouldBe(2);
				paged.Items.Count.ShouldBe(1);
				paged.Items.Single().ApplicationId.ShouldNotBe(unassigned.ApplicationId);
				(await service.GetAsync(overdue.ApplicationId, overdue.StageId)).IsOverdue.ShouldBeTrue();
			}
			(await service.GetAsync(overdue.ApplicationId, overdue.StageId)).ConcurrencyStamp.ShouldBe(stamp);
			(await service.GetHistoryAsync(overdue.ApplicationId, overdue.StageId)).Count.ShouldBe(1);
			(await service.GetHistoryAsync(unassigned.ApplicationId, unassigned.StageId)).ShouldBeEmpty();
		});
}
