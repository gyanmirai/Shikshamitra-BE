using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructure
{
	private const string OptionalServiceEndDatePrefix = "OptionalServiceEndDate.";
	private const string OptionalServicePeriodsPrefix = "OptionalServicePeriods.";

	public IReadOnlyList<OptionalFeeServicePeriod> GetOptionalServicePeriods(Guid feeHeadId) =>
		this.GetProperty<string>(OptionalServicePeriodsPrefix + feeHeadId.ToString("N")) is { } json
			? JsonSerializer.Deserialize<List<OptionalFeeServicePeriod>>(json) ?? []
			: [];

	private void SaveOptionalServicePeriods(Guid feeHeadId, IEnumerable<OptionalFeeServicePeriod> periods) =>
		this.SetProperty(OptionalServicePeriodsPrefix + feeHeadId.ToString("N"), JsonSerializer.Serialize(periods));

	public void AddOptionalServicePeriod(Guid feeHeadId, OptionalFeeServicePeriod period)
	{
		var history = GetOptionalServicePeriods(feeHeadId);
		if (history.LastOrDefault() is { } last &&
			(!last.CancellationEffectiveDate.HasValue || period.StartDate < last.CancellationEffectiveDate.Value))
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceRestartUnavailable);
		SaveOptionalServicePeriods(feeHeadId, history.Append(period));
		if (_includedFeeHeadIds != null && !_includedFeeHeadIds.Contains(feeHeadId))
			IncludedFeeHeadIds = _includedFeeHeadIds.Append(feeHeadId).Order().ToArray();
	}

	public DateOnly? GetOptionalServiceEndDate(Guid feeHeadId)
	{
		if (GetOptionalServicePeriods(feeHeadId).LastOrDefault() is { } latest)
			return latest.CancellationEffectiveDate;
		return GetOriginalOptionalServiceEndDate(feeHeadId);
	}

	public DateOnly? GetOriginalOptionalServiceEndDate(Guid feeHeadId)
	{
		var stored = this.GetProperty<DateTime?>(OptionalServiceEndDatePrefix + feeHeadId.ToString("N"));
		return stored.HasValue ? DateOnly.FromDateTime(stored.Value) : null;
	}

	public bool HasOptionalServiceEndDates() =>
		HasOptionalServicePeriods() || ExtraProperties.Keys.Any(key => key.StartsWith(OptionalServiceEndDatePrefix, StringComparison.Ordinal));

	public bool HasOptionalServicePeriods() =>
		ExtraProperties.Keys.Any(key => key.StartsWith(OptionalServicePeriodsPrefix, StringComparison.Ordinal));

	public void RecordOptionalServiceEndDate(Guid feeHeadId, DateOnly effectiveDate)
	{
		var history = GetOptionalServicePeriods(feeHeadId);
		if (history.LastOrDefault() is { } latest)
		{
			SaveOptionalServicePeriods(feeHeadId, history.Take(history.Count - 1)
				.Append(latest with { CancellationEffectiveDate = effectiveDate }));
			return;
		}
		this.SetProperty(OptionalServiceEndDatePrefix + feeHeadId.ToString("N"),
			effectiveDate.ToDateTime(TimeOnly.MinValue));
	}
}

public sealed record OptionalFeeServicePeriod(Guid Id, DateOnly StartDate, DateOnly EndDate,
	DateOnly? CancellationEffectiveDate, decimal MonthlyRate, Guid ProrationPolicyVersionId,
	string ProrationPolicyName, int ProrationPolicyVersion, Guid PaymentTermVersionId, decimal AssignedAmount, Guid[] DueIds);
