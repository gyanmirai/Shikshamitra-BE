using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollRunItemBuilder
{
	public static RunPayrollItem BuildPayrollItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		PayrollBasisDaysResult basis,
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		HrResolvedPolicies resolvedPolicies,
		string holidayImpactJson,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyList<string>? additionalIssueCodes = null,
		DateOnly? employmentStartDate = null,
		ShiftManagement? shift = null,
		PayrollStatutoryRuleSet? statutoryRules = null,
		EmployeeCompensationRevision? compensationRevision = null)
	{
		var resolvedShift = shift ?? employee.Shift;
		var itemAttendancePolicyWarnings = new List<string>();
		var issueCodes = (additionalIssueCodes ?? Array.Empty<string>()).ToList();
		var lateArrivalDeductionWarning =
			PayrollLateArrivalDeductionDiagnostics.BuildConfigurationWarning(
				employee,
				attendanceRecords,
				year,
				month,
				resolvedPolicies.AttendancePolicy,
				resolvedShift);
		if (!string.IsNullOrWhiteSpace(lateArrivalDeductionWarning))
		{
			issueCodes.Add(IssueCodes.AttendanceSalaryDeductionConfiguration);
			itemAttendancePolicyWarnings.Add(lateArrivalDeductionWarning);
		}

		var preview = PayrollRunItemPreview.Prepare(
			attendanceRecords,
			components,
			adjustments,
			basis,
			year,
			month,
			holidays,
			leaveUnitsByDate,
			previousMonthNetPay,
			employmentStartDate,
			resolvedShift,
			resolvedPolicies.AttendancePolicy,
			statutoryRules);
		var compensation = BuildCompensationProjection(compensationRevision, components, statutoryRules);
		if (compensation?.HasVariance == true)
		{
			issueCodes.Add(IssueCodes.CompensationTargetVariance);
		}

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = PayrollRunRules.FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)preview.Computation.BasicSalary,
			GrossSalary = (double)preview.Computation.GrossSalary,
			FullGrossSalary = (double)preview.Computation.FullGrossSalary,
			TotalDeduction = (double)preview.Computation.TotalDeduction,
			EmployerContributionTotal = (double)preview.Computation.EmployerContributionTotal,
			EmployerCost = (double)(preview.Computation.TotalEarnings + preview.Computation.EmployerContributionTotal),
			CompensationRevisionId = compensationRevision?.Id,
			CompensationRevisionNumber = compensationRevision?.RevisionNumber,
			CompensationSalaryBasis = compensationRevision == null ? null : ToContractEnum(compensationRevision.SalaryBasis.ToString()),
			CompensationProtectionPolicy = compensationRevision == null ? null : ToContractEnum(compensationRevision.ProtectionPolicy.ToString()),
			CompensationQuotedAmount = compensationRevision == null ? null : (double)compensationRevision.QuotedMonthlyAmount,
			CompensationProtectedAmount = compensationRevision == null ? null : (double)compensationRevision.ProtectedMonthlyAmount,
			CompensationNormalizedProtectedAmount = compensation == null ? null : (double)compensation.NormalizedProtectedAmount,
			CompensationTargetDifference = compensation == null ? null : (double)compensation.Difference,
			NetPay = (double)preview.Computation.NetPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = preview.NetPayDelta.HasValue ? (double)preview.NetPayDelta.Value : null,
			NetPayDeltaPercent = preview.NetPayDeltaPercent.HasValue ? (double)preview.NetPayDeltaPercent.Value : null,
			IssueCodes = issueCodes
				.Concat(preview.Computation.StatutoryIssueCodes)
				.Concat(PayrollIssueCodeBuilder.Build(payrollDetails, components, basis.BasisDays, preview.Computation.PayableDays, preview.Computation.NetPay, previousMonthNetPay, preview.NetPayDeltaPercent, preview.HasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = preview.AttendanceSummary.PresentDays,
			HalfWorkingDays = preview.AttendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)preview.Computation.PayableDays,
			ProrationFactor = (double)preview.Computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = basis.UsedTenantDefault,
			PayrollPolicyId = resolvedPolicies.PayrollPolicy.Id,
			PayrollPolicyVersion = resolvedPolicies.PayrollPolicy.Version,
			AttendancePolicyId = resolvedPolicies.AttendancePolicy.Id,
			AttendancePolicyVersion = resolvedPolicies.AttendancePolicy.Version,
			WorkCalendarPolicyId = resolvedPolicies.PayrollWorkCalendarPolicy.Id,
			WorkCalendarPolicyVersion = resolvedPolicies.PayrollWorkCalendarPolicy.Version,
			HolidayImpactJson = holidayImpactJson,
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)preview.Computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			AttendancePolicyWarnings = itemAttendancePolicyWarnings,
			EarningComponents = preview.Computation.Earnings.Select(PayrollRunProjection.ToComponentItem).ToList(),
			DeductionComponents = preview.Computation.Deductions.Select(PayrollRunProjection.ToComponentItem).ToList(),
			EmployerContributions = preview.Computation.EmployerContributions.Select(PayrollRunProjection.ToEmployerContributionItem).ToList(),
			ProvidentFundRuleId = preview.Computation.ProvidentFundRuleId,
			ProvidentFundRuleVersion = preview.Computation.ProvidentFundRuleVersion,
			ProfessionalTaxRuleId = preview.Computation.ProfessionalTaxRuleId,
			ProfessionalTaxRuleVersion = preview.Computation.ProfessionalTaxRuleVersion,
			EsiConfigurationId = preview.Computation.EsiConfigurationId,
			EmployeeStateInsuranceRuleId = preview.Computation.EmployeeStateInsuranceRuleId,
			EmployeeStateInsuranceRuleVersion = preview.Computation.EmployeeStateInsuranceRuleVersion,
			EmployeeStateInsuranceWages = (double)preview.Computation.EmployeeStateInsuranceWages,
			EmployeeStateInsuranceGovernmentContribution = (double)preview.Computation.EmployeeStateInsuranceGovernmentContribution,
			EmployeeStateInsuranceCoverageDecision = preview.Computation.EmployeeStateInsuranceCoverageDecision,
			EmployeeStateInsuranceNumber = preview.Computation.EmployeeStateInsuranceNumber,
			EmployeeStateInsuranceContributionPeriodStart = preview.Computation.EmployeeStateInsuranceContributionPeriodStart,
			EmployeeStateInsuranceContributionPeriodEnd = preview.Computation.EmployeeStateInsuranceContributionPeriodEnd,
			PayrollAdjustments = adjustments.Select(PayrollRunProjection.ToAdjustmentDto).ToList()
		};
	}

	public static RunPayrollItem BuildPayrollSetupIssueItem(
		Employee employee,
		List<EmployeeAttendance> attendanceRecords,
		EmployeePayrollDetails? payrollDetails,
		List<EmployeePayrollComponent> components,
		List<PayrollAdjustment> adjustments,
		int year,
		int month,
		bool isLocked,
		PayrollRunReview? review,
		decimal? previousMonthNetPay,
		IReadOnlyDictionary<DateOnly, PayrollLeaveUnits>? leaveUnitsByDate,
		IReadOnlyList<string> issueCodes,
		DateOnly? employmentStartDate = null,
		PayrollStatutoryRuleSet? statutoryRules = null,
		EmployeeCompensationRevision? compensationRevision = null)
	{
		var basis = new PayrollBasisDaysResult(
			DateTime.DaysInMonth(year, month),
			DayCalculationType.TotalMonthDays,
			UsedTenantDefault: true,
			SaturdayWorkPolicy.FullDay);
		var preview = PayrollRunItemPreview.Prepare(
			attendanceRecords,
			components,
			adjustments,
			basis,
			year,
			month,
			Array.Empty<Holiday>(),
			leaveUnitsByDate,
			previousMonthNetPay,
			employmentStartDate,
			statutoryRules: statutoryRules);
		var compensation = BuildCompensationProjection(compensationRevision, components, statutoryRules);
		var resolvedIssueCodes = issueCodes.ToList();
		if (compensation?.HasVariance == true)
		{
			resolvedIssueCodes.Add(IssueCodes.CompensationTargetVariance);
		}

		return new RunPayrollItem
		{
			EmployeeId = employee.Id,
			IsLocked = isLocked,
			ReviewStatus = isLocked ? ReviewStatuses.Locked : review == null ? ReviewStatuses.Pending : ReviewStatuses.Reviewed,
			ReviewedByUserId = review?.ReviewedByUserId,
			ReviewedAt = review?.ReviewedAt,
			EmployeeName = PayrollRunRules.FormatEmployeeName(employee),
			EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			BasicSalary = (double)preview.Computation.BasicSalary,
			GrossSalary = (double)preview.Computation.GrossSalary,
			FullGrossSalary = (double)preview.Computation.FullGrossSalary,
			TotalDeduction = (double)preview.Computation.TotalDeduction,
			EmployerContributionTotal = (double)preview.Computation.EmployerContributionTotal,
			EmployerCost = (double)(preview.Computation.TotalEarnings + preview.Computation.EmployerContributionTotal),
			CompensationRevisionId = compensationRevision?.Id,
			CompensationRevisionNumber = compensationRevision?.RevisionNumber,
			CompensationSalaryBasis = compensationRevision == null ? null : ToContractEnum(compensationRevision.SalaryBasis.ToString()),
			CompensationProtectionPolicy = compensationRevision == null ? null : ToContractEnum(compensationRevision.ProtectionPolicy.ToString()),
			CompensationQuotedAmount = compensationRevision == null ? null : (double)compensationRevision.QuotedMonthlyAmount,
			CompensationProtectedAmount = compensationRevision == null ? null : (double)compensationRevision.ProtectedMonthlyAmount,
			CompensationNormalizedProtectedAmount = compensation == null ? null : (double)compensation.NormalizedProtectedAmount,
			CompensationTargetDifference = compensation == null ? null : (double)compensation.Difference,
			NetPay = (double)preview.Computation.NetPay,
			PreviousMonthNetPay = previousMonthNetPay.HasValue ? (double)previousMonthNetPay.Value : null,
			NetPayDelta = preview.NetPayDelta.HasValue ? (double)preview.NetPayDelta.Value : null,
			NetPayDeltaPercent = preview.NetPayDeltaPercent.HasValue ? (double)preview.NetPayDeltaPercent.Value : null,
			IssueCodes = resolvedIssueCodes
				.Concat(preview.Computation.StatutoryIssueCodes)
				.Concat(PayrollIssueCodeBuilder.Build(payrollDetails, components, basis.BasisDays, preview.Computation.PayableDays, preview.Computation.NetPay, previousMonthNetPay, preview.NetPayDeltaPercent, preview.HasAttendanceOrPaidLeave))
				.Distinct()
				.ToList(),
			PresentDays = preview.AttendanceSummary.PresentDays,
			HalfWorkingDays = preview.AttendanceSummary.HalfWorkingDays,
			TotalWorkingDays = (double)basis.BasisDays,
			PayableDays = (double)preview.Computation.PayableDays,
			ProrationFactor = (double)preview.Computation.ProrationFactor,
			DayCalculationMode = PayrollDayCalculationApiLabels.ToApiString(basis.EffectiveMode),
			UsedDefaultDayCalculation = true,
			HolidayImpactJson = "[]",
			BasisDays = (double)basis.BasisDays,
			PayableUnits = (double)preview.Computation.PayableDays,
			PayrollAttendanceSource = "raw_attendance",
			EarningComponents = preview.Computation.Earnings.Select(PayrollRunProjection.ToComponentItem).ToList(),
			DeductionComponents = preview.Computation.Deductions.Select(PayrollRunProjection.ToComponentItem).ToList(),
			EmployerContributions = preview.Computation.EmployerContributions.Select(PayrollRunProjection.ToEmployerContributionItem).ToList(),
			ProvidentFundRuleId = preview.Computation.ProvidentFundRuleId,
			ProvidentFundRuleVersion = preview.Computation.ProvidentFundRuleVersion,
			ProfessionalTaxRuleId = preview.Computation.ProfessionalTaxRuleId,
			ProfessionalTaxRuleVersion = preview.Computation.ProfessionalTaxRuleVersion,
			EsiConfigurationId = preview.Computation.EsiConfigurationId,
			EmployeeStateInsuranceRuleId = preview.Computation.EmployeeStateInsuranceRuleId,
			EmployeeStateInsuranceRuleVersion = preview.Computation.EmployeeStateInsuranceRuleVersion,
			EmployeeStateInsuranceWages = (double)preview.Computation.EmployeeStateInsuranceWages,
			EmployeeStateInsuranceGovernmentContribution = (double)preview.Computation.EmployeeStateInsuranceGovernmentContribution,
			EmployeeStateInsuranceCoverageDecision = preview.Computation.EmployeeStateInsuranceCoverageDecision,
			EmployeeStateInsuranceNumber = preview.Computation.EmployeeStateInsuranceNumber,
			EmployeeStateInsuranceContributionPeriodStart = preview.Computation.EmployeeStateInsuranceContributionPeriodStart,
			EmployeeStateInsuranceContributionPeriodEnd = preview.Computation.EmployeeStateInsuranceContributionPeriodEnd,
			PayrollAdjustments = adjustments.Select(PayrollRunProjection.ToAdjustmentDto).ToList()
		};
	}

	public static string BuildHolidayImpactJson(
		int year,
		int month,
		IReadOnlyList<Holiday> holidays,
		IReadOnlyList<PayrollHolidayOverride> holidayOverrides)
	{
		var items = PayrollMonthCalendar.GetResolvedHolidayDaysInMonth(year, month, holidays, holidayOverrides)
			.Select(x => new
			{
				date = x.Date.ToString("yyyy-MM-dd"),
				calendarHolidayId = x.CalendarHolidayId,
				holidayName = x.HolidayName,
				payrollImpact = "excluded",
				x.IsPayrollOnly
			})
			.ToList();

		return JsonSerializer.Serialize(items);
	}

	private static CompensationProjection? BuildCompensationProjection(
		EmployeeCompensationRevision? revision,
		IReadOnlyList<EmployeePayrollComponent> components,
		PayrollStatutoryRuleSet? statutoryRules)
	{
		if (revision == null)
		{
			return null;
		}

		var recurringComponents = components.Where(x => !x.IsTemp).ToList();
		var normalized = PayrollComputation.Calculate(
			presentDays: 1,
			halfWorkingDays: 0,
			payableDays: 1m,
			basisDays: 1m,
			recurringComponents,
			statutoryRules);
		var normalizedProtectedAmount = revision.ProtectionPolicy switch
		{
			ShikshaMitra.HR.Payroll.Templates.CompensationProtectionPolicy.ProtectTakeHome => normalized.NetPay,
			ShikshaMitra.HR.Payroll.Templates.CompensationProtectionPolicy.ProtectEmployerCost => normalized.GrossSalary + normalized.EmployerContributionTotal,
			_ => normalized.GrossSalary
		};
		var difference = Math.Round(
			normalizedProtectedAmount - revision.ProtectedMonthlyAmount,
			2,
			MidpointRounding.AwayFromZero);
		return new CompensationProjection(normalizedProtectedAmount, difference, Math.Abs(difference) > 0.01m);
	}

	private static string ToContractEnum(string value) =>
		string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? "_" + character : character.ToString()))
			.ToUpperInvariant();

	private sealed record CompensationProjection(
		decimal NormalizedProtectedAmount,
		decimal Difference,
		bool HasVariance);
}
