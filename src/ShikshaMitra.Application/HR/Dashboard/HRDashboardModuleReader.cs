using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Dashboard;

public class HRDashboardModuleReader(
	HrEmployeeScopeQueryService employeeScope,
	IRepository<EmployeeApplication, Guid> applicationRepository,
	IRepository<ProfessionalReviewCycle, Guid> cycleRepository,
	IRepository<PerformanceReviewAssignment, Guid> reviewRepository,
	IRepository<PerformanceReviewStage, Guid> stageRepository,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	internal async Task<List<Employee>> ReadEmployeesAsync(UserScopeContext scope) =>
		await executer.ToListAsync(await employeeScope.BuildScopedEmployeeQueryAsync(scope));

	internal static HRDashboardOperationalWorkforceDto CountWorkforce(List<Employee> employees, DateOnly today)
	{
		var active = employees.Where(x => HRDashboardOperationalStaffReader.IsEligible(x, today)).ToList();
		var teaching = active.Count(x => x.EmployeeGroup == EmployeeGroupType.Teaching);
		var monthStart = new DateOnly(today.Year, today.Month, 1);
		return new(active.Count, teaching, active.Count - teaching,
			active.Count(x => x.DateOfJoining.HasValue && x.DateOfJoining.Value >= monthStart && x.DateOfJoining.Value <= today));
	}

	internal async Task<HRDashboardOnboardingDto> ReadOnboardingAsync()
	{
		var query = await applicationRepository.GetQueryableAsync();
		var groups = await executer.ToListAsync(query.GroupBy(x => x.Status)
			.Select(x => new { Status = x.Key, Count = x.Count() }));
		return new(
			groups.Where(x => x.Status == EmployeeApplicationStatus.ReadyForOnboarding).Sum(x => x.Count),
			groups.Where(x => x.Status is EmployeeApplicationStatus.Applied or EmployeeApplicationStatus.Screening or
				EmployeeApplicationStatus.Interviewing or EmployeeApplicationStatus.Offered).Sum(x => x.Count));
	}

	internal async Task<HRDashboardAppraisalDto> ReadAppraisalsAsync(List<Guid> employeeIds, DateOnly today)
	{
		var cycleQuery = await cycleRepository.GetQueryableAsync();
		var cycles = await executer.ToListAsync(cycleQuery.Where(x => x.Status == ProfessionalReviewCycleStatus.Published ||
			x.Status == ProfessionalReviewCycleStatus.InProgress || x.Status == ProfessionalReviewCycleStatus.Calibration));
		var cycleIds = cycles.Select(x => x.Id).ToArray();
		var reviewQuery = await reviewRepository.GetQueryableAsync();
		var reviews = await executer.ToListAsync(reviewQuery.Where(x => Enumerable.Contains(cycleIds, x.CycleId) &&
			employeeIds.Contains(x.EmployeeId) && x.Status != ProfessionalReviewStatus.Finalized)
			.Select(x => new { x.Id, x.CycleId }));
		var reviewIds = reviews.Select(x => x.Id).ToArray();
		var stageQuery = await stageRepository.GetQueryableAsync();
		var stages = await executer.ToListAsync(stageQuery.Where(x => Enumerable.Contains(reviewIds, x.ReviewAssignmentId) &&
			x.Status != PerformanceReviewStageStatus.Submitted)
			.Select(x => new { x.ReviewAssignmentId, x.StageType }));
		var cyclesById = cycles.ToDictionary(x => x.Id);
		var cyclesByReview = reviews.ToDictionary(x => x.Id, x => cyclesById[x.CycleId]);
		var deadlines = stages.Select(x => new
		{
			x.ReviewAssignmentId,
			Date = ResolveDeadline(cyclesByReview[x.ReviewAssignmentId], x.StageType)
		}).Where(x => x.Date.HasValue).ToList();
		var upcoming = deadlines.Where(x => x.Date >= today).Select(x => x.Date!.Value).ToList();
		return new(reviews.Count,
			deadlines.Where(x => x.Date < today).Select(x => x.ReviewAssignmentId).Distinct().Count(),
			upcoming.Count == 0 ? null : upcoming.Min());
	}

	private static DateOnly? ResolveDeadline(ProfessionalReviewCycle cycle, PerformanceReviewStageType stage)
	{
		var deadline = stage switch
		{
			PerformanceReviewStageType.SelfAppraisal => cycle.SelfReviewDueDate,
			PerformanceReviewStageType.HrVerification or PerformanceReviewStageType.PrincipalManagement => cycle.FinalizationDueDate,
			_ => cycle.ReviewerDueDate
		};
		return deadline.HasValue ? DateOnly.FromDateTime(deadline.Value) : null;
	}
}
