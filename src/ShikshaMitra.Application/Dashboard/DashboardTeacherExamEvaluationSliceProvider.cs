using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
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

public class DashboardTeacherExamEvaluationSliceProvider(
	DashboardTeacherScopeResolver teacherScopeResolver,
	IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
	IRepository<ExamMarksWorkflowState, Guid> workflowStateRepository,
	IRepository<Exam, Guid> examRepository,
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
	private const string WidgetId = "teacher-exam-evaluation";

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		var summary = context.Summary;
		if (summary.DashboardTemplate != Teacher)
		{
			return;
		}

		if (!await HasAnyPermissionAsync(WidgetDefinitions[WidgetId].RequiredPermissions))
		{
			return;
		}

		try
		{
			var scope = await teacherScopeResolver.ResolveAsync();
			if (!scope.IsResolved || !scope.EmployeeId.HasValue)
			{
				return;
			}

			var snapshot = await BuildSnapshotAsync(
				scope.EmployeeId.Value,
				summary.AcademicYearId);
			if (!snapshot.HasScopedTeacherSubjectAllocation)
			{
				return;
			}

			UpdateWidget(
				summary.KpiCards,
				WidgetId,
				snapshot.PendingWorkflowCount,
				snapshot.PendingWorkflowCount.ToString(CultureInfo.InvariantCulture),
				snapshot.PendingWorkflowCount == 0
					? "No scoped assigned-subject exam marks workflow pending"
					: BuildCountSubtitle(snapshot.PendingWorkflowCount, "scoped assigned-subject exam marks workflow needs action", "scoped assigned-subject exam marks workflows need action"),
				snapshot.PendingWorkflowCount == 0 ? "success" : "warning",
				title: "Pending Exam Evaluation");
		}
		catch
		{
			SetZoneError(summary, Kpis, "Teacher exam evaluation could not be loaded.");
		}
	}

	private async Task<TeacherExamEvaluationSnapshot> BuildSnapshotAsync(
		Guid teacherId,
		string? academicYearId)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var allocations = await BuildScopedTeacherSubjectAllocationsAsync(teacherId, scope, academicYearId);
		if (allocations.Length == 0)
		{
			return TeacherExamEvaluationSnapshot.Unavailable;
		}

		var standardIds = allocations
			.Select(allocation => allocation.StandardId)
			.Distinct()
			.ToList();
		var divisionIds = allocations
			.Select(allocation => allocation.DivisionId)
			.Distinct()
			.ToList();
		var subjectIds = allocations
			.Select(allocation => allocation.SubjectId)
			.Distinct()
			.ToList();

		var workflows = await workflowStateRepository.GetQueryableAsync();
		var exams = await examRepository.GetQueryableAsync();
		var candidateRows = await asyncExecuter.ToListAsync(
			from workflow in workflows
			join exam in exams on workflow.ExamId equals exam.Id
			where standardIds.Contains(exam.StandardId) &&
			      divisionIds.Contains(workflow.DivisionId) &&
			      subjectIds.Contains(workflow.SubjectId) &&
			      workflow.Status != ExamMarksWorkflowStatus.Published
			select new TeacherExamWorkflowRow
			{
				StandardId = exam.StandardId,
				DivisionId = workflow.DivisionId,
				SubjectId = workflow.SubjectId
			});

		var allocationKeys = allocations
			.Select(allocation => new TeacherSubjectKey(allocation.StandardId, allocation.DivisionId, allocation.SubjectId))
			.ToHashSet();
		var pendingCount = candidateRows.Count(row =>
			allocationKeys.Contains(new TeacherSubjectKey(row.StandardId, row.DivisionId, row.SubjectId)));

		return new TeacherExamEvaluationSnapshot(true, pendingCount);
	}

	private async Task<ScopedTeacherSubjectAllocationRow[]> BuildScopedTeacherSubjectAllocationsAsync(
		Guid teacherId,
		UserScopeContext scope,
		string? academicYearId)
	{
		if (!scope.HasAnyAccess)
		{
			return [];
		}

		var allocations = await teacherSubjectAllocationRepository.GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var stages = await stageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();

		var query =
			from allocation in allocations
			join division in divisions on allocation.DivisionId equals division.Id
			join standard in standards on allocation.StandardId equals standard.Id
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where allocation.Teachers.Any(teacher => teacher.TeacherId == teacherId)
			select new ScopedTeacherSubjectAllocationRow
			{
				AcademicYearId = board.AcademicYearId,
				BoardId = board.Id,
				MediumId = medium.Id,
				MediumStageId = stage.Id,
				StandardId = allocation.StandardId,
				DivisionId = allocation.DivisionId,
				SubjectId = allocation.SubjectId
			};

		var rows = await asyncExecuter.ToListAsync(query);
		IEnumerable<ScopedTeacherSubjectAllocationRow> scopedRows = rows;
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
			.DistinctBy(row => new { row.StandardId, row.DivisionId, row.SubjectId })
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

	private sealed record TeacherExamEvaluationSnapshot(
		bool HasScopedTeacherSubjectAllocation,
		int PendingWorkflowCount)
	{
		public static TeacherExamEvaluationSnapshot Unavailable { get; } = new(false, 0);
	}

	private sealed record TeacherSubjectKey(Guid StandardId, Guid DivisionId, Guid SubjectId);

	private sealed class TeacherExamWorkflowRow
	{
		public Guid StandardId { get; init; }
		public Guid DivisionId { get; init; }
		public Guid SubjectId { get; init; }
	}

	private sealed class ScopedTeacherSubjectAllocationRow
	{
		public Guid AcademicYearId { get; init; }
		public Guid BoardId { get; init; }
		public Guid MediumId { get; init; }
		public Guid MediumStageId { get; init; }
		public Guid StandardId { get; init; }
		public Guid DivisionId { get; init; }
		public Guid SubjectId { get; init; }
	}
}
