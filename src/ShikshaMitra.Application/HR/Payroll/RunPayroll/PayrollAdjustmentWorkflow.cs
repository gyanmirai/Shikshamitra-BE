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
		await EnsureCanCreateAsync(internalMonthYear, request.Month, request.EmployeeId);

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
		EnsureUserEditable(adjustment);
		if (adjustment.MonthYear != internalMonthYear || adjustment.EmployeeId != request.EmployeeId)
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentNotFound);
		}

		await EnsureCanModifyExistingAsync(internalMonthYear, request.Month, request.EmployeeId);

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
		EnsureUserEditable(adjustment);
		await EnsureCanModifyExistingAsync(adjustment.MonthYear, adjustment.MonthYear, adjustment.EmployeeId);
		await payrollAdjustmentRepository.DeleteAsync(adjustment, autoSave: true);
		return adjustment;
	}

	public async Task<PayrollAdjustmentDto> UpsertSystemGeneratedAsync(
		string internalMonthYear,
		Guid employeeId,
		string name,
		decimal amount,
		bool isEarning,
		string reason,
		string? description,
		string sourceType,
		Guid sourceId)
	{
		await EnsureSystemPostingMonthUnlockedAsync(internalMonthYear, employeeId);

		var query = await payrollAdjustmentRepository.GetQueryableAsync();
		var existing = await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.SourceType == sourceType &&
			x.SourceId == sourceId));

		if (existing == null)
		{
			existing = new PayrollAdjustment(
				guidGenerator.Create(),
				currentTenant.Id,
				internalMonthYear,
				employeeId,
				name,
				amount,
				isEarning,
				reason,
				description,
				sourceType,
				sourceId,
				isSystemGenerated: true);
			await payrollAdjustmentRepository.InsertAsync(existing, autoSave: true);
			return PayrollRunProjection.ToAdjustmentDto(existing);
		}

		existing.UpdatePostingScope(internalMonthYear, employeeId);
		existing.SetSource(sourceType, sourceId, isSystemGenerated: true);
		existing.Update(name, amount, isEarning, reason, description);
		await payrollAdjustmentRepository.UpdateAsync(existing, autoSave: true);
		return PayrollRunProjection.ToAdjustmentDto(existing);
	}

	private async Task EnsureCanCreateAsync(string internalMonthYear, string requestMonth, Guid employeeId)
	{
		var snapshot = await GetSnapshotAsync(internalMonthYear, employeeId);
		if (snapshot != null && !snapshot.IsLocked && snapshot.ReviewStatus == ReviewStatuses.Reviewed)
		{
			return;
		}

		ThrowCannotChange(requestMonth, employeeId, snapshot);
	}

	private async Task EnsureCanModifyExistingAsync(string internalMonthYear, string requestMonth, Guid employeeId)
	{
		var snapshot = await GetSnapshotAsync(internalMonthYear, employeeId);
		if (snapshot != null &&
		    !snapshot.IsLocked &&
		    snapshot.ReviewStatus is ReviewStatuses.Reviewed or ReviewStatuses.Pending)
		{
			return;
		}

		ThrowCannotChange(requestMonth, employeeId, snapshot);
	}

	private async Task<PayrollRunItemSnapshot?> GetSnapshotAsync(string internalMonthYear, Guid employeeId)
	{
		var query = await payrollRunItemSnapshotRepository.GetQueryableAsync();

		return await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.MonthYear == internalMonthYear &&
			x.EmployeeId == employeeId));
	}

	private async Task EnsureSystemPostingMonthUnlockedAsync(string internalMonthYear, Guid employeeId)
	{
		var snapshot = await GetSnapshotAsync(internalMonthYear, employeeId);
		if (snapshot?.IsLocked == true)
		{
			throw new BusinessException(HRErrorCodes.PayrollAlreadyLocked)
				.WithData("month", internalMonthYear)
				.WithData("employeeId", employeeId);
		}
	}

	private static void EnsureUserEditable(PayrollAdjustment adjustment)
	{
		if (adjustment.IsSystemGenerated)
		{
			throw new BusinessException(HRErrorCodes.PayrollAdjustmentSystemGeneratedReadOnly)
				.WithData("adjustmentId", adjustment.Id)
				.WithData("sourceType", adjustment.SourceType ?? string.Empty)
				.WithData("sourceId", adjustment.SourceId?.ToString() ?? string.Empty);
		}
	}

	private static void ThrowCannotChange(string requestMonth, Guid employeeId, PayrollRunItemSnapshot? snapshot)
	{
		throw new BusinessException(HRErrorCodes.PayrollAdjustmentRequiresReviewedPreview)
			.WithData("month", requestMonth)
			.WithData("employeeId", employeeId)
			.WithData("reviewStatus", snapshot?.ReviewStatus ?? ReviewStatuses.Pending)
			.WithData("isLocked", snapshot?.IsLocked ?? false);
	}
}
