using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeBalanceService
{
	private sealed record AccrualContext(Guid StructureId, Guid VersionId, EffectiveCollectionRules Rules,
		List<DueFee> Dues, Func<DateTime, bool> CanRecover, LateFeeStartBoundary? StartBoundary);

	private sealed record LateFeeStartBoundary(DateTime? Start, Dictionary<Guid, DateTime> HeadStarts)
	{
		public bool Allows(DueFee due) =>
			(!Start.HasValue || due.DueDate.Date >= Start.Value.Date) &&
			(!HeadStarts.TryGetValue(due.FeeHeadId, out var headStart) || due.DueDate.Date >= headStart) &&
			(!due.ProrationChargeStartDate.HasValue || due.DueDate.Date >= due.ProrationChargeStartDate.Value.Date);
	}

	private async Task<List<DueFee>> ReadCompleteScopeAsync(IReadOnlyCollection<DueFee> requested, Guid yearId)
	{
		var requestedById = requested.ToDictionary(x => x.Id);
		var students = requested.Where(x => x.StudentId.HasValue).Select(x => x.StudentId!.Value).Distinct().ToArray();
		var accounts = requested.Where(x => x.FinanceFeeAccountId.HasValue).Select(x => x.FinanceFeeAccountId!.Value).Distinct().ToArray();
		var dues = await dueFeeRepository.GetQueryableAsync();
		var heads = await feeHeadRepository.GetQueryableAsync();
		// DueFee.Year is a calendar year, so academic-year ownership comes from its fee head.
		var complete = await AsyncExecuter.ToListAsync(from due in dues
			join head in heads on due.FeeHeadId equals head.Id
			where head.AcademicYearId == yearId &&
				(due.StudentId.HasValue && Enumerable.Contains(students, due.StudentId.Value) ||
				 due.FinanceFeeAccountId.HasValue && Enumerable.Contains(accounts, due.FinanceFeeAccountId.Value))
			select due);
		if (requestedById.Keys.Except(complete.Select(x => x.Id)).Any())
			throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
		return complete;
	}

	private async Task<LateFeeStartBoundary> ReadApplicableFromAsync(string? snapshot, DateTime? chargeStart,
		Guid? studentId, Guid? applicationId, Guid yearId)
	{
		DateTime? joining = null;
		var headStarts = new Dictionary<Guid, DateTime>();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			try
			{
				using var document = JsonDocument.Parse(snapshot);
				if (document.RootElement.TryGetProperty("RegistrationJoiningDate", out var date) && date.ValueKind != JsonValueKind.Null)
					joining = date.GetDateTime().Date;
				if (document.RootElement.TryGetProperty("ChargeStartDate", out var start) && start.ValueKind != JsonValueKind.Null)
					chargeStart = start.GetDateTime().Date;
				if (document.RootElement.TryGetProperty("Heads", out var heads) && heads.ValueKind != JsonValueKind.Null)
					foreach (var head in heads.EnumerateArray())
					{
						if (!head.TryGetProperty("ServicePeriod", out var period) || period.ValueKind == JsonValueKind.Null ||
							!period.TryGetProperty("StartDate", out var headStart) || headStart.ValueKind == JsonValueKind.Null) continue;
						if (!head.TryGetProperty("FeeHeadId", out var id) || !id.TryGetGuid(out var headId) ||
							!headStarts.TryAdd(headId, headStart.GetDateTime().Date)) throw new JsonException();
					}
			}
			catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
			{
				throw new BusinessException(AssignmentErrorCodes.SourceUnavailable, innerException: error);
			}
		}
		joining ??= await admissionDates.ReadAsync(studentId, applicationId, yearId);
		if (chargeStart == DateTime.MinValue) chargeStart = null;
		var applicableFrom = joining.HasValue && (!chargeStart.HasValue || joining > chargeStart) ? joining : chargeStart?.Date;
		return new(applicableFrom, headStarts);
	}
}
