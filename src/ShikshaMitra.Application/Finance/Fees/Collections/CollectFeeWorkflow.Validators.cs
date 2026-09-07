using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class CollectFeeWorkflow
{
	private async Task<(Student Student, AcademicYear AcademicYear)> ValidateAndLoadStudentForCollectionAsync(
		CollectFeeInput input)
	{
		var academicYear = await academicYearRepository.GetAsync(input.AcademicYearId);
		var student = await studentRepository.GetAsync(input.StudentId);

		if (student.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(FeeCollectionErrorCodes.StudentAcademicYearMismatch);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		return (student, academicYear);
	}

	private async Task<DueFeeValidationContext> LoadAndValidateDueFeesContextAsync(CollectFeeInput input)
	{
		var dueFeeMap = new Dictionary<Guid, DueFee>();
		var feeHeadMap = new Dictionary<Guid, FeeHead>();

		if (input.DueFeePayments is not { Count: > 0 })
			return new DueFeeValidationContext(dueFeeMap, feeHeadMap);

		var dueFeeIds = input.DueFeePayments.Select(dfp => dfp.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
		if (dueFees.Count != dueFeeIds.Count)
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);
		dueFeeMap = dueFees.ToDictionary(df => df.Id);

		var feeHeadIds = input.DueFeePayments.Select(dfp => dfp.FeeHeadId).Distinct().ToList();
		var feeHeadsList = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
		if (feeHeadsList.Count != feeHeadIds.Count)
			throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);
		feeHeadMap = feeHeadsList.ToDictionary(fh => fh.Id);

		foreach (var dfp in input.DueFeePayments)
		{
			var df = dueFeeMap[dfp.DueFeeId];
			if (df.StudentId != input.StudentId) throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
			if (df.FeeHeadId != dfp.FeeHeadId) throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);
			if (!df.IsBillGeneratedOn(clock.Now.Date))
				throw new BusinessException(DueFeeErrorCodes.BillNotGeneratedYet)
					.WithData("DueFeeId", df.Id)
					.WithData("DemandDate", df.DemandDate);
			if ((decimal)dfp.Amount > df.OutstandingAmount)
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("DueFeeId", df.Id).WithData("Outstanding", df.OutstandingAmount).WithData("Amount", dfp.Amount);
		}

		return new DueFeeValidationContext(dueFeeMap, feeHeadMap);
	}

	/// <summary>
	/// Validates that every LateFeePayments entry is a real due line belonging to this student
	/// and that the amount does not exceed the currently-computed, waiver-net late fee for that
	/// line. Unlike DueFeePayments, a late-fee-only due line (fee already fully paid, only late
	/// fee remains) never appears in DueFeePayments, so this is a fully independent load —
	/// without it, a client could submit an arbitrary DueFeeId + amount as "late fee collected"
	/// with no server-side check at all.
	/// </summary>
	private async Task ValidateLateFeePaymentsAsync(CollectFeeInput input)
	{
		if (input.LateFeePayments is not { Count: > 0 })
		{
			return;
		}

		var dueFeeIds = input.LateFeePayments.Select(lfp => lfp.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
		if (dueFees.Count != dueFeeIds.Count)
		{
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);
		}

		var dueFeeMap = dueFees.ToDictionary(df => df.Id);
		if (dueFeeMap.Values.Any(df => df.StudentId != input.StudentId))
		{
			throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
		}

		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == input.StudentId);
		var effectiveRules = assignment is null
			? new EffectiveCollectionRules(null, false, null, false)
			: await policyResolver.ResolveEffectiveRulesForStudentAsync(input.StudentId, assignment.FeeStructureStandardId);

		var waivedByDueFee = await waiverLookupService.GetAppliedWaivedAmountsAsync(dueFeeIds);

		var today = clock.Now.Date;
		var activeWindow = assignment is null
			? null
			: await exceptionWindowResolver.ResolveActiveWindowAsync(input.AcademicYearId, assignment.FeeStructureStandardId, today);

		foreach (var lfp in input.LateFeePayments)
		{
			var dueFee = dueFeeMap[lfp.DueFeeId];
			var ceiling = 0m;
			if (effectiveRules.Policy is not null && !effectiveRules.IsLateFeeExempt)
			{
				var daysLate = (today - dueFee.DueDate.Date).Days;
				var result = lateFeeCalculationService.Calculate(effectiveRules.Policy, new LateFeeCalculationInput(
					daysLate,
					(decimal)dueFee.OutstandingAmount,
					(decimal)dueFee.OriginalAmount,
					(decimal)dueFee.OutstandingAmount,
					dueFee.FeeHeadId,
					effectiveRules.IsLateFeeExempt,
					effectiveRules.CustomGracePeriodDays,
					activeWindow?.Effect,
					activeWindow?.ExtensionDays,
					activeWindow?.Label));
				ceiling = Math.Max(0, result.LateFee - waivedByDueFee.GetValueOrDefault(lfp.DueFeeId, 0m));
			}

			if ((decimal)lfp.Amount > ceiling)
			{
				throw new BusinessException(FeeCollectionErrorCodes.LateFeeAmountExceedsComputed)
					.WithData("DueFeeId", lfp.DueFeeId).WithData("Ceiling", ceiling).WithData("Amount", lfp.Amount);
			}
		}
	}

	private async Task<ChargeValidationContext> LoadAndValidateChargesContextAsync(CollectFeeInput input)
	{
		var chargeMap = new Dictionary<Guid, Charge>();
		var pendingChargeMap = new Dictionary<Guid, double>();

		if (input.ChargePayments is not { Count: > 0 })
			return new ChargeValidationContext(chargeMap, pendingChargeMap);

		var chargeIds = input.ChargePayments.Select(cp => cp.ChargeId).Distinct().ToList();
		var charges = await chargeRepository.GetListAsync(c => chargeIds.Contains(c.Id) && c.Status == ChargeStatus.Approved);
		if (charges.Count != chargeIds.Count) throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
		chargeMap = charges.ToDictionary(c => c.Id);

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var chargeMetaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var reversedTxIds = chargeMetaQ.Where(m => m.Status == FeeCollectionStatus.Reversed).Select(m => m.Id);

		var paidPerCharge = chargeCollQ
			.Where(r => r.StudentId == input.StudentId
				&& !reversedTxIds.Contains(r.FeeRecordMetadataId) && chargeIds.Contains(r.ChargeId))
			.GroupBy(r => r.ChargeId)
			.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.ChargeId, x => x.Paid);

		foreach (var cp in input.ChargePayments)
		{
			var charge = chargeMap[cp.ChargeId];
			if (charge.StudentId != input.StudentId || charge.AcademicYearId != input.AcademicYearId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
			var paid = paidPerCharge.GetValueOrDefault(cp.ChargeId, 0d);
			var pending = Math.Max(0d, (double)charge.Amount - paid);
			pendingChargeMap[cp.ChargeId] = pending;
			if (cp.Amount > pending)
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("ChargeId", charge.Id).WithData("Pending", pending).WithData("Amount", cp.Amount);
		}

		return new ChargeValidationContext(chargeMap, pendingChargeMap);
	}
}
