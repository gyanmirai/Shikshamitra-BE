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
	public async Task<SalarySlip> ApplyUnpaidCorrectionAsync(SalarySlip source, PayrollRunItemSnapshot snapshot,
		List<SalarySlipLineItem> earnings, List<SalarySlipLineItem> deductions,
		List<SalarySlipLineItem> employerContributions, DayCalculationType dayMode)
	{
		if (source.EmployeeId != snapshot.EmployeeId || source.MonthYear != snapshot.MonthYear || source.TenantId != snapshot.TenantId)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		source.BeginUnpaidCorrection();
		var corrected = await GenerateOrGetSalarySlipFromSnapshotAsync(snapshot, earnings, deductions, employerContributions, dayMode);
		corrected.FinalizeSlip();
		return corrected;
	}

	public async Task<SalarySlip> GenerateOrGetSalarySlipFromSnapshotAsync(
		PayrollRunItemSnapshot snapshot,
		List<SalarySlipLineItem> earningLineItems,
		List<SalarySlipLineItem> deductionLineItems,
		List<SalarySlipLineItem> employerContributionLineItems,
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
			salarySlip.SetStatutorySnapshot(employerContributionLineItems, snapshot.ProvidentFundRuleId,
				snapshot.ProvidentFundRuleVersion, snapshot.ProfessionalTaxRuleId, snapshot.ProfessionalTaxRuleVersion,
				snapshot.EmployeeStateInsuranceRuleId, snapshot.EmployeeStateInsuranceRuleVersion,
				snapshot.EmployeeStateInsuranceWages, snapshot.EmployeeStateInsuranceGovernmentContribution,
				snapshot.EmployeeStateInsuranceCoverageDecision,
				snapshot.EmployeeStateInsuranceNumber, snapshot.EmployeeStateInsuranceContributionPeriodStart,
				snapshot.EmployeeStateInsuranceContributionPeriodEnd, snapshot.EsiConfigurationId);
			salarySlip.SetCompensationSnapshot(
				snapshot.CompensationRevisionId,
				snapshot.CompensationRevisionNumber,
				snapshot.CompensationSalaryBasis,
				snapshot.CompensationProtectionPolicy,
				snapshot.CompensationQuotedAmount,
				snapshot.CompensationProtectedAmount,
				snapshot.CompensationNormalizedProtectedAmount,
				snapshot.CompensationTargetDifference);
			await salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);
			return salarySlip;
		}

		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems, Clock.Now);
		SalarySlipGenerationRules.ApplySnapshotMetadata(existingSlip, snapshot, dayCalculationMode);
		existingSlip.SetStatutorySnapshot(employerContributionLineItems, snapshot.ProvidentFundRuleId,
			snapshot.ProvidentFundRuleVersion, snapshot.ProfessionalTaxRuleId, snapshot.ProfessionalTaxRuleVersion,
			snapshot.EmployeeStateInsuranceRuleId, snapshot.EmployeeStateInsuranceRuleVersion,
			snapshot.EmployeeStateInsuranceWages, snapshot.EmployeeStateInsuranceGovernmentContribution,
			snapshot.EmployeeStateInsuranceCoverageDecision,
			snapshot.EmployeeStateInsuranceNumber, snapshot.EmployeeStateInsuranceContributionPeriodStart,
			snapshot.EmployeeStateInsuranceContributionPeriodEnd, snapshot.EsiConfigurationId);
		existingSlip.SetCompensationSnapshot(
			snapshot.CompensationRevisionId,
			snapshot.CompensationRevisionNumber,
			snapshot.CompensationSalaryBasis,
			snapshot.CompensationProtectionPolicy,
			snapshot.CompensationQuotedAmount,
			snapshot.CompensationProtectedAmount,
			snapshot.CompensationNormalizedProtectedAmount,
			snapshot.CompensationTargetDifference);
		await salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);
		return existingSlip;
	}
}
