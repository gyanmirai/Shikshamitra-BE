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
	private async Task ValidatePaymentChannelPolicyAsync(CollectFeeInput input)
	{
		if (input.FinanceFeeAccountId.HasValue)
		{
			var rules = await policyResolver.ResolveEffectiveRulesForAccountAsync(input.FinanceFeeAccountId.Value);
			if (rules.Policy?.AllowOnlineCollection == false && input.Payments.Any(p => p.PaymentMethod is
				PaymentMethodEnum.BankTransfer or PaymentMethodEnum.CreditCard or PaymentMethodEnum.DebitCard or PaymentMethodEnum.UPI))
				throw new BusinessException(FinanceChargeErrorCodes.OnlineCollectionNotAllowed);
			return;
		}
		var assignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == input.StudentId && x.AcademicYearId == input.AcademicYearId);
		if (assignment is null) return;

		var effectiveRules = await policyResolver.ResolveEffectiveRulesForStudentAsync(
			input.StudentId!.Value, assignment.FeeStructureStandardId);
		if (effectiveRules.Policy?.AllowOnlineCollection != false) return;

		var containsOnlineMethod = input.Payments.Any(payment => payment.PaymentMethod is
			PaymentMethodEnum.BankTransfer or PaymentMethodEnum.CreditCard or
			PaymentMethodEnum.DebitCard or PaymentMethodEnum.UPI);
		if (containsOnlineMethod)
			throw new BusinessException(FinanceChargeErrorCodes.OnlineCollectionNotAllowed);
	}

	private async Task<(Student Student, AcademicYear AcademicYear)> ValidateAndLoadStudentForCollectionAsync(
		CollectFeeInput input)
	{
		var academicYear = await academicYearRepository.GetAsync(input.AcademicYearId);
		var student = await studentRepository.GetAsync(input.StudentId!.Value);

		if (student.AcademicYearId != input.AcademicYearId)
			throw new BusinessException(FeeCollectionErrorCodes.StudentAcademicYearMismatch);

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId!.Value);
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
			if (input.FinanceFeeAccountId.HasValue ? df.FinanceFeeAccountId != input.FinanceFeeAccountId : df.StudentId != input.StudentId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
			if (df.FeeHeadId != dfp.FeeHeadId) throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);
			var maxCollectible = Math.Ceiling(df.OutstandingAmount);
			if ((decimal)dfp.Amount > maxCollectible)
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
	private async Task<Dictionary<Guid, DueFee>> ValidateLateFeePaymentsAsync(CollectFeeInput input)
	{
		if (input.LateFeePayments is not { Count: > 0 })
		{
			return [];
		}

		var dueFeeIds = input.LateFeePayments.Select(lfp => lfp.DueFeeId).Distinct().ToList();
		var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));
		if (dueFees.Count != dueFeeIds.Count)
		{
			throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);
		}

		var dueFeeMap = dueFees.ToDictionary(df => df.Id);
		if (dueFeeMap.Values.Any(df => input.FinanceFeeAccountId.HasValue ? df.FinanceFeeAccountId != input.FinanceFeeAccountId : df.StudentId != input.StudentId))
		{
			throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
		}

		var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
		if (heads.Any(x => x.AcademicYearId != input.AcademicYearId))
			throw new BusinessException(FeeCollectionErrorCodes.InvalidDueFee);
		var balances = await lateFeeBalanceService.GetBalancesAsync(dueFees, input.AcademicYearId, clock.Now, preserveAssessment: true);
		foreach (var lfp in input.LateFeePayments)
		{
			var ceiling = balances[lfp.DueFeeId].PendingAmount;
			if ((decimal)lfp.Amount > ceiling)
			{
				throw new BusinessException(FeeCollectionErrorCodes.LateFeeAmountExceedsComputed)
					.WithData("DueFeeId", lfp.DueFeeId).WithData("Ceiling", ceiling).WithData("Amount", lfp.Amount);
			}
		}
		return dueFeeMap;
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
		var waivedPerCharge = (await chargeWaiverRepository.GetListAsync(x => chargeIds.Contains(x.ChargeId)))
			.GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(w => (double)w.EffectiveAmount));

		foreach (var cp in input.ChargePayments)
		{
			var charge = chargeMap[cp.ChargeId];
			if (charge.StudentId != input.StudentId || charge.AcademicYearId != input.AcademicYearId)
				throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
			var paid = paidPerCharge.GetValueOrDefault(cp.ChargeId, 0d);
			var pending = Math.Max(0d, (double)charge.Amount - paid
				- waivedPerCharge.GetValueOrDefault(cp.ChargeId, 0d));
			pendingChargeMap[cp.ChargeId] = pending;
			if (cp.Amount > pending)
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("ChargeId", charge.Id).WithData("Pending", pending).WithData("Amount", cp.Amount);
		}

		return new ChargeValidationContext(chargeMap, pendingChargeMap);
	}
}
