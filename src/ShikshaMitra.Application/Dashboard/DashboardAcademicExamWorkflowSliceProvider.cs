using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardAcademicExamWorkflowSliceProvider(
	IRepository<ExamMarksWorkflowState, Guid> workflowStateRepository,
	IRepository<ExamResultHeader, Guid> resultHeaderRepository,
	IRepository<ExamResultDetail, Guid> resultDetailRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IUserScopeAccessService userScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	private const string PrincipalAcademicDashboardWidgetId = "principal-academic-dashboard";

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;
		if (summary.DashboardTemplate != PrincipalAdmin)
		{
			return;
		}

		if (!await HasAnyPermissionAsync(WidgetDefinitions[PrincipalAcademicDashboardWidgetId].RequiredPermissions))
		{
			return;
		}

		try
		{
			var scopedDivisionRows = await BuildScopedDivisionRowsAsync(
				await userScopeAccessService.GetCurrentUserScopeAsync(),
				summary.AcademicYearId);
			var snapshot = new ExamSnapshot(
				await BuildExamWorkflowSnapshotAsync(scopedDivisionRows),
				await BuildResultOutcomeSnapshotAsync(scopedDivisionRows));
			ApplyPrincipalAcademicDashboard(summary, snapshot);
		}
		catch
		{
			SetZoneError(summary, Modules, "Academic exam workflow snapshot could not be loaded.");
		}
	}

	private async Task<ExamWorkflowSnapshot> BuildExamWorkflowSnapshotAsync(ScopedDivisionRow[] scopedDivisionRows)
	{
		if (!await HasAnyPermissionAsync([ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View]))
		{
			return ExamWorkflowSnapshot.Unavailable;
		}

		if (scopedDivisionRows.Length == 0)
		{
			return ExamWorkflowSnapshot.Empty;
		}

		var scopedDivisionIds = scopedDivisionRows
			.Select(row => row.DivisionId)
			.Distinct()
			.ToList();

		var workflows = await workflowStateRepository.GetQueryableAsync();
		var candidateRows = await asyncExecuter.ToListAsync(
			workflows
				.Where(workflow =>
					scopedDivisionIds.Contains(workflow.DivisionId) &&
					workflow.Status != ExamMarksWorkflowStatus.Published)
				.Select(workflow => workflow.DivisionId));

		var scopedDivisionIdSet = scopedDivisionRows
			.Select(row => row.DivisionId)
			.ToHashSet();
		var pending = candidateRows.Count(scopedDivisionIdSet.Contains);
		return new ExamWorkflowSnapshot(true, pending);
	}

	private async Task<ResultOutcomeSnapshot> BuildResultOutcomeSnapshotAsync(ScopedDivisionRow[] scopedDivisionRows)
	{
		if (!await HasAnyPermissionAsync([ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View]))
		{
			return ResultOutcomeSnapshot.Unavailable;
		}

		if (scopedDivisionRows.Length == 0)
		{
			return ResultOutcomeSnapshot.Empty;
		}

		var scopedDivisionIds = scopedDivisionRows
			.Select(row => row.DivisionId)
			.Distinct()
			.ToList();
		var headers = await resultHeaderRepository.GetQueryableAsync();
		var publishedHeaderRows = await asyncExecuter.ToListAsync(headers
			.Where(header =>
				scopedDivisionIds.Contains(header.DivisionId) &&
				(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen))
			.Select(header => new ResultHeaderRow
			{
				Id = header.Id,
				DivisionId = header.DivisionId
			}));

		if (publishedHeaderRows.Count == 0)
		{
			return ResultOutcomeSnapshot.Empty;
		}

		var scopedDivisionIdSet = scopedDivisionRows
			.Select(row => row.DivisionId)
			.ToHashSet();
		var headerIds = publishedHeaderRows
			.Where(header => scopedDivisionIdSet.Contains(header.DivisionId))
			.Select(header => header.Id)
			.Distinct()
			.ToList();
		if (headerIds.Count == 0)
		{
			return ResultOutcomeSnapshot.Empty;
		}

		var detailRows = await asyncExecuter.ToListAsync((await resultDetailRepository.GetQueryableAsync())
			.Where(detail => headerIds.Contains(detail.ResultHeaderId))
			.Select(detail => new ResultDetailRow
			{
				Status = detail.ResultStatus,
				Percentage = detail.Percentage
			}));
		var appeared = detailRows.Count(row => row.Status != ExamStudentResultStatus.Absent);
		if (appeared == 0)
		{
			return new ResultOutcomeSnapshot(true, headerIds.Count, 0, 0, 0, 0);
		}

		var passed = detailRows.Count(row =>
			row.Status is ExamStudentResultStatus.Pass or ExamStudentResultStatus.Compartment);
		var failed = detailRows.Count(row => row.Status == ExamStudentResultStatus.Fail);
		var averagePercentage = detailRows
			.Where(row => row.Status != ExamStudentResultStatus.Absent)
			.Average(row => row.Percentage);
		var passPercentage = (int)Math.Round(passed / (double)appeared * 100, MidpointRounding.AwayFromZero);

		return new ResultOutcomeSnapshot(
			true,
			headerIds.Count,
			appeared,
			passed,
			failed,
			passPercentage,
			(int)Math.Round(averagePercentage, MidpointRounding.AwayFromZero));
	}

	private void ApplyPrincipalAcademicDashboard(
		DashboardSummaryDto summary,
		ExamSnapshot snapshot)
	{
		var widget = summary.ModuleSnapshots.FirstOrDefault(x => x.Id == PrincipalAcademicDashboardWidgetId);
		var existingSubtitle = string.IsNullOrWhiteSpace(widget?.Subtitle)
			? "Open Academic Dashboard for class performance and syllabus risk"
			: widget!.Subtitle!;
		var subtitleParts = new List<string>();
		if (snapshot.Workflow.IsAvailable)
		{
			subtitleParts.Add(snapshot.Workflow.PendingWorkflowCount == 0
				? "No scoped exam marks workflow pending."
				: string.Format(
					CultureInfo.InvariantCulture,
					"{0} scoped exam marks {1} need submit, verify, or publish.",
					snapshot.Workflow.PendingWorkflowCount,
					snapshot.Workflow.PendingWorkflowCount == 1 ? "workflow" : "workflows"));
		}

		if (snapshot.ResultOutcome.IsAvailable)
		{
			subtitleParts.Add(snapshot.ResultOutcome.PublishedResultCount == 0
				? "No scoped published result outcome found."
				: string.Format(
					CultureInfo.InvariantCulture,
					"Published result pass rate {0}% across {1} scoped {2}; {3} students need result follow-up.",
					snapshot.ResultOutcome.PassPercentage,
					snapshot.ResultOutcome.PublishedResultCount,
					snapshot.ResultOutcome.PublishedResultCount == 1 ? "result" : "results",
					snapshot.ResultOutcome.FailedCount));
		}

		subtitleParts.Add(existingSubtitle);
		var health = (snapshot.Workflow.IsAvailable && snapshot.Workflow.PendingWorkflowCount > 0) ||
		             (snapshot.ResultOutcome.IsAvailable &&
		              snapshot.ResultOutcome.PublishedResultCount > 0 &&
		              snapshot.ResultOutcome.PassPercentage < 75)
			? "attention"
			: "good";

		UpdateWidget(
			summary.ModuleSnapshots,
			PrincipalAcademicDashboardWidgetId,
			snapshot.Workflow.PendingWorkflowCount,
			null,
			string.Join(" ", subtitleParts),
			health == "good" ? "success" : "warning",
			health: health);
	}

	private async Task<ScopedDivisionRow[]> BuildScopedDivisionRowsAsync(
		UserScopeContext scope,
		string? academicYearId)
	{
		if (!scope.HasAnyAccess)
		{
			return [];
		}

		var divisions = await divisionRepository.GetQueryableAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var stages = await stageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();

		var query =
			from division in divisions
			join standard in standards on division.StandardId equals standard.Id
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			select new ScopedAcademicOrganizationRow
			{
				AcademicYearId = board.AcademicYearId,
				BoardId = board.Id,
				MediumId = medium.Id,
				MediumStageId = stage.Id,
				StandardId = standard.Id,
				DivisionId = division.Id
			};

		var organizationRows = await asyncExecuter.ToListAsync(query);
		IEnumerable<ScopedAcademicOrganizationRow> scopedRows = organizationRows;
		if (Guid.TryParse(academicYearId, out var requestedAcademicYearId))
		{
			scopedRows = scopedRows.Where(row => row.AcademicYearId == requestedAcademicYearId);
		}

		if (!scope.HasTenantWideScope())
		{
			scopedRows = scopedRows.Where(row =>
				scope.AcademicYearIds.Contains(row.AcademicYearId) ||
				scope.WildcardBoardAcademicYearIds.Contains(row.AcademicYearId) ||
				scope.BoardIds.Contains(row.BoardId) ||
				scope.WildcardMediumBoardIds.Contains(row.BoardId) ||
				scope.MediumIds.Contains(row.MediumId) ||
				scope.WildcardMediumStageMediumIds.Contains(row.MediumId) ||
				scope.MediumStageIds.Contains(row.MediumStageId) ||
				scope.WildcardStandardMediumStageIds.Contains(row.MediumStageId) ||
				scope.StandardIds.Contains(row.StandardId) ||
				scope.WildcardDivisionStandardIds.Contains(row.StandardId) ||
				scope.DivisionIds.Contains(row.DivisionId));
		}

		return scopedRows
			.Select(row => new ScopedDivisionRow
			{
				StandardId = row.StandardId,
				DivisionId = row.DivisionId
			})
			.Distinct()
			.ToArray();
	}

	private async Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissionNames)
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

	private sealed record ExamSnapshot(
		ExamWorkflowSnapshot Workflow,
		ResultOutcomeSnapshot ResultOutcome);

	private sealed record ExamWorkflowSnapshot(bool IsAvailable, int PendingWorkflowCount)
	{
		public static ExamWorkflowSnapshot Unavailable { get; } = new(false, 0);
		public static ExamWorkflowSnapshot Empty { get; } = new(true, 0);
	}

	private sealed record ResultOutcomeSnapshot(
		bool IsAvailable,
		int PublishedResultCount,
		int AppearedCount,
		int PassedCount,
		int FailedCount,
		int PassPercentage,
		int AveragePercentage = 0)
	{
		public static ResultOutcomeSnapshot Unavailable { get; } = new(false, 0, 0, 0, 0, 0);
		public static ResultOutcomeSnapshot Empty { get; } = new(true, 0, 0, 0, 0, 0);
	}

	private sealed class ResultHeaderRow
	{
		public Guid Id { get; init; }
		public Guid DivisionId { get; init; }
	}

	private sealed class ResultDetailRow
	{
		public ExamStudentResultStatus Status { get; init; }
		public double Percentage { get; init; }
	}

	private sealed class ScopedAcademicOrganizationRow
	{
		public Guid AcademicYearId { get; init; }
		public Guid BoardId { get; init; }
		public Guid MediumId { get; init; }
		public Guid MediumStageId { get; init; }
		public Guid StandardId { get; init; }
		public Guid DivisionId { get; init; }
	}

	private sealed class ScopedDivisionRow : IEquatable<ScopedDivisionRow>
	{
		public Guid StandardId { get; init; }
		public Guid DivisionId { get; init; }

		public bool Equals(ScopedDivisionRow? other) =>
			other is not null &&
			StandardId == other.StandardId &&
			DivisionId == other.DivisionId;

		public override bool Equals(object? obj) => Equals(obj as ScopedDivisionRow);

		public override int GetHashCode() => HashCode.Combine(StandardId, DivisionId);
	}
}
