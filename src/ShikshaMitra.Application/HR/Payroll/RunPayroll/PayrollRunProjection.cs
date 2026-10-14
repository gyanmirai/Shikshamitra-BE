using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollRunProjection
{
	public static bool SnapshotValuesChanged(
		PayrollRunItemSnapshot snapshot,
		RunPayrollItem item,
		string issuesJson,
		string attendancePolicyWarningsJson,
		string earningsJson,
		string deductionsJson,
		string employerContributionsJson,
		string adjustmentsJson) =>
		snapshot.BasicSalary != (decimal)item.BasicSalary ||
		snapshot.GrossSalary != (decimal)item.GrossSalary ||
		snapshot.FullGrossSalary != (decimal)item.FullGrossSalary ||
		snapshot.TotalDeduction != (decimal)item.TotalDeduction ||
		snapshot.EmployerContributionTotal != (decimal)item.EmployerContributionTotal ||
		snapshot.EmployerCost != (decimal)item.EmployerCost ||
		snapshot.CompensationRevisionId != item.CompensationRevisionId ||
		snapshot.CompensationRevisionNumber != item.CompensationRevisionNumber ||
		snapshot.CompensationSalaryBasis != item.CompensationSalaryBasis ||
		snapshot.CompensationProtectionPolicy != item.CompensationProtectionPolicy ||
		snapshot.CompensationQuotedAmount != ToNullableDecimal(item.CompensationQuotedAmount) ||
		snapshot.CompensationProtectedAmount != ToNullableDecimal(item.CompensationProtectedAmount) ||
		snapshot.CompensationNormalizedProtectedAmount != ToNullableDecimal(item.CompensationNormalizedProtectedAmount) ||
		snapshot.CompensationTargetDifference != ToNullableDecimal(item.CompensationTargetDifference) ||
		snapshot.NetPay != (decimal)item.NetPay ||
		snapshot.PresentDays != (decimal)item.PresentDays ||
		snapshot.HalfWorkingDays != (decimal)item.HalfWorkingDays ||
		snapshot.TotalWorkingDays != (decimal)item.TotalWorkingDays ||
		snapshot.PayableDays != (decimal)item.PayableDays ||
		snapshot.ProrationFactor != (decimal)item.ProrationFactor ||
		snapshot.DayCalculationMode != item.DayCalculationMode ||
		snapshot.PayrollPolicyId != item.PayrollPolicyId ||
		snapshot.PayrollPolicyVersion != item.PayrollPolicyVersion ||
		snapshot.AttendancePolicyId != item.AttendancePolicyId ||
		snapshot.AttendancePolicyVersion != item.AttendancePolicyVersion ||
		snapshot.WorkCalendarPolicyId != item.WorkCalendarPolicyId ||
		snapshot.WorkCalendarPolicyVersion != item.WorkCalendarPolicyVersion ||
		snapshot.BasisDays != (decimal)item.BasisDays ||
		snapshot.PayableUnits != (decimal)item.PayableUnits ||
		snapshot.IssueCodesJson != issuesJson ||
		snapshot.AttendancePolicyWarningsJson != attendancePolicyWarningsJson ||
		snapshot.EarningComponentsJson != earningsJson ||
		snapshot.DeductionComponentsJson != deductionsJson ||
		snapshot.EmployerContributionsJson != employerContributionsJson ||
		snapshot.ProvidentFundRuleId != item.ProvidentFundRuleId ||
		snapshot.ProvidentFundRuleVersion != item.ProvidentFundRuleVersion ||
		snapshot.ProfessionalTaxRuleId != item.ProfessionalTaxRuleId ||
		snapshot.ProfessionalTaxRuleVersion != item.ProfessionalTaxRuleVersion ||
		snapshot.EsiCalculationDetailsJson != item.EsiCalculationDetailsJson ||
		snapshot.PfEligibilityDetailsJson != item.PfEligibilityDetailsJson ||
		snapshot.StatutoryApplicationJson != item.StatutoryApplicationJson ||
		snapshot.LeaveUnitsByTypeJson != item.LeaveUnitsByTypeJson ||
		snapshot.EsiConfigurationId != item.EsiConfigurationId ||
		snapshot.EmployeeStateInsuranceRuleId != item.EmployeeStateInsuranceRuleId ||
		snapshot.EmployeeStateInsuranceRuleVersion != item.EmployeeStateInsuranceRuleVersion ||
		snapshot.EmployeeStateInsuranceWages != (decimal)item.EmployeeStateInsuranceWages ||
		snapshot.EmployeeStateInsuranceGovernmentContribution != (decimal)item.EmployeeStateInsuranceGovernmentContribution ||
		snapshot.EmployeeStateInsuranceCoverageDecision != item.EmployeeStateInsuranceCoverageDecision ||
		snapshot.EmployeeStateInsuranceNumber != item.EmployeeStateInsuranceNumber ||
		snapshot.EmployeeStateInsuranceContributionPeriodStart != item.EmployeeStateInsuranceContributionPeriodStart ||
		snapshot.EmployeeStateInsuranceContributionPeriodEnd != item.EmployeeStateInsuranceContributionPeriodEnd ||
		snapshot.PayrollAdjustmentsJson != adjustmentsJson;

	private static decimal? ToNullableDecimal(double? value) => value.HasValue ? (decimal)value.Value : null;

	public static void ApplySnapshotValues(
		PayrollRunItemSnapshot snapshot,
		RunPayrollItem item,
		string issuesJson,
		string attendancePolicyWarningsJson,
		string earningsJson,
		string deductionsJson,
		string employerContributionsJson,
		string adjustmentsJson)
	{
		snapshot.IsLocked = item.IsLocked;
		snapshot.ReviewStatus = item.ReviewStatus;
		snapshot.ReviewedByUserId = item.ReviewedByUserId;
		snapshot.ReviewedAt = item.ReviewedAt;
		snapshot.EmployeeName = item.EmployeeName;
		snapshot.EmployeeIdentifier = item.EmployeeIdentifier;
		snapshot.IsTeaching = item.IsTeaching;
		snapshot.BasicSalary = (decimal)item.BasicSalary;
		snapshot.GrossSalary = (decimal)item.GrossSalary;
		snapshot.FullGrossSalary = (decimal)item.FullGrossSalary;
		snapshot.TotalDeduction = (decimal)item.TotalDeduction;
		snapshot.EmployerContributionTotal = (decimal)item.EmployerContributionTotal;
		snapshot.EmployerCost = (decimal)item.EmployerCost;
		snapshot.CompensationRevisionId = item.CompensationRevisionId;
		snapshot.CompensationRevisionNumber = item.CompensationRevisionNumber;
		snapshot.CompensationSalaryBasis = item.CompensationSalaryBasis;
		snapshot.CompensationProtectionPolicy = item.CompensationProtectionPolicy;
		snapshot.CompensationQuotedAmount = item.CompensationQuotedAmount.HasValue ? (decimal)item.CompensationQuotedAmount.Value : null;
		snapshot.CompensationProtectedAmount = item.CompensationProtectedAmount.HasValue ? (decimal)item.CompensationProtectedAmount.Value : null;
		snapshot.CompensationNormalizedProtectedAmount = item.CompensationNormalizedProtectedAmount.HasValue ? (decimal)item.CompensationNormalizedProtectedAmount.Value : null;
		snapshot.CompensationTargetDifference = item.CompensationTargetDifference.HasValue ? (decimal)item.CompensationTargetDifference.Value : null;
		snapshot.NetPay = (decimal)item.NetPay;
		snapshot.PreviousMonthNetPay = item.PreviousMonthNetPay.HasValue ? (decimal)item.PreviousMonthNetPay.Value : null;
		snapshot.NetPayDelta = item.NetPayDelta.HasValue ? (decimal)item.NetPayDelta.Value : null;
		snapshot.NetPayDeltaPercent = item.NetPayDeltaPercent.HasValue ? (decimal)item.NetPayDeltaPercent.Value : null;
		snapshot.IssueCodesJson = issuesJson;
		snapshot.PresentDays = (decimal)item.PresentDays;
		snapshot.HalfWorkingDays = (decimal)item.HalfWorkingDays;
		snapshot.TotalWorkingDays = (decimal)item.TotalWorkingDays;
		snapshot.PayableDays = (decimal)item.PayableDays;
		snapshot.ProrationFactor = (decimal)item.ProrationFactor;
		snapshot.DayCalculationMode = item.DayCalculationMode;
		snapshot.UsedDefaultDayCalculation = item.UsedDefaultDayCalculation;
		snapshot.PayrollPolicyId = item.PayrollPolicyId;
		snapshot.PayrollPolicyVersion = item.PayrollPolicyVersion;
		snapshot.AttendancePolicyId = item.AttendancePolicyId;
		snapshot.AttendancePolicyVersion = item.AttendancePolicyVersion;
		snapshot.WorkCalendarPolicyId = item.WorkCalendarPolicyId;
		snapshot.WorkCalendarPolicyVersion = item.WorkCalendarPolicyVersion;
		snapshot.HolidayImpactJson = item.HolidayImpactJson;
		snapshot.BasisDays = (decimal)item.BasisDays;
		snapshot.PayableUnits = (decimal)item.PayableUnits;
		snapshot.PayrollAttendanceSource = item.PayrollAttendanceSource;
		snapshot.AttendancePolicyWarningsJson = attendancePolicyWarningsJson;
		snapshot.EarningComponentsJson = earningsJson;
		snapshot.DeductionComponentsJson = deductionsJson;
		snapshot.EmployerContributionsJson = employerContributionsJson;
		snapshot.ProvidentFundRuleId = item.ProvidentFundRuleId;
		snapshot.ProvidentFundRuleVersion = item.ProvidentFundRuleVersion;
		snapshot.ProfessionalTaxRuleId = item.ProfessionalTaxRuleId;
		snapshot.ProfessionalTaxRuleVersion = item.ProfessionalTaxRuleVersion;
		snapshot.EsiCalculationDetailsJson = item.EsiCalculationDetailsJson;
		snapshot.PfEligibilityDetailsJson = item.PfEligibilityDetailsJson;
		snapshot.StatutoryApplicationJson = item.StatutoryApplicationJson;
		snapshot.LeaveUnitsByTypeJson = item.LeaveUnitsByTypeJson;
		snapshot.EsiConfigurationId = item.EsiConfigurationId;
		snapshot.EmployeeStateInsuranceRuleId = item.EmployeeStateInsuranceRuleId;
		snapshot.EmployeeStateInsuranceRuleVersion = item.EmployeeStateInsuranceRuleVersion;
		snapshot.EmployeeStateInsuranceWages = (decimal)item.EmployeeStateInsuranceWages;
		snapshot.EmployeeStateInsuranceGovernmentContribution = (decimal)item.EmployeeStateInsuranceGovernmentContribution;
		snapshot.EmployeeStateInsuranceCoverageDecision = item.EmployeeStateInsuranceCoverageDecision;
		snapshot.EmployeeStateInsuranceNumber = item.EmployeeStateInsuranceNumber;
		snapshot.EmployeeStateInsuranceContributionPeriodStart = item.EmployeeStateInsuranceContributionPeriodStart;
		snapshot.EmployeeStateInsuranceContributionPeriodEnd = item.EmployeeStateInsuranceContributionPeriodEnd;
		snapshot.PayrollAdjustmentsJson = adjustmentsJson;
	}

	public static RunPayrollItem ToItem(PayrollRunItemSnapshot row) =>
		new()
		{
			EmployeeId = row.EmployeeId,
			IsLocked = row.IsLocked,
			ReviewStatus = row.ReviewStatus,
			ReviewedByUserId = row.ReviewedByUserId,
			ReviewedAt = row.ReviewedAt,
			EmployeeName = row.EmployeeName,
			EmployeeIdentifier = row.EmployeeIdentifier,
			IsTeaching = row.IsTeaching,
			BasicSalary = (double)row.BasicSalary,
			GrossSalary = (double)row.GrossSalary,
			FullGrossSalary = (double)row.FullGrossSalary,
			TotalDeduction = (double)row.TotalDeduction,
			EmployerContributionTotal = (double)row.EmployerContributionTotal,
			EmployerCost = (double)row.EmployerCost,
			CompensationRevisionId = row.CompensationRevisionId,
			CompensationRevisionNumber = row.CompensationRevisionNumber,
			CompensationSalaryBasis = row.CompensationSalaryBasis,
			CompensationProtectionPolicy = row.CompensationProtectionPolicy,
			CompensationQuotedAmount = row.CompensationQuotedAmount.HasValue ? (double)row.CompensationQuotedAmount.Value : null,
			CompensationProtectedAmount = row.CompensationProtectedAmount.HasValue ? (double)row.CompensationProtectedAmount.Value : null,
			CompensationNormalizedProtectedAmount = row.CompensationNormalizedProtectedAmount.HasValue ? (double)row.CompensationNormalizedProtectedAmount.Value : null,
			CompensationTargetDifference = row.CompensationTargetDifference.HasValue ? (double)row.CompensationTargetDifference.Value : null,
			NetPay = (double)row.NetPay,
			PreviousMonthNetPay = row.PreviousMonthNetPay.HasValue ? (double)row.PreviousMonthNetPay.Value : null,
			NetPayDelta = row.NetPayDelta.HasValue ? (double)row.NetPayDelta.Value : null,
			NetPayDeltaPercent = row.NetPayDeltaPercent.HasValue ? (double)row.NetPayDeltaPercent.Value : null,
			IssueCodes = JsonSerializer.Deserialize<List<string>>(row.IssueCodesJson) ?? new List<string>(),
			PresentDays = (double)row.PresentDays,
			HalfWorkingDays = (double)row.HalfWorkingDays,
			TotalWorkingDays = (double)row.TotalWorkingDays,
			PayableDays = (double)row.PayableDays,
			ProrationFactor = (double)row.ProrationFactor,
			DayCalculationMode = row.DayCalculationMode,
			UsedDefaultDayCalculation = row.UsedDefaultDayCalculation,
			PayrollPolicyId = row.PayrollPolicyId,
			PayrollPolicyVersion = row.PayrollPolicyVersion,
			AttendancePolicyId = row.AttendancePolicyId,
			AttendancePolicyVersion = row.AttendancePolicyVersion,
			WorkCalendarPolicyId = row.WorkCalendarPolicyId,
			WorkCalendarPolicyVersion = row.WorkCalendarPolicyVersion,
			HolidayImpactJson = row.HolidayImpactJson,
			BasisDays = (double)row.BasisDays,
			PayableUnits = (double)row.PayableUnits,
			PayrollAttendanceSource = row.PayrollAttendanceSource,
			AttendancePolicyWarnings = JsonSerializer.Deserialize<List<string>>(row.AttendancePolicyWarningsJson)
				?? new List<string>(),
			EarningComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.EarningComponentsJson)
				?? new List<RunPayrollComponentItem>(),
			DeductionComponents = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.DeductionComponentsJson)
				?? new List<RunPayrollComponentItem>(),
			EmployerContributions = JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(row.EmployerContributionsJson)
				?? new List<RunPayrollComponentItem>(),
			ProvidentFundRuleId = row.ProvidentFundRuleId,
			ProvidentFundRuleVersion = row.ProvidentFundRuleVersion,
			ProfessionalTaxRuleId = row.ProfessionalTaxRuleId,
			ProfessionalTaxRuleVersion = row.ProfessionalTaxRuleVersion,
			EsiCalculationDetailsJson = row.EsiCalculationDetailsJson, PfEligibilityDetailsJson = row.PfEligibilityDetailsJson, StatutoryApplicationJson = row.StatutoryApplicationJson, LeaveUnitsByTypeJson = row.LeaveUnitsByTypeJson,
			EsiConfigurationId = row.EsiConfigurationId,
			EmployeeStateInsuranceRuleId = row.EmployeeStateInsuranceRuleId,
			EmployeeStateInsuranceRuleVersion = row.EmployeeStateInsuranceRuleVersion,
			EmployeeStateInsuranceWages = (double)row.EmployeeStateInsuranceWages,
			EmployeeStateInsuranceGovernmentContribution = (double)row.EmployeeStateInsuranceGovernmentContribution,
			EmployeeStateInsuranceCoverageDecision = row.EmployeeStateInsuranceCoverageDecision,
			EmployeeStateInsuranceNumber = row.EmployeeStateInsuranceNumber,
			EmployeeStateInsuranceContributionPeriodStart = row.EmployeeStateInsuranceContributionPeriodStart,
			EmployeeStateInsuranceContributionPeriodEnd = row.EmployeeStateInsuranceContributionPeriodEnd,
			PayrollAdjustments = JsonSerializer.Deserialize<List<PayrollAdjustmentDto>>(row.PayrollAdjustmentsJson)
				?? new List<PayrollAdjustmentDto>()
		};

	public static RunPayrollComponentItem ToComponentItem(PayrollComputedLineItem item) =>
		new()
		{
			SemanticType = item.Source.SemanticType.ToString(),
			Name = item.Source.Name,
			Amount = (double)item.Amount,
			FullMonthAmount = item.Source.IsTemp ? null : item.Source.Amount,
			FormulaType = item.Source.Formula?.Type.ToString(),
			FormulaValue = item.Source.Formula?.Value,
			BasisComponentId = item.Source.Formula?.BasisComponentId,
			Description = item.Source.Description,
			IsFixed = item.Source.IsFixed,
			IsTemp = item.Source.IsTemp,
			ComponentType = item.Source.ComponentType.ToString(),
			DependOn = (item.Source.DependOn ?? SalaryComponentDependency.None).ToString()
		};

	public static RunPayrollComponentItem ToEmployerContributionItem(PayrollComputedEmployerContribution item) =>
		new()
		{
			Name = item.Name,
			Amount = (double)item.Amount,
			Description = $"Statutory rule version {item.RuleVersion}",
			IsFixed = true,
			IsTemp = false,
			ComponentType = "EmployerContribution",
			DependOn = "Basic"
		};

	public static EmployeePayrollComponent ToPayrollComponent(PayrollAdjustment adjustment) =>
		new(
			adjustment.Id,
			adjustment.EmployeeId,
			adjustment.Name,
			componentId: null,
			adjustment.Description ?? adjustment.Reason,
			adjustment.Amount,
			isFixed: true,
			isTemp: true,
			applicableMonth: DateTime.Parse($"{adjustment.MonthYear}-01"),
			SalaryComponentType.Fixed,
			SalaryComponentDependency.None,
			adjustment.IsEarning,
			usesMasterValue: false);

	public static PayrollAdjustmentDto ToAdjustmentDto(PayrollAdjustment adjustment) =>
		new()
		{
			Id = adjustment.Id,
			MonthYear = adjustment.MonthYear,
			EmployeeId = adjustment.EmployeeId,
			Name = adjustment.Name,
			Amount = (double)adjustment.Amount,
			IsEarning = adjustment.IsEarning,
			Reason = adjustment.Reason,
			Description = adjustment.Description,
			SourceType = adjustment.SourceType,
			SourceId = adjustment.SourceId,
			IsSystemGenerated = adjustment.IsSystemGenerated,
			CreationTime = adjustment.CreationTime,
			CreatorId = adjustment.CreatorId
		};
}
