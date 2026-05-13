using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Domain manager responsible for generating <see cref="DueFee"/> records when a fee
/// structure is assigned to a student. This is a one-time, idempotent operation per student.
/// </summary>
public class DueFeeManager(IDueFeeRepository dueFeeRepository) : DomainService
{
	/// <summary>
	/// Generates the full set of <see cref="DueFee"/> records for a student based on
	/// the fee heads defined in the assigned <paramref name="feeStructure"/>.
	/// </summary>
	/// <remarks>
	/// <list type="bullet">
	///   <item>Optional fee heads are included only when their id is present in <paramref name="selectedOptionalFeeHeadIds"/>.</item>
	///   <item>One-time fees produce a single record — for the <c>PayableMonth/Year</c> on the detail if set, otherwise the first month of the academic year.</item>
	///   <item>Recurring fees produce one record per month in the academic year range, ignoring <c>PayableMonth/Year</c>.</item>
	/// </list>
	/// The returned list is NOT persisted here — the application layer owns the Unit of Work.
	/// </remarks>
	public async Task<IReadOnlyList<DueFee>> GenerateForStudentAsync(
		Guid studentId,
		FeeStructure feeStructure,
		AcademicYear academicYear,
		IReadOnlyList<Guid> selectedOptionalFeeHeadIds)
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

			if (detail.IsOneTime)
			{
				// One-time fee: use PayableMonth/Year from the fee structure detail when
				// explicitly configured; fall back to the first month of the academic year.
				var (month, year) = detail.PayableMonth.HasValue && detail.PayableYear.HasValue
					? (detail.PayableMonth.Value, detail.PayableYear.Value)
					: monthRange[0];

				dueFees.Add(CreateDueFee(studentId, detail, month, year, detail.Amount));
			}
			else
			{
				// Recurring fee: one DueFee per month in the academic year — PayableMonth/Year
				// on the detail is intentionally ignored for recurring fees.
				foreach (var (month, year) in monthRange)
					dueFees.Add(CreateDueFee(studentId, detail, month, year, detail.Amount/monthRange.Count()));
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
