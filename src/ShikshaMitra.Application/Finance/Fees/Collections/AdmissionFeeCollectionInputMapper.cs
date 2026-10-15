using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

internal static class AdmissionFeeCollectionInputMapper
{
	internal static CollectFeeInput Map(CollectAdmissionFeeInput input, FinanceFeeAccount account)
	{
		bool Invalid(decimal amount) => amount <= 0m || amount > 999_999_999_999.99m || decimal.Round(amount, 2) != amount;
		if (input == null || string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 100 || input.Notes?.Length > 1000 ||
			input.DueFeePayments == null || input.ChargePayments == null || input.LateFeePayments == null || input.Payments == null ||
			input.Payments.Count is < 1 or > 5 || input.DueFeePayments.Count > 200 || input.ChargePayments.Count > 20 || input.LateFeePayments.Count > 200 ||
			input.DueFeePayments.Count + input.ChargePayments.Count + input.LateFeePayments.Count == 0 ||
			input.DueFeePayments.Any(p => p == null || p.DueFeeId == Guid.Empty || p.FeeHeadId == Guid.Empty || Invalid(p.Amount)) ||
			input.ChargePayments.Any(p => p == null || p.ChargeId == Guid.Empty || Invalid(p.Amount)) ||
			input.LateFeePayments.Any(p => p == null || p.DueFeeId == Guid.Empty || Invalid(p.Amount)) ||
			input.Payments.Any(p => p == null || !Enum.IsDefined(p.PaymentMethod) || Invalid(p.Amount)) ||
			input.DueFeePayments.Select(p => p.DueFeeId).Distinct().Count() != input.DueFeePayments.Count ||
			input.ChargePayments.Select(p => p.ChargeId).Distinct().Count() != input.ChargePayments.Count ||
			input.LateFeePayments.Select(p => p.DueFeeId).Distinct().Count() != input.LateFeePayments.Count ||
			input.DueFeePayments.Sum(p => p.Amount) + input.ChargePayments.Sum(p => p.Amount) + input.LateFeePayments.Sum(p => p.Amount) != input.Payments.Sum(p => p.Amount))
			throw new AbpValidationException("Invalid fee collection.", [new ValidationResult("Choose valid fee allocations and matching payment amounts.")]);
		return new CollectFeeInput
		{
			StudentId = account.StudentId, FinanceFeeAccountId = account.Id, AcademicYearId = account.AcademicYearId,
			IdempotencyKey = input.IdempotencyKey, Notes = input.Notes, PaymentConfirmed = input.PaymentConfirmed,
			DueFeePayments = input.DueFeePayments.Select(p => new DueFeePaymentDto { DueFeeId = p.DueFeeId, FeeHeadId = p.FeeHeadId, Amount = (double)p.Amount }).ToList(),
			ChargePayments = input.ChargePayments.Select(p => new ChargePaymentDto { ChargeId = p.ChargeId, Amount = (double)p.Amount }).ToList(),
			LateFeePayments = input.LateFeePayments.Select(p => new LateFeePaymentDto { DueFeeId = p.DueFeeId, Amount = (double)p.Amount }).ToList(),
			Payments = input.Payments.Select(p => new FeePaymentDto { BankAccountId = p.BankAccountId, PaymentMethod = p.PaymentMethod, Amount = (double)p.Amount, Details = p.Details }).ToList()
		};
	}
}
