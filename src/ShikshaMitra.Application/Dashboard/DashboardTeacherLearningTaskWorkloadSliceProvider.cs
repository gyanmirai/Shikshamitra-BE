using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardTeacherLearningTaskWorkloadSliceProvider(
	DashboardTeacherScopeResolver teacherScopeResolver,
	IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
	IRepository<AssignmentEntity, Guid> assignmentRepository,
	IRepository<StudentSubmissionEntity, Guid> submissionRepository,
	IUserScopeAccessService userScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker,
	ICurrentUser currentUser)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	private const string TeacherHomeworkWidgetId = "teacher-homework";

	private static readonly SubmissionStatus[] PendingReviewStatuses =
	[
		SubmissionStatus.Submitted,
		SubmissionStatus.Late,
		SubmissionStatus.NeedsReview
	];

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		if (context.Summary.DashboardTemplate != Teacher)
		{
			return;
		}

		try
		{
			if (!await HasAnyPermissionAsync(WidgetDefinitions[TeacherHomeworkWidgetId].RequiredPermissions))
			{
				return;
			}

			if (!currentUser.Id.HasValue)
			{
				return;
			}

			var scope = await teacherScopeResolver.ResolveAsync();
			if (!scope.IsResolved || !scope.EmployeeId.HasValue)
			{
				return;
			}

			var pendingReviews =
				await CountPendingHomeworkReviewsAsync(currentUser.Id.Value) +
				await CountPendingAssignmentReviewsAsync(currentUser.Id.Value);

			UpdateWidget(
				context.Summary.KpiCards,
				TeacherHomeworkWidgetId,
				pendingReviews,
				pendingReviews.ToString(CultureInfo.InvariantCulture),
				pendingReviews == 0
					? "No creator-owned submissions pending review"
					: BuildCountSubtitle(
						pendingReviews,
						"creator-owned submission pending review",
						"creator-owned submissions pending review"),
				CountStatus(pendingReviews),
				title: "Pending Learning Reviews");
		}
		catch
		{
			SetZoneError(context.Summary, Kpis, "Teacher learning-task workload could not be loaded.");
		}
	}

	private async Task<int> CountPendingHomeworkReviewsAsync(Guid creatorUserId)
	{
		var query = await homeworkRepository.GetQueryableAsync();
		var scopedQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
		var homeworkIds = await asyncExecuter.ToListAsync(scopedQuery
			.Where(homework => homework.CreatorId == creatorUserId)
			.Select(homework => homework.Id));

		if (homeworkIds.Count == 0)
		{
			return 0;
		}

		return await CountPendingReviewsAsync(homeworkIds, isHomework: true);
	}

	private async Task<int> CountPendingAssignmentReviewsAsync(Guid creatorUserId)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var scopedQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
		var assignmentIds = await asyncExecuter.ToListAsync(scopedQuery
			.Where(assignment => assignment.CreatorId == creatorUserId)
			.Select(assignment => assignment.Id));

		if (assignmentIds.Count == 0)
		{
			return 0;
		}

		return await CountPendingReviewsAsync(assignmentIds, isHomework: false);
	}

	private async Task<int> CountPendingReviewsAsync(IReadOnlyCollection<Guid> learningTaskIds, bool isHomework)
	{
		var submissions = await submissionRepository.GetQueryableAsync();
		var pendingSubmissions = submissions.Where(submission =>
			submission.ReviewedBy == null &&
			submission.ReviewedAt == null &&
			submission.GradedBy == null &&
			submission.GradedDate == null &&
			PendingReviewStatuses.Contains(submission.Status));

		return isHomework
			? await asyncExecuter.CountAsync(pendingSubmissions
				.Where(submission =>
					submission.HomeworkAssignmentId.HasValue &&
					learningTaskIds.Contains(submission.HomeworkAssignmentId.Value)))
			: await asyncExecuter.CountAsync(pendingSubmissions
				.Where(submission =>
					submission.AssignmentId.HasValue &&
					learningTaskIds.Contains(submission.AssignmentId.Value)));
	}

	private async Task<bool> HasAnyPermissionAsync(string[] permissionNames)
	{
		foreach (var permissionName in permissionNames)
		{
			if (await permissionChecker.IsGrantedAsync(permissionName))
			{
				return true;
			}
		}

		return false;
	}
}
