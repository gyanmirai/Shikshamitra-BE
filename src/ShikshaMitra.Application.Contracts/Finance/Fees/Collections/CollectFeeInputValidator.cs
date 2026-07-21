using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections;

public class CollectFeeInputValidator : AbstractValidator<CollectFeeInput>
{
    // Payment methods that require the cashier to confirm bank receipt
    private static readonly HashSet<PaymentMethodEnum> CashierDecidedMethods =
    [
        PaymentMethodEnum.UPI,
        PaymentMethodEnum.CreditCard,
        PaymentMethodEnum.DebitCard,
        PaymentMethodEnum.BankTransfer,
    ];

    public CollectFeeInputValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty();

        RuleFor(x => x.AcademicYearId)
            .NotEmpty();

        RuleFor(x => x.Notes)
            .MaximumLength(1000).When(x => x.Notes != null);

        // ── BankReceiptConfirmed rules ────────────────────────────────────────
        // When ANY payment method is CashierDecided, the flag is mandatory.
        RuleFor(x => x.PaymentConfirmed)
            .NotNull()
            .WithMessage(
                "BankReceiptConfirmed is required when using UPI, card, bank transfer, or online gateway.")
            .When(x => x.Payments.Any(p => CashierDecidedMethods.Contains(p.PaymentMethod)));

        // For Cash/Cheque/DD only transactions, the flag must not be supplied.
        RuleFor(x => x.PaymentConfirmed)
            .Null()
            .WithMessage(
                "BankReceiptConfirmed must be null for Cash, Cheque, or Demand Draft payments.")
            .When(x => x.Payments.Any()
                     && x.Payments.All(p => !CashierDecidedMethods.Contains(p.PaymentMethod)));

        // ── Line-level collections (At least one is required) ──────────────────
        RuleFor(x => x)
            .Must(x => x.DueFeePayments.Count + x.ChargePayments.Count >= 1)
            .WithMessage("At least one due fee or charge payment is required.");

        RuleFor(x => x.DueFeePayments)
            .Must(x => x.Count <= 200)
            .WithMessage("Maximum 200 due fee lines allowed per transaction.")
            .Must(list => list.Select(dfp => dfp.DueFeeId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate dueFeeId in DueFeePayments is not allowed.");

        RuleForEach(x => x.DueFeePayments).ChildRules(dfp =>
        {
            dfp.RuleFor(x => x.DueFeeId).NotEmpty();
            dfp.RuleFor(x => x.FeeHeadId).NotEmpty();
            dfp.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Due fee payment amount must be greater than zero.");
        });

        RuleFor(x => x.ChargePayments)
            .Must(x => x.Count <= 20)
            .WithMessage("Maximum 20 charges allowed per transaction.")
            .Must(list => list.Select(cp => cp.ChargeId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate chargeId in ChargePayments is not allowed.");

        RuleForEach(x => x.ChargePayments).ChildRules(cp =>
        {
            cp.RuleFor(x => x.ChargeId).NotEmpty();
            cp.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Charge payment amount must be greater than zero.");
        });

        RuleFor(x => x.LateFeePayments)
            .Must(x => x.Count <= 200)
            .WithMessage("Maximum 200 late fee lines allowed per transaction.")
            .Must(list => list.Select(lfp => lfp.DueFeeId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate dueFeeId in LateFeePayments is not allowed.");

        RuleForEach(x => x.LateFeePayments).ChildRules(lfp =>
        {
            lfp.RuleFor(x => x.DueFeeId).NotEmpty();
            lfp.RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Late fee payment amount must be greater than zero.");
        });

        // ── Payments ──────────────────────────────────────────────────────────
        RuleFor(x => x.Payments)
            .NotEmpty().WithMessage("At least one payment method is required.")
            .Must(p => p.Count <= 5).WithMessage("Maximum 5 payment methods allowed per transaction.");

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(x => x.Amount).GreaterThan(0);
            payment.RuleFor(x => x.PaymentMethod)
                .IsInEnum().WithMessage("Invalid payment method value.");

            // Cheque, DD, BankTransfer, and UPI require a non-empty details dictionary.
            payment.When(x =>
                x.PaymentMethod == PaymentMethodEnum.Cheque ||
                x.PaymentMethod == PaymentMethodEnum.DemandDraft ||
                x.PaymentMethod == PaymentMethodEnum.BankTransfer ||
                x.PaymentMethod == PaymentMethodEnum.UPI,
            () =>
            {
                payment.RuleFor(x => x.Details)
                    .NotNull().WithMessage("Payment details are required for the selected payment method.")
                    .Must(d => d != null && d.Count > 0)
                    .WithMessage("Payment details cannot be empty for the selected payment method.");
            });
        });

        // ── Payment sum ───────────────────────────────────────────────────────
        RuleFor(x => x)
            .Must(x =>
            {
                var paymentsTotal = x.Payments.Sum(p => p.Amount);
                var linesTotal = x.DueFeePayments.Sum(dfp => dfp.Amount) + x.ChargePayments.Sum(cp => cp.Amount)
                    + x.LateFeePayments.Sum(lfp => lfp.Amount);
                return Math.Abs(paymentsTotal - linesTotal) < 0.01;
            })
            .WithMessage("Sum of payment amounts must equal the total fee and charge payments.");
    }
}
