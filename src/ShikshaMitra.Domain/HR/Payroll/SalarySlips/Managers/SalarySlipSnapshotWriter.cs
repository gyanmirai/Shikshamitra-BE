using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class SalarySlipSnapshotWriter(ISalarySlipRepository salarySlipRepository) : DomainService
{
	public async Task<SalarySlip> GenerateOrGetSalarySlipFromSnapshotAsync(
		PayrollRunItemSnapshot snapshot,
		List<SalarySlipLineItem> earningLineItems,
		List<SalarySlipLineItem> deductionLineItems,
		DayCalculationType dayCalculationMode,
		CancellationToken cancellationToken = default)
	{
		var existingSlip = await salarySlipRepository.GetByEmployeeAndMonthAsync(
			snapshot.EmployeeId,
			snapshot.MonthYear,
			cancellationToken);

		if (existingSlip != null && existingSlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
		{
			throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
		}

		if (existingSlip == null)
		{
			var salarySlip = new SalarySlip(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				snapshot.EmployeeId,
				snapshot.MonthYear,
				earningLineItems,
				deductionLineItems,
				Clock.Now);

			SalarySlipGenerationRules.ApplySnapshotMetadata(salarySlip, snapshot, dayCalculationMode);
			await salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);
			return salarySlip;
		}

		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems, Clock.Now);
		SalarySlipGenerationRules.ApplySnapshotMetadata(existingSlip, snapshot, dayCalculationMode);
		await salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);
		return existingSlip;
	}
}
