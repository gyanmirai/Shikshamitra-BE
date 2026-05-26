using System;
using System.Linq;
using FluentValidation;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections;

public class CollectFeeInputValidator : AbstractValidator<CollectFeeInput>
{
    // Payment methods that require the cashier to confirm bank receipt
    private static readonly System.Collections.Generic.HashSet<PaymentMethodEnum> CashierDecidedMethods =
    [
        PaymentMethodEnum.UPI,
        PaymentMethodEnum.CreditCard,
        PaymentMethodEnum.DebitCard,
        PaymentMethodEnum.BankTransfer,
        PaymentMethodEnum.OnlinePaymentGateway,
        PaymentMethodEnum.WalletPrepaidCard,
    ];

    public CollectFeeInputValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty();

        RuleFor(x => x.AcademicYearId)
            .NotEmpty();

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("Total amount must be greater than zero.");

        RuleFor(x => x.ReceivedAmount)
            .GreaterThanOrEqualTo(0).When(x => x.ReceivedAmount.HasValue)
            .WithMessage("Received amount cannot be negative.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).When(x => x.Notes != null);

        // ── BankReceiptConfirmed rules ────────────────────────────────────────
        // When ANY payment method is CashierDecided, the flag is mandatory.
        RuleFor(x => x.BankReceiptConfirmed)
            .NotNull()
            .WithMessage(
                "BankReceiptConfirmed is required when using UPI, card, bank transfer, or online gateway.")
            .When(x => x.Payments.Any(p => CashierDecidedMethods.Contains(p.PaymentMethod)));

        // For Cash/Cheque/DD only transactions, the flag must not be supplied.
        RuleFor(x => x.BankReceiptConfirmed)
            .Null()
            .WithMessage(
                "BankReceiptConfirmed must be null for Cash, Cheque, or Demand Draft payments.")
            .When(x => x.Payments.Any()
                     && x.Payments.All(p => !CashierDecidedMethods.Contains(p.PaymentMethod)));

        // ── Fee heads ─────────────────────────────────────────────────────────
        RuleFor(x => x.SelectedFeeHeads)
            .NotEmpty().WithMessage("At least one fee head is required.")
            .Must(x => x.Count <= 50).WithMessage("Maximum 50 fee heads allowed per transaction.");

        RuleForEach(x => x.SelectedFeeHeads).ChildRules(feeHead =>
        {
            feeHead.RuleFor(x => x.FeeHeadId).NotEmpty();
            feeHead.RuleFor(x => x.Amount).GreaterThan(0);

            feeHead.When(x => x.Allocations is { Count: > 0 }, () =>
            {
                feeHead.RuleFor(x => x.Allocations)
                    .Must((fh, allocs) =>
                    {
                        if (allocs is null) return true;
                        var allocSum = allocs.Sum(a => a.Amount);
                        return Math.Abs(allocSum - fh.Amount) < 0.01;
                    })
                    .WithMessage("Sum of allocations must equal the fee-head amount.");

                feeHead.RuleForEach(x => x.Allocations).ChildRules(alloc =>
                {
                    alloc.RuleFor(a => a.DueFeeId).NotEmpty();
                    alloc.RuleFor(a => a.Amount).GreaterThan(0);
                });
            });
        });

        // ── Charges ───────────────────────────────────────────────────────────
        RuleFor(x => x.SelectedCharges)
            .Must(c => c == null || c.Count <= 20).WithMessage("Maximum 20 charges allowed per transaction.");

        RuleForEach(x => x.SelectedCharges).ChildRules(charge =>
        {
            charge.RuleFor(x => x.ChargeId).NotEmpty();
            charge.RuleFor(x => x.Amount).GreaterThan(0);
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
                var received = x.ReceivedAmount ?? x.TotalAmount;
                return Math.Abs(paymentsTotal - received) < 0.01;
            })
            .WithMessage("Sum of payment amounts must equal the received amount.");
    }
}
