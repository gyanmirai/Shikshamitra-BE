using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
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
	PaymentTermScheduleResolver scheduleResolver
) : DomainService
{
	/// <summary>
	/// Day-of-month a fee obligation becomes due, absent any real per-school configuration
	/// (none exists yet — see collection-policy-implementation-plan.md Open Questions). Kept
	/// as one named constant so every DueFee-creation call site computes the same convention;
	/// changing this later is a one-line edit, not a hunt across the codebase.
	/// </summary>
	public const int DefaultDueDayOfMonth = 10;

	/// <summary>
	/// Computes the due date for a fee obligation covering <paramref name="month"/>/<paramref name="year"/>,
	/// using <see cref="DefaultDueDayOfMonth"/>. The single canonical place this is computed —
	/// DueFeeManager.GenerateForStudentAsync and every seeder that hand-builds DueFee rows call
	/// this rather than each picking their own convention.
	/// </summary>
	public static DateTime ComputeDefaultDueDate(MonthEnum month, int year) =>
		new DateTime(year, (int)month + 1, 1).AddDays(DefaultDueDayOfMonth - 1);

	public async Task<IReadOnlyList<DueFee>> GenerateForStudentAsync(
		Guid studentId,
		FeeStructure feeStructure,
		AcademicYear academicYear,
		IReadOnlyList<Guid> selectedOptionalFeeHeadIds,
		IReadOnlyList<CustomOptionalFeeHeadDetail> optionalFeeHeadDetails = null)
	{
		var alreadyExists = await dueFeeRepository.ExistsForStudentAsync(studentId);
		if (alreadyExists)
			throw new BusinessException(DueFeeErrorCodes.DueFeesAlreadyGenerated)
				.WithData("StudentId", studentId);

		var monthRange = ComputeMonthRange(academicYear);
		var dueFees = new List<DueFee>();

		foreach (var detail in feeStructure.FeeStructureDetails)
		{
			if (detail.IsOptional && !selectedOptionalFeeHeadIds.Contains(detail.FeeHeadId))
				continue;

			var customDetail = detail.IsOptional
				? optionalFeeHeadDetails?.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
				: null;

			if (detail.PaymentTermId.HasValue && detail.PaymentTermVersionId.HasValue)
			{
				dueFees.AddRange(await GenerateFromPaymentTermAsync(studentId, detail, customDetail));
				continue;
			}

			// Legacy path — monthly / one-time without Payment Term attachment
			dueFees.AddRange(GenerateLegacy(studentId, detail, monthRange, customDetail));
		}

		return dueFees;
	}

	private async Task<List<DueFee>> GenerateFromPaymentTermAsync(
		Guid studentId,
		FeeStructureDetail detail,
		CustomOptionalFeeHeadDetail? customDetail)
	{
		var schedule = await scheduleResolver.ResolveAsync(detail);
		if (!schedule.IsValid || schedule.Installments.Count == 0)
		{
			throw new BusinessException(PaymentTermErrorCodes.VersionNotPublished)
				.WithData("FeeStructureDetailId", detail.Id)
				.WithData("PaymentTermId", detail.PaymentTermId);
		}

		var annualAmount = customDetail?.Amount ?? detail.Amount;
		var result = new List<DueFee>();
		var installmentCount = schedule.Installments.Count;
		var allocated = 0d;

		for (var i = 0; i < installmentCount; i++)
		{
			var installment = schedule.Installments[i];
			double amount;

			if (customDetail != null)
			{
				// Optional custom amount: split evenly across installments.
				var floor = Math.Floor(annualAmount / installmentCount);
				var remainder = (int)(annualAmount - floor * installmentCount);
				amount = i < remainder ? floor + 1 : floor;
			}
			else if (i == installmentCount - 1)
			{
				// Absorb rounding residual on the last installment.
				amount = Math.Round(annualAmount - allocated, 2);
			}
			else
			{
				amount = (double)Math.Round((decimal)annualAmount * installment.AllocationPercentage / 100m, 2);
				allocated += amount;
			}

			var dueDate = installment.DueDate;
			var month = ToMonthEnum(dueDate.Month);
			var year = dueDate.Year;

			result.Add(new DueFee(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				studentId: studentId,
				feeHeadId: detail.FeeHeadId,
				month: month,
				year: year,
				isOneTime: installmentCount == 1,
				amount: (decimal)amount,
				dueDate: dueDate,
				feeStructureDetailId: detail.Id,
				paymentTermId: schedule.PaymentTermId,
				paymentTermVersionId: schedule.PaymentTermVersionId,
				paymentTermInstallmentId: null,
				installmentSequence: installment.Sequence,
				installmentName: installment.Name,
				coverageStartDate: installment.CoverageStartDate,
				coverageEndDate: installment.CoverageEndDate,
				demandDate: installment.DemandDate,
				scheduleDueDate: installment.DueDate,
				scheduleSource: ScheduleSource.PaymentTerm));
		}

		return result;
	}

	private List<DueFee> GenerateLegacy(
		Guid studentId,
		FeeStructureDetail detail,
		List<(MonthEnum Month, int Year)> monthRange,
		CustomOptionalFeeHeadDetail? customDetail)
	{
		var dueFees = new List<DueFee>();

		if (detail.IsOneTime)
		{
			MonthEnum month;
			int year;
			double amount;

			if (customDetail != null)
			{
				month = customDetail.PayableMonth ?? (detail.PayableMonth ?? monthRange[0].Month);
				year = customDetail.PayableYear ?? (detail.PayableYear ?? monthRange[0].Year);
				amount = customDetail.Amount;

				if (!monthRange.Any(m => m.Month == month && m.Year == year))
				{
					throw new BusinessException("Finance:OutofAcademicYearRange",
							$"Selected payable month '{month}-{year}' is outside the academic year range.")
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

			dueFees.Add(CreateLegacyDueFee(studentId, detail, month, year, amount));
			return dueFees;
		}

		if (customDetail != null &&
			customDetail.StartPayableMonth.HasValue &&
			customDetail.StartPayableYear.HasValue &&
			customDetail.EndPayableMonth.HasValue &&
			customDetail.EndPayableYear.HasValue)
		{
			var startMonth = customDetail.StartPayableMonth.Value;
			var startYear = customDetail.StartPayableYear.Value;
			var endMonth = customDetail.EndPayableMonth.Value;
			var endYear = customDetail.EndPayableYear.Value;

			if (!monthRange.Any(m => m.Month == startMonth && m.Year == startYear))
			{
				throw new BusinessException("Finance:OutofAcademicYearRange",
						$"Selected start month '{startMonth}-{startYear}' is outside the academic year range.")
					.WithData("Month", startMonth.ToString())
					.WithData("Year", startYear);
			}

			if (!monthRange.Any(m => m.Month == endMonth && m.Year == endYear))
			{
				throw new BusinessException("Finance:OutofAcademicYearRange",
						$"Selected end month '{endMonth}-{endYear}' is outside the academic year range.")
					.WithData("Month", endMonth.ToString())
					.WithData("Year", endYear);
			}

			var startDate = new DateTime(startYear, (int)startMonth + 1, 1);
			var endDate = new DateTime(endYear, (int)endMonth + 1, 1);
			if (startDate > endDate)
			{
				throw new BusinessException("Finance:InvalidDateRange", "Start month cannot be after end month.");
			}

			var customMonths = monthRange.Where(m =>
			{
				var mDate = new DateTime(m.Year, (int)m.Month + 1, 1);
				return mDate >= startDate && mDate <= endDate;
			}).ToList();

			foreach (var m in customMonths)
			{
				dueFees.Add(CreateLegacyDueFee(studentId, detail, m.Month, m.Year, customDetail.Amount));
			}

			return dueFees;
		}

		var months = monthRange.ToList();
		var count = months.Count;
		var floorAmount = Math.Floor(detail.Amount / count);
		var remainder = (int)(detail.Amount - floorAmount * count);

		for (var i = 0; i < count; i++)
		{
			var feeAmount = i < remainder ? floorAmount + 1 : floorAmount;
			dueFees.Add(CreateLegacyDueFee(studentId, detail, months[i].Month, months[i].Year, feeAmount));
		}

		return dueFees;
	}

	private DueFee CreateLegacyDueFee(
		Guid studentId,
		FeeStructureDetail detail,
		MonthEnum month,
		int year,
		double amount) => new(
			id: GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			studentId: studentId,
			feeHeadId: detail.FeeHeadId,
			month: month,
			year: year,
			isOneTime: detail.IsOneTime,
			amount: (decimal)amount,
			dueDate: ComputeDefaultDueDate(month, year));

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
