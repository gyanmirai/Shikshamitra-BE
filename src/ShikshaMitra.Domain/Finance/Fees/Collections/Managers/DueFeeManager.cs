using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class DueFeeManager(IDueFeeRepository dueFeeRepository) : DomainService
{
	public async Task<IReadOnlyList<DueFee>> GenerateForStudentAsync(
		Guid studentId,
		FeeStructure feeStructure,
		AcademicYear academicYear,
		IReadOnlyList<Guid> selectedOptionalFeeHeadIds,
		IReadOnlyList<CustomOptionalFeeHeadDetail> optionalFeeHeadDetails = null)
	{
		// ── Guard: idempotency ────────────────────────────────────────────────
		var alreadyExists = await dueFeeRepository.ExistsForStudentAsync(studentId);
		if (alreadyExists)
			throw new BusinessException(DueFeeErrorCodes.DueFeesAlreadyGenerated)
				.WithData("StudentId", studentId);

		// ── Compute academic-year month range ────────────────────────────────
		var monthRange = ComputeMonthRange(academicYear);

		// ── Generate DueFee records ──────────────────────────────────────────
		var dueFees = new List<DueFee>();

		foreach (var detail in feeStructure.FeeStructureDetails)
		{
			// Skip optional fee heads that the student did not select
			if (detail.IsOptional && !selectedOptionalFeeHeadIds.Contains(detail.FeeHeadId))
				continue;

			var customDetail = detail.IsOptional
				? optionalFeeHeadDetails?.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
				: null;

			if (detail.IsOneTime)
			{
				// One-time fee: use PayableMonth/Year from the fee structure detail when
				// explicitly configured; fall back to the first month of the academic year.
				MonthEnum month;
				int year;
				double amount;

				if (customDetail != null)
				{
					month = customDetail.PayableMonth ?? (detail.PayableMonth.HasValue ? detail.PayableMonth.Value : monthRange[0].Month);
					year = customDetail.PayableYear ?? (detail.PayableYear.HasValue ? detail.PayableYear.Value : monthRange[0].Year);
					amount = customDetail.Amount;

					// Validate that selected month-year is within academic year monthRange
					if (!monthRange.Any(m => m.Month == month && m.Year == year))
					{
						throw new BusinessException("Finance:OutofAcademicYearRange", $"Selected payable month '{month}-{year}' is outside the academic year range.")
							.WithData("Month", month.ToString())
							.WithData("Year", year);
					}
				}
				else
				{
					(month, year) = detail.PayableMonth.HasValue && detail.PayableYear.HasValue
						? (detail.PayableMonth.Value, detail.PayableYear.Value)
						: monthRange[0];
					amount = detail.Amount;
				}

				dueFees.Add(CreateDueFee(studentId, detail, month, year, amount));
			}
			else
			{
				// Recurring fee
				if (customDetail != null && customDetail.StartPayableMonth.HasValue && customDetail.StartPayableYear.HasValue && customDetail.EndPayableMonth.HasValue && customDetail.EndPayableYear.HasValue)
				{
					var startMonth = customDetail.StartPayableMonth.Value;
					var startYear = customDetail.StartPayableYear.Value;
					var endMonth = customDetail.EndPayableMonth.Value;
					var endYear = customDetail.EndPayableYear.Value;

					// Validate selected start and end months are within academic year monthRange
					if (!monthRange.Any(m => m.Month == startMonth && m.Year == startYear))
					{
						throw new BusinessException("Finance:OutofAcademicYearRange", $"Selected start month '{startMonth}-{startYear}' is outside the academic year range.")
							.WithData("Month", startMonth.ToString())
							.WithData("Year", startYear);
					}

					if (!monthRange.Any(m => m.Month == endMonth && m.Year == endYear))
					{
						throw new BusinessException("Finance:OutofAcademicYearRange", $"Selected end month '{endMonth}-{endYear}' is outside the academic year range.")
							.WithData("Month", endMonth.ToString())
							.WithData("Year", endYear);
					}

					var startDate = new DateTime(startYear, (int)startMonth + 1, 1);
					var endDate = new DateTime(endYear, (int)endMonth + 1, 1);

					if (startDate > endDate)
					{
						throw new BusinessException("Finance:InvalidDateRange", "Start month cannot be after end month.");
					}

					// Filter the range of months
					var customMonths = monthRange.Where(m => {
						var mDate = new DateTime(m.Year, (int)m.Month + 1, 1);
						return mDate >= startDate && mDate <= endDate;
					}).ToList();

					foreach (var m in customMonths)
					{
						dueFees.Add(CreateDueFee(studentId, detail, m.Month, m.Year, customDetail.Amount));
					}
				}
				else
				{
					// Standard logic: one DueFee per month in the academic year — PayableMonth/Year
					// on the detail is intentionally ignored for recurring fees.
					var months = monthRange.ToList();
					var count = months.Count;
					var floorAmount = Math.Floor(detail.Amount / count);
					var remainder = (int)(detail.Amount - floorAmount * count);

					for (int i = 0; i < count; i++)
					{
						var feeAmount = i < remainder ? floorAmount + 1 : floorAmount;
						dueFees.Add(CreateDueFee(studentId, detail, months[i].Month, months[i].Year, feeAmount));
					}
				}
			}
		}

		return dueFees;
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	private DueFee CreateDueFee(
		Guid studentId,
		FeeStructureDetail detail,
		MonthEnum month,
		int year,
		double amount) => new DueFee(
			id: GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			studentId: studentId,
			feeHeadId: detail.FeeHeadId,
			month: month,
			year: year,
			isOneTime: detail.IsOneTime,
			amount: amount);

	/// <summary>
	/// Enumerates every (MonthEnum, Year) pair from <paramref name="academicYear"/>.StartDate
	/// to <paramref name="academicYear"/>.EndDate inclusive, month by month.
	/// </summary>
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

	/// <summary>
	/// Maps a 1-based <see cref="DateTime.Month"/> integer to the 0-based <see cref="MonthEnum"/>.
	/// </summary>
	private static MonthEnum ToMonthEnum(int month) => (MonthEnum)(month - 1);
}
