using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;

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
			if (dfp.Amount > df.OutstandingAmount)
				throw new BusinessException(FeeCollectionErrorCodes.AmountExceedsOutstanding)
					.WithData("DueFeeId", df.Id).WithData("Outstanding", df.OutstandingAmount).WithData("Amount", dfp.Amount);
		}

		return new DueFeeValidationContext(dueFeeMap, feeHeadMap);
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
