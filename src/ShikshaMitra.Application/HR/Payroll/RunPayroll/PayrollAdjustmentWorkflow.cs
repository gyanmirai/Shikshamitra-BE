using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAdjustmentWorkflow(
	IRepository<PayrollAdjustment, Guid> payrollAdjustmentRepository,
	IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
	PayrollAdjustmentReader adjustmentReader,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<PayrollAdjustmentDto> CreateAsync(
		PayrollAdjustmentRequest request,
		string internalMonthYear)
	{
		await EnsureCanChangeAsync(internalMonthYear, request.Month, request.EmployeeId);

		var adjustment = new PayrollAdjustment(
			guidGenerator.Create(),
			currentTenant.Id,
			internalMonthYear,
			request.EmployeeId,
			request.Name,
			(decimal)request.Amount,
			request.IsEarning,
			request.Reason,
			request.Description);

		await payrollAdjustmentRepository.InsertAsync(adjustment, autoSave: true);
		return PayrollRunProjection.ToAdjustmentDto(adjustment);
	}

	public async Task<PayrollAdjustmentDto> UpdateAsync(
		Guid id,
		PayrollAdjustmentRequest request,
		string internalMonthYear)
	{
		var adjustment = await adjustmentReader.GetOrThrowAsync(id);
		if (adjustment.MonthYear != internalMonthYear || adjustment.EmployeeId != request.EmployeeId)
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentNotFound);
		}

		await EnsureCanChangeAsync(internalMonthYear, request.Month, request.EmployeeId);

		adjustment.Update(
			request.Name,
			(decimal)request.Amount,
			request.IsEarning,
			request.Reason,
			request.Description);

		await payrollAdjustmentRepository.UpdateAsync(adjustment, autoSave: true);
		return PayrollRunProjection.ToAdjustmentDto(adjustment);
	}

	public async Task<PayrollAdjustment> DeleteAsync(Guid id)
	{
		var adjustment = await adjustmentReader.GetOrThrowAsync(id);
		await EnsureCanChangeAsync(adjustment.MonthYear, adjustment.MonthYear, adjustment.EmployeeId);
		await payrollAdjustmentRepository.DeleteAsync(adjustment, autoSave: true);
		return adjustment;
	}

	private async Task EnsureCanChangeAsync(string internalMonthYear, string requestMonth, Guid employeeId)
	{
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		var snapshot = await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.MonthYear == internalMonthYear &&
			x.EmployeeId == employeeId));

		if (snapshot != null && !snapshot.IsLocked && snapshot.ReviewStatus == ReviewStatuses.Reviewed)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollAdjustmentRequiresReviewedPreview)
			.WithData("month", requestMonth)
			.WithData("employeeId", employeeId)
			.WithData("reviewStatus", snapshot?.ReviewStatus ?? ReviewStatuses.Pending)
			.WithData("isLocked", snapshot?.IsLocked ?? false);
	}
}
