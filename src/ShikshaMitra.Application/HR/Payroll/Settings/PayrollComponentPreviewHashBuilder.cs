using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShikshaMitra.HR.Payroll.Settings;

internal static class PayrollComponentPreviewHashBuilder
{
	public static string BuildPreviewHash(
		Guid componentId,
		UpdateEarningComponentDto input,
		string effectiveMonth,
		int syncedCount,
		int overrideCount,
		int finalizedSlipCount,
		int draftSlipCount)
	{
		var raw = string.Join("|",
			componentId,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedEarningComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			effectiveMonth,
			syncedCount,
			overrideCount,
			finalizedSlipCount,
			draftSlipCount);
		return Hash(raw);
	}

	public static string BuildPreviewHash(
		Guid componentId,
		UpdateDeductionComponentDto input,
		string effectiveMonth,
		int syncedCount,
		int overrideCount,
		int finalizedSlipCount,
		int draftSlipCount)
	{
		var raw = string.Join("|",
			componentId,
			input.Name,
			input.Description,
			input.ComponentType,
			input.CalculationType,
			input.Amount,
			input.Percentage,
			input.SelectedDeductionComponentId,
			input.DependOn,
			input.VisibleInPayslip,
			effectiveMonth,
			syncedCount,
			overrideCount,
			finalizedSlipCount,
			draftSlipCount);
		return Hash(raw);
	}

	public static string BuildDeletePreviewHash(
		Guid componentId,
		IReadOnlyCollection<Guid> employeeIds,
		int syncedCount,
		int overrideCount,
		int finalizedSlipCount,
		int draftSlipCount)
	{
		var orderedEmployeeIds = string.Join(",", employeeIds.OrderBy(x => x));
		var raw = string.Join("|",
			"delete",
			componentId,
			orderedEmployeeIds,
			employeeIds.Count,
			syncedCount,
			overrideCount,
			finalizedSlipCount,
			draftSlipCount);
		return Hash(raw);
	}

	private static string Hash(string raw) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
