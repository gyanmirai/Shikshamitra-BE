using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

[RemoteService(false)]
public class PayrollAdjustmentAppWorkflow(
	PayrollAdjustmentReader adjustmentReader,
	PayrollAdjustmentWorkflow adjustmentWorkflow,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollSnapshotRefresher snapshotRefresher,
	PayrollAcademicYearScopeResolver academicYearScopeResolver,
	PayrollReviewWorkflow reviewWorkflow) : ApplicationService, ITransientDependency
{
	public async Task<List<PayrollAdjustmentDto>> GetAdjustmentsAsync(
		string month,
		Guid? employeeId = null,
		Guid? academicYearId = null)
	{
		var (year, monthNumber, internalMonthYear) = PayrollRunRules.ParseMonth(month);
		await academicYearScopeResolver.ResolveAsync(academicYearId, year, monthNumber);
		var employeeIds = employeeId.HasValue
			? PayrollTargetEmployeeResolver.ResolveTargetEmployees(
					await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, monthNumber, new List<Guid> { employeeId.Value }),
					new List<Guid> { employeeId.Value })
				.Select(x => x.Id)
				.ToList()
			: (await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, monthNumber, null)).Select(x => x.Id).ToList();

		return await adjustmentReader.GetListAsync(internalMonthYear, employeeIds);
	}

	public async Task<PayrollAdjustmentDto> CreateAdjustmentAsync(PayrollAdjustmentRequest request)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		var academicYearId = await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { request.EmployeeId });
		await reviewWorkflow.EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);
		var adjustment = await adjustmentWorkflow.CreateAsync(request, internalMonthYear);
		await snapshotRefresher.RefreshForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			academicYearId,
			new[] { request.EmployeeId },
			preserveReviewedSnapshots: true);
		return adjustment;
	}

	public async Task<PayrollAdjustmentDto> UpdateAdjustmentAsync(Guid id, PayrollAdjustmentRequest request)
	{
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(request.Month);
		var academicYearId = await academicYearScopeResolver.ResolveAsync(request.AcademicYearId, year, month);
		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { request.EmployeeId });
		await reviewWorkflow.EnsureReviewCanChangeAsync(internalMonthYear, request.Month, targetEmployees);
		var adjustment = await adjustmentWorkflow.UpdateAsync(id, request, internalMonthYear);
		await snapshotRefresher.RefreshForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			academicYearId,
			new[] { request.EmployeeId },
			preserveReviewedSnapshots: true);
		return adjustment;
	}

	public async Task DeleteAdjustmentAsync(Guid id)
	{
		var adjustment = await adjustmentReader.GetOrThrowAsync(id);
		var (year, month, internalMonthYear) = PayrollRunRules.ParseMonth(adjustment.MonthYear);
		var targetEmployees = await scopedEmployeeResolver.ResolveScopedTargetEmployeesAsync(year, month, new List<Guid> { adjustment.EmployeeId });
		await reviewWorkflow.EnsureReviewCanChangeAsync(adjustment.MonthYear, adjustment.MonthYear, targetEmployees);
		adjustment = await adjustmentWorkflow.DeleteAsync(id);
		await snapshotRefresher.RefreshForEmployeeIdsAsync(
			internalMonthYear,
			year,
			month,
			null,
			new[] { adjustment.EmployeeId },
			preserveReviewedSnapshots: true);
	}
}
