using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class DueFeeManager(
	IDueFeeRepository dueFeeRepository,
	PaymentTermScheduleResolver scheduleResolver,
	ProrationPolicyCalculationService prorationCalculator
) : DomainService
{
	public const int DefaultDueDayOfMonth = 10;

	public static DateTime ComputeDefaultDueDate(MonthEnum month, int year) =>
		new DateTime(year, (int)month + 1, 1).AddDays(DefaultDueDayOfMonth - 1);

	public async Task<IReadOnlyList<DueFee>> GenerateForStudentAsync(
		Guid studentId,
		FeeStructure feeStructure,
		AcademicYear academicYear,
		IReadOnlyList<Guid> selectedOptionalFeeHeadIds,
		IReadOnlyList<CustomOptionalFeeHeadDetail>? optionalFeeHeadDetails = null,
		ProrationPolicySnapshot? prorationPolicy = null,
		Guid? prorationPolicyVersionId = null,
		DateTime? chargeStartDate = null,
		DateTime? chargeEndDate = null)
	{
		var alreadyExists = await dueFeeRepository.ExistsForStudentInPeriodAsync(
			studentId, academicYear.StartDate.Date, academicYear.EndDate.Date);
		if (alreadyExists)
			throw new BusinessException(DueFeeErrorCodes.DueFeesAlreadyGenerated)
				.WithData("StudentId", studentId);

		var assignmentChargeStart = (chargeStartDate ?? academicYear.StartDate).Date;
		var assignmentChargeEnd = (chargeEndDate ?? academicYear.EndDate).Date;
		ValidateChargePeriod(assignmentChargeStart, assignmentChargeEnd, academicYear);
		if (prorationPolicy != null && !prorationPolicyVersionId.HasValue)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);

		var monthRange = ComputeMonthRange(academicYear);
		var dueFees = new List<DueFee>();
		foreach (var detail in feeStructure.FeeStructureDetails)
		{
			if (detail.IsOptional && !selectedOptionalFeeHeadIds.Contains(detail.FeeHeadId))
				continue;

			var customDetail = optionalFeeHeadDetails?.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId);
			var detailChargeStart = (customDetail?.ChargeStartDate ?? assignmentChargeStart).Date;
			var detailChargeEnd = (customDetail?.ChargeEndDate ?? assignmentChargeEnd).Date;
			ValidateChargePeriod(detailChargeStart, detailChargeEnd, academicYear, detail.FeeHeadId);

			if (detail.PaymentTermId.HasValue && detail.PaymentTermVersionId.HasValue)
			{
				dueFees.AddRange(await GenerateFromPaymentTermAsync(
					studentId, detail, customDetail, prorationPolicy, prorationPolicyVersionId,
					detailChargeStart, detailChargeEnd, academicYear));
				continue;
			}

			dueFees.AddRange(GenerateLegacy(
				studentId, detail, monthRange, customDetail, prorationPolicy,
				prorationPolicyVersionId, detailChargeStart, detailChargeEnd));
		}

		return dueFees;
	}

	private async Task<List<DueFee>> GenerateFromPaymentTermAsync(
		Guid studentId,
		FeeStructureDetail detail,
		CustomOptionalFeeHeadDetail? customDetail,
		ProrationPolicySnapshot? prorationPolicy,
		Guid? prorationPolicyVersionId,
		DateTime chargeStartDate,
		DateTime chargeEndDate, AcademicYear academicYear)
	{
		var schedule = await scheduleResolver.ResolveAsync(detail);
		if (!schedule.IsValid || schedule.Installments.Count == 0)
			throw new BusinessException(PaymentTermErrorCodes.VersionNotPublished)
				.WithData("FeeStructureDetailId", detail.Id)
				.WithData("PaymentTermId", detail.PaymentTermId);

		var annualAmount = customDetail?.Amount ?? detail.Amount;
		var rows = new List<DueFee>();
        var weights = schedule.Installments.Select(x => x.AllocationPercentage).ToArray();
        var scheduled = AssignmentAmountCalculator.Allocate(annualAmount, weights);
        var rule = prorationPolicy?.Rules.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
            ?? new ProrationPolicyRuleSnapshot(detail.FeeHeadId, ProrationRuleType.FullCharge, ProrationRoundingMode.Currency, null, null, null);
        if (detail.IsOneTime) rule = rule with { RuleType = ProrationRuleType.FullCharge };
        var calculation = AssignmentAmountCalculator.Calculate(prorationCalculator, rule, annualAmount,
            academicYear.StartDate.Date, academicYear.EndDate.Date, chargeStartDate, chargeEndDate);
        var obligations = AssignmentAmountCalculator.Allocate(calculation.ProratedAmount, weights);
		for (var i = 0; i < schedule.Installments.Count; i++)
		{
			var installment = schedule.Installments[i];
			var scheduledAmount = scheduled[i];
			var dueDate = installment.DueDate;
			rows.Add(new DueFee(
				id: GuidGenerator.Create(), tenantId: CurrentTenant.Id, studentId: studentId,
				feeHeadId: detail.FeeHeadId, month: ToMonthEnum(dueDate.Month), year: dueDate.Year,
				isOneTime: detail.IsOneTime, amount: obligations[i], dueDate: dueDate,
				feeStructureDetailId: detail.Id, paymentTermId: schedule.PaymentTermId,
				paymentTermVersionId: schedule.PaymentTermVersionId, paymentTermInstallmentId: null,
				installmentSequence: installment.Sequence, installmentName: installment.Name,
				coverageStartDate: installment.CoverageStartDate, coverageEndDate: installment.CoverageEndDate,
				demandDate: installment.DemandDate, scheduleDueDate: installment.DueDate,
				scheduleSource: ScheduleSource.PaymentTerm, scheduledAmount: scheduledAmount,
				prorationPolicyId: prorationPolicy?.PolicyId, prorationPolicyVersionId: prorationPolicyVersionId,
				prorationChargeStartDate: prorationPolicy == null ? null : chargeStartDate,
				prorationChargeEndDate: prorationPolicy == null ? null : chargeEndDate,
				prorationEligibleStartDate: calculation.EligibleStartDate,
				prorationEligibleEndDate: calculation.EligibleEndDate,
				prorationCoverageDays: calculation.CoverageDays,
				prorationEligibleDays: calculation.ActualEligibleDays,
				prorationChargedDays: calculation.ChargedDays,
				prorationFactor: calculation.Factor,
				prorationRuleType: calculation.RuleType,
				prorationRoundingMode: calculation.RoundingMode,
				prorationExplanation: calculation.Explanation));
			rows[^1].PreserveAllocation(installment.AllocationPercentage);
		}

		return rows;
	}

	private List<DueFee> GenerateLegacy(
		Guid studentId,
		FeeStructureDetail detail,
		List<(MonthEnum Month, int Year)> monthRange,
		CustomOptionalFeeHeadDetail? customDetail,
		ProrationPolicySnapshot? prorationPolicy,
		Guid? prorationPolicyVersionId,
		DateTime chargeStartDate,
		DateTime chargeEndDate)
	{
		var dueFees = new List<DueFee>();
		if (detail.IsOneTime)
		{
			var month = customDetail?.PayableMonth ?? detail.PayableMonth ?? monthRange[0].Month;
			var year = customDetail?.PayableYear ?? detail.PayableYear ?? monthRange[0].Year;
			var amount = customDetail?.Amount ?? detail.Amount;
			if (!monthRange.Any(m => m.Month == month && m.Year == year))
				throw new BusinessException("Finance:OutofAcademicYearRange")
					.WithData("Month", month.ToString()).WithData("Year", year);
			dueFees.Add(CreateLegacyDueFee(
				studentId, detail, month, year, amount,
				prorationPolicy, prorationPolicyVersionId, chargeStartDate, chargeEndDate));
			return dueFees;
		}

		var selectedMonths = monthRange;
		if (customDetail?.StartPayableMonth.HasValue == true && customDetail.StartPayableYear.HasValue &&
			customDetail.EndPayableMonth.HasValue && customDetail.EndPayableYear.HasValue)
		{
			var start = new DateTime(customDetail.StartPayableYear.Value, (int)customDetail.StartPayableMonth.Value + 1, 1);
			var end = new DateTime(customDetail.EndPayableYear.Value, (int)customDetail.EndPayableMonth.Value + 1, 1);
			if (start > end || start < new DateTime(monthRange[0].Year, (int)monthRange[0].Month + 1, 1) ||
				end > new DateTime(monthRange[^1].Year, (int)monthRange[^1].Month + 1, 1))
				throw new BusinessException("Finance:InvalidDateRange");
			selectedMonths = monthRange.Where(m =>
			{
				var value = new DateTime(m.Year, (int)m.Month + 1, 1);
				return value >= start && value <= end;
			}).ToList();
		}

		var totalAmount = customDetail != null ? customDetail.Amount * selectedMonths.Count : detail.Amount;
		var baseAmount = customDetail != null
			? customDetail.Amount
			: Math.Round(totalAmount / selectedMonths.Count, 2, MidpointRounding.AwayFromZero);
		var allocated = 0m;
		for (var i = 0; i < selectedMonths.Count; i++)
		{
			var scheduledAmount = i == selectedMonths.Count - 1 ? totalAmount - allocated : baseAmount;
			allocated += scheduledAmount;
			dueFees.Add(CreateLegacyDueFee(
				studentId, detail, selectedMonths[i].Month, selectedMonths[i].Year, scheduledAmount,
				prorationPolicy, prorationPolicyVersionId, chargeStartDate, chargeEndDate));
		}

		return dueFees;
	}

	private DueFee CreateLegacyDueFee(
		Guid studentId,
		FeeStructureDetail detail,
		MonthEnum month,
		int year,
		decimal scheduledAmount,
		ProrationPolicySnapshot? prorationPolicy = null,
		Guid? prorationPolicyVersionId = null,
		DateTime? chargeStartDate = null,
		DateTime? chargeEndDate = null)
	{
		var coverageStart = new DateTime(year, (int)month + 1, 1);
		var coverageEnd = coverageStart.AddMonths(1).AddDays(-1);
		var calculation = CalculateProration(
			detail, scheduledAmount, coverageStart, coverageEnd,
			chargeStartDate ?? coverageStart, chargeEndDate ?? coverageEnd, prorationPolicy);
		return new DueFee(
			id: GuidGenerator.Create(), tenantId: CurrentTenant.Id, studentId: studentId,
			feeHeadId: detail.FeeHeadId, month: month, year: year, isOneTime: detail.IsOneTime,
			amount: calculation.ProratedAmount, dueDate: ComputeDefaultDueDate(month, year),
			coverageStartDate: coverageStart, coverageEndDate: coverageEnd,
			scheduledAmount: scheduledAmount,
			prorationPolicyId: prorationPolicy?.PolicyId, prorationPolicyVersionId: prorationPolicyVersionId,
			prorationChargeStartDate: prorationPolicy == null ? null : chargeStartDate,
			prorationChargeEndDate: prorationPolicy == null ? null : chargeEndDate,
			prorationEligibleStartDate: calculation.EligibleStartDate,
			prorationEligibleEndDate: calculation.EligibleEndDate,
			prorationCoverageDays: calculation.CoverageDays,
			prorationEligibleDays: calculation.ActualEligibleDays,
			prorationChargedDays: calculation.ChargedDays,
			prorationFactor: calculation.Factor,
			prorationRuleType: calculation.RuleType,
			prorationRoundingMode: calculation.RoundingMode,
			prorationExplanation: calculation.Explanation);
	}

	private ProrationCalculationResult CalculateProration(
		FeeStructureDetail detail,
		decimal scheduledAmount,
		DateTime coverageStart,
		DateTime coverageEnd,
		DateTime chargeStart,
		DateTime chargeEnd,
		ProrationPolicySnapshot? policy)
	{
		var rule = policy?.Rules.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
			?? new ProrationPolicyRuleSnapshot(
				detail.FeeHeadId, ProrationRuleType.FullCharge,
				ProrationRoundingMode.Currency, null, null, null);
		if (detail.IsOneTime)
			rule = rule with { RuleType = ProrationRuleType.FullCharge };
		return prorationCalculator.Calculate(rule, scheduledAmount, coverageStart, coverageEnd, chargeStart, chargeEnd);
	}

	private static void ValidateChargePeriod(
		DateTime start, DateTime end, AcademicYear academicYear, Guid? feeHeadId = null)
	{
		if (start > end || start < academicYear.StartDate.Date || end > academicYear.EndDate.Date)
		{
			var error = new BusinessException(ProrationPolicyErrorCodes.InvalidChargePeriod);
			if (feeHeadId.HasValue) error.WithData("FeeHeadId", feeHeadId.Value);
			throw error;
		}
	}

	private static List<(MonthEnum Month, int Year)> ComputeMonthRange(AcademicYear academicYear)
	{
		var result = new List<(MonthEnum, int)>();
		var current = new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1);
		var end = new DateTime(academicYear.EndDate.Year, academicYear.EndDate.Month, 1);
		while (current <= end)
		{
			result.Add((ToMonthEnum(current.Month), current.Year));
			current = current.AddMonths(1);
		}
		return result;
	}

	private static MonthEnum ToMonthEnum(int month) => (MonthEnum)(month - 1);
}
