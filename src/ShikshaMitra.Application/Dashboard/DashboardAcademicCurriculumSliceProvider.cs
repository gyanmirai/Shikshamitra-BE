using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
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

public class DashboardAcademicCurriculumSliceProvider(
	IRepository<AssignedSubject, Guid> subjectAssignmentRepository,
	IRepository<AssignedSubjectChapter, Guid> chapterRepository,
	IRepository<ChapterProgress, Guid> progressRepository,
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
	private const string LeadershipAcademicHealthWidgetId = "leadership-academic-health";
	private const string PrincipalCurriculumWidgetId = "principal-curriculum";
	private const string PrincipalWeakClassesWidgetId = "principal-weak-classes";
	private const string PrincipalAcademicDashboardWidgetId = "principal-academic-dashboard";

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;

		if (summary.DashboardTemplate is not (Leadership or PrincipalAdmin))
		{
			return;
		}

		if (!await HasAnyRequiredAcademicPermissionAsync(summary.DashboardTemplate))
		{
			return;
		}

		try
		{
			var snapshot = await BuildCurriculumSnapshotAsync(
				summary.AcademicYearId,
				ParseNormalizedDate(context.DateRange.To));

			await ApplyLeadershipAcademicHealthAsync(summary, snapshot);
			await ApplyPrincipalCurriculumAsync(summary, snapshot);
			await ApplyPrincipalCurriculumRiskAsync(summary, snapshot);
			await ApplyPrincipalAcademicDashboardAsync(summary, snapshot);
		}
		catch
		{
			SetZoneError(summary, Kpis, "Academic curriculum snapshot could not be loaded.");
		}
	}

	private async Task<CurriculumSnapshot> BuildCurriculumSnapshotAsync(
		string? academicYearId,
		DateTime effectiveDate)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var scopedDivisionRows = await BuildScopedDivisionRowsAsync(scope, academicYearId);
		if (scopedDivisionRows.Length == 0)
		{
			return CurriculumSnapshot.Empty;
		}

		var scopedStandardIds = scopedDivisionRows
			.Select(row => row.StandardId)
			.Distinct()
			.ToList();
		var scopedDivisionIds = scopedDivisionRows
			.Select(row => row.DivisionId)
			.Distinct()
			.ToList();

		var assignments = await subjectAssignmentRepository.GetQueryableAsync();
		var chapters = await chapterRepository.GetQueryableAsync();
		var scopedChapterRows = await asyncExecuter.ToListAsync(
			from assignment in assignments
			join chapter in chapters on assignment.Id equals chapter.AssignedSubjectId
			where assignment.Status == SubjectStatus.Published &&
			      scopedStandardIds.Contains(assignment.StandardId)
			select new CurriculumChapterRow
			{
				StandardId = assignment.StandardId,
				ChapterId = chapter.Id,
				PlannedEndDate = chapter.ChapterEndTime
			});
		var scopedChapterDivisionRows = scopedChapterRows
			.SelectMany(chapter => scopedDivisionRows
				.Where(division => division.StandardId == chapter.StandardId)
				.Select(division => new CurriculumChapterDivisionRow
				{
					ChapterId = chapter.ChapterId,
					DivisionId = division.DivisionId,
					PlannedEndDate = chapter.PlannedEndDate
				}))
			.Distinct()
			.ToList();

		if (scopedChapterDivisionRows.Count == 0)
		{
			return CurriculumSnapshot.Empty;
		}

		var chapterIds = scopedChapterDivisionRows.Select(row => row.ChapterId).Distinct().ToList();
		var progressRows = await asyncExecuter.ToListAsync((await progressRepository.GetQueryableAsync())
			.Where(progress => chapterIds.Contains(progress.ChapterId) &&
			                   scopedDivisionIds.Contains(progress.DivisionId))
			.Select(progress => new CurriculumProgressRow
			{
				ChapterId = progress.ChapterId,
				DivisionId = progress.DivisionId,
				Progress = progress.Progress,
				CreationTime = progress.CreationTime
			}));
		var progressByChapterDivision = progressRows
			.GroupBy(row => (row.ChapterId, row.DivisionId))
			.ToDictionary(
				group => group.Key,
				group => group.OrderByDescending(row => row.CreationTime).First().Progress);

		var progressSum = 0;
		var completed = 0;
		var overdue = 0;
		foreach (var row in scopedChapterDivisionRows)
		{
			progressByChapterDivision.TryGetValue((row.ChapterId, row.DivisionId), out var progress);
			progressSum += progress;
			if (progress == 100)
			{
				completed++;
			}

			if (row.PlannedEndDate.Date < effectiveDate.Date && progress < 100)
			{
				overdue++;
			}
		}

		var total = scopedChapterDivisionRows.Count;
		var percentage = (int)Math.Round(progressSum / (double)total, MidpointRounding.AwayFromZero);
		return new CurriculumSnapshot(total, completed, overdue, percentage);
	}

	private async Task ApplyLeadershipAcademicHealthAsync(
		DashboardSummaryDto summary,
		CurriculumSnapshot snapshot)
	{
		if (summary.DashboardTemplate != Leadership ||
		    !await HasAnyPermissionAsync(WidgetDefinitions[LeadershipAcademicHealthWidgetId].RequiredPermissions))
		{
			return;
		}

		var hasData = snapshot.TotalDivisionChapters > 0;
		UpdateWidget(
			summary.ModuleSnapshots,
			LeadershipAcademicHealthWidgetId,
			snapshot.OverdueDivisionChapters,
			hasData ? FormatPercentage(snapshot.CompletionPercentage) : "Open",
			hasData
				? $"{snapshot.CompletedDivisionChapters} of {snapshot.TotalDivisionChapters} scoped division-chapters completed"
				: "No scoped curriculum progress found; open Academic Dashboard",
			hasData ? CurriculumStatus(snapshot.CompletionPercentage, snapshot.OverdueDivisionChapters) : "info",
			health: hasData ? CurriculumHealth(snapshot.CompletionPercentage, snapshot.OverdueDivisionChapters) : "attention");
	}

	private async Task ApplyPrincipalCurriculumAsync(
		DashboardSummaryDto summary,
		CurriculumSnapshot snapshot)
	{
		if (summary.DashboardTemplate != PrincipalAdmin ||
		    !await HasAnyPermissionAsync(WidgetDefinitions[PrincipalCurriculumWidgetId].RequiredPermissions))
		{
			return;
		}

		if (snapshot.TotalDivisionChapters == 0)
		{
			UpdateWidget(
				summary.KpiCards,
				PrincipalCurriculumWidgetId,
				0,
				"0%",
				"No scoped curriculum chapters found",
				"info");
			return;
		}

		UpdateWidget(
			summary.KpiCards,
			PrincipalCurriculumWidgetId,
			snapshot.CompletedDivisionChapters,
			FormatPercentage(snapshot.CompletionPercentage),
			$"{snapshot.CompletedDivisionChapters} of {snapshot.TotalDivisionChapters} scoped division-chapters completed",
			CurriculumStatus(snapshot.CompletionPercentage, snapshot.OverdueDivisionChapters));
	}

	private async Task ApplyPrincipalCurriculumRiskAsync(
		DashboardSummaryDto summary,
		CurriculumSnapshot snapshot)
	{
		if (summary.DashboardTemplate != PrincipalAdmin ||
		    !await HasAnyPermissionAsync(WidgetDefinitions[PrincipalWeakClassesWidgetId].RequiredPermissions))
		{
			return;
		}

		UpdateWidget(
			summary.RiskAlerts,
			PrincipalWeakClassesWidgetId,
			snapshot.OverdueDivisionChapters,
			null,
			snapshot.OverdueDivisionChapters == 0
				? "No scoped curriculum pacing risk found."
				: BuildCountSubtitle(snapshot.OverdueDivisionChapters, "scoped division-chapter is past plan", "scoped division-chapters are past plan"),
			snapshot.OverdueDivisionChapters == 0 ? "success" : "warning",
			title: "Curriculum pacing risk");
	}

	private async Task ApplyPrincipalAcademicDashboardAsync(
		DashboardSummaryDto summary,
		CurriculumSnapshot snapshot)
	{
		if (summary.DashboardTemplate != PrincipalAdmin ||
		    !await HasAnyPermissionAsync(WidgetDefinitions[PrincipalAcademicDashboardWidgetId].RequiredPermissions))
		{
			return;
		}

		var hasData = snapshot.TotalDivisionChapters > 0;
		UpdateWidget(
			summary.ModuleSnapshots,
			PrincipalAcademicDashboardWidgetId,
			snapshot.OverdueDivisionChapters,
			hasData ? FormatPercentage(snapshot.CompletionPercentage) : "Open",
			hasData
				? $"{snapshot.OverdueDivisionChapters} scoped division-chapters need pacing review"
				: "Open Academic Dashboard for class performance and syllabus risk",
			hasData ? CurriculumStatus(snapshot.CompletionPercentage, snapshot.OverdueDivisionChapters) : "primary",
			health: hasData ? CurriculumHealth(snapshot.CompletionPercentage, snapshot.OverdueDivisionChapters) : "attention");
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

	private async Task<bool> HasAnyRequiredAcademicPermissionAsync(string template)
	{
		var widgetIds = template == Leadership
			? [LeadershipAcademicHealthWidgetId]
			:
			new[]
			{
				PrincipalCurriculumWidgetId,
				PrincipalWeakClassesWidgetId,
				PrincipalAcademicDashboardWidgetId
			};

		foreach (var widgetId in widgetIds)
		{
			if (await HasAnyPermissionAsync(WidgetDefinitions[widgetId].RequiredPermissions))
			{
				return true;
			}
		}

		return false;
	}

	private static string FormatPercentage(int percentage) =>
		percentage.ToString(CultureInfo.InvariantCulture) + "%";

	private static string CurriculumStatus(int percentage, int overdueCount) =>
		overdueCount > 0 ? "warning" : percentage >= 90 ? "success" : percentage >= 60 ? "info" : "warning";

	private static string CurriculumHealth(int percentage, int overdueCount) =>
		overdueCount > 0 ? "attention" : percentage >= 90 ? "good" : "attention";

	private sealed record CurriculumSnapshot(
		int TotalDivisionChapters,
		int CompletedDivisionChapters,
		int OverdueDivisionChapters,
		int CompletionPercentage)
	{
		public static CurriculumSnapshot Empty { get; } = new(0, 0, 0, 0);
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

	private sealed class CurriculumChapterRow
	{
		public Guid StandardId { get; init; }
		public Guid ChapterId { get; init; }
		public DateTimeOffset PlannedEndDate { get; init; }
	}

	private sealed class CurriculumChapterDivisionRow : IEquatable<CurriculumChapterDivisionRow>
	{
		public Guid ChapterId { get; init; }
		public Guid DivisionId { get; init; }
		public DateTimeOffset PlannedEndDate { get; init; }

		public bool Equals(CurriculumChapterDivisionRow? other) =>
			other is not null &&
			ChapterId == other.ChapterId &&
			DivisionId == other.DivisionId;

		public override bool Equals(object? obj) => Equals(obj as CurriculumChapterDivisionRow);

		public override int GetHashCode() => HashCode.Combine(ChapterId, DivisionId);
	}

	private sealed class CurriculumProgressRow
	{
		public Guid ChapterId { get; init; }
		public Guid DivisionId { get; init; }
		public int Progress { get; init; }
		public DateTime CreationTime { get; init; }
	}
}
