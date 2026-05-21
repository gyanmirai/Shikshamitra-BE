using System;
using System.Linq;
using FluentValidation;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections;

public class CollectFeeInputValidator : AbstractValidator<CollectFeeInput>
{
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

        RuleFor(x => x.SelectedCharges)
            .Must(c => c == null || c.Count <= 20).WithMessage("Maximum 20 charges allowed per transaction.");

        RuleForEach(x => x.SelectedCharges).ChildRules(charge =>
        {
            charge.RuleFor(x => x.ChargeId).NotEmpty();
            charge.RuleFor(x => x.Amount).GreaterThan(0);
        });

        RuleFor(x => x.Payments)
            .NotEmpty().WithMessage("At least one payment method is required.")
            .Must(p => p.Count <= 5).WithMessage("Maximum 5 payment methods allowed per transaction.");

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(x => x.Amount).GreaterThan(0);
        });

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
