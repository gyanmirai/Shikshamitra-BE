using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class RefundPolicyDto : EntityDto<Guid>, ICurrentPolicyDto
{
	public Guid? CurrentVersionId { get; set; }
	public int CurrentVersionNumber => Version;
	public bool IsAttachable => Status == "Active" && CurrentVersionId.HasValue;
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = "Draft";
	public bool IsDefault { get; set; }
	public string PolicyType { get; set; } = "refund";
	public string FeeHeadScope { get; set; } = "AllFeeHeads";
	public List<Guid> ApplicableFeeHeadIds { get; set; } = [];
	public bool IncludePaidCharges { get; set; }
	public bool IncludePaidLateFee { get; set; }
	public bool AllowPartialRefund { get; set; }
	public string AmountMode { get; set; } = "ManualUpToEligible";
	public decimal? PercentageValue { get; set; }
	public decimal? FixedAmount { get; set; }
	public decimal? MaximumPerRequest { get; set; }
	public decimal? MaximumPerStudentYear { get; set; }
	public string RoundingMethod { get; set; } = "None";
	public string DeductionType { get; set; } = "None";
	public decimal? DeductionValue { get; set; }
	public decimal? MaximumDeduction { get; set; }
	public string DeductionExplanation { get; set; } = string.Empty;
	public List<string> AllowedPayoutMethods { get; set; } = [];
	public int? ProcessingTargetDays { get; set; }
	public int Version { get; set; }
	public bool IsUsedInDownstream { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class CreateUpdateRefundPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string? PolicyCode { get; set; }
	public string? Description { get; set; }
	public string Status { get; set; } = "Draft";
	public bool IsDefault { get; set; }
	public string FeeHeadScope { get; set; } = "AllFeeHeads";
	public List<Guid> ApplicableFeeHeadIds { get; set; } = [];
	public bool IncludePaidCharges { get; set; }
	public bool IncludePaidLateFee { get; set; }
	public bool AllowPartialRefund { get; set; } = true;
	public string AmountMode { get; set; } = "ManualUpToEligible";
	public decimal? PercentageValue { get; set; }
	public decimal? FixedAmount { get; set; }
	public decimal? MaximumPerRequest { get; set; }
	public decimal? MaximumPerStudentYear { get; set; }
	public string RoundingMethod { get; set; } = "None";
	public string DeductionType { get; set; } = "None";
	public decimal? DeductionValue { get; set; }
	public decimal? MaximumDeduction { get; set; }
	public string? DeductionExplanation { get; set; }
	public List<string> AllowedPayoutMethods { get; set; } =
		["Cash", "Cheque", "DemandDraft", "BankTransfer", "Upi"];
	public int? ProcessingTargetDays { get; set; }
	public string? ChangeReason { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class CreateUpdateRefundPolicyDtoValidator : AbstractValidator<CreateUpdateRefundPolicyDto>
{
	public CreateUpdateRefundPolicyDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.PolicyName).NotEmpty().MinimumLength(3).MaximumLength(128);
		RuleFor(x => x.PolicyCode).MaximumLength(32);
		RuleFor(x => x.Description).MaximumLength(500);
		RuleFor(x => x.ChangeReason).MaximumLength(500);
		RuleFor(x => x.DeductionExplanation).MaximumLength(500);
		RuleFor(x => x.Status).Must(x => EnumValue(x, "Draft", "Active"))
			.WithMessage("Status must be Draft or Active. Use the archive action for Archived.");
		RuleFor(x => x).Must(x => !x.IsDefault || EnumValue(x.Status, "Active"))
			.WithMessage("Only an Active refund policy can be the default.");
		RuleFor(x => x.FeeHeadScope).Must(x => EnumValue(x, "AllFeeHeads", "SelectedFeeHeads"));
		RuleFor(x => x.AmountMode).Must(x => EnumValue(x, "RemainingPaidAmount", "ManualUpToEligible", "FixedAmount", "Percentage"));
		RuleFor(x => x.RoundingMethod).Must(x => EnumValue(x, "None", "RoundUp", "RoundDown", "NearestRupee"));
		RuleFor(x => x.DeductionType).Must(x => EnumValue(x, "None", "FixedAmount", "Percentage"));
		RuleFor(x => x.ProcessingTargetDays).GreaterThanOrEqualTo(0).When(x => x.ProcessingTargetDays.HasValue);
		RuleFor(x => x.MaximumPerRequest).GreaterThan(0).When(x => x.MaximumPerRequest.HasValue);
		RuleFor(x => x.MaximumPerStudentYear).GreaterThan(0).When(x => x.MaximumPerStudentYear.HasValue);
		RuleFor(x => x.PercentageValue).NotNull().InclusiveBetween(0.01m, 100m)
			.When(x => EnumValue(x.AmountMode, "Percentage"));
		RuleFor(x => x.FixedAmount).NotNull().GreaterThan(0)
			.When(x => EnumValue(x.AmountMode, "FixedAmount"));
		RuleFor(x => x.ApplicableFeeHeadIds).NotEmpty()
			.When(x => EnumValue(x.FeeHeadScope, "SelectedFeeHeads"));
		RuleFor(x => x.AllowedPayoutMethods).NotEmpty();
		RuleForEach(x => x.AllowedPayoutMethods)
			.Must(x => EnumValue(x, "Cash", "Cheque", "DemandDraft", "BankTransfer", "Upi"));
		RuleFor(x => x.DeductionValue).NotNull().GreaterThan(0)
			.When(x => !EnumValue(x.DeductionType, "None"));
		RuleFor(x => x.DeductionValue).LessThanOrEqualTo(100)
			.When(x => EnumValue(x.DeductionType, "Percentage") && x.DeductionValue.HasValue);
		RuleFor(x => x.DeductionExplanation).NotEmpty()
			.When(x => !EnumValue(x.DeductionType, "None"));
		RuleFor(x => x).Must(x => !x.MaximumPerRequest.HasValue || !x.MaximumPerStudentYear.HasValue ||
			x.MaximumPerRequest <= x.MaximumPerStudentYear)
			.WithMessage("Maximum per request cannot exceed maximum per student/year.");
		RuleFor(x => x).Must(x => !EnumValue(x.AmountMode, "ManualUpToEligible") || x.AllowPartialRefund)
			.WithMessage("Manual up to eligible requires partial refunds to be allowed.");
	}

	private static bool EnumValue(string? value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class RefundPolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Status { get; set; }
	public string? Search { get; set; }
}

public class RefundPolicyListRequestDtoValidator : AbstractValidator<RefundPolicyListRequestDto>
{
	public RefundPolicyListRequestDtoValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxResultCount).InclusiveBetween(1, 100);
		RuleFor(x => x.Status).Must(x => x is null || EnumValue(x, "Draft", "Active", "Archived"));
	}

	private static bool EnumValue(string value, params string[] allowed) =>
		allowed.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public class RefundPolicyImpactDto
{
	public Guid PolicyId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int CurrentVersion { get; set; }
	public int AttachedFeeStructureCount { get; set; }
	public int StudentAssignmentCount { get; set; }
	public bool ChangeReasonRequired { get; set; }
}

public class RefundPolicyCloneRequestDto
{
	public string? Name { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
}

public class RefundPolicySimulateRequestDto
{
	public decimal SettledPaidAmount { get; set; }
	public decimal PriorProcessedRefund { get; set; }
	public decimal ActiveReservation { get; set; }
	public decimal StudentYearRefundedOrReserved { get; set; }
	public decimal? RequestedAmount { get; set; }
}

public class RefundPolicySimulateRequestDtoValidator : AbstractValidator<RefundPolicySimulateRequestDto>
{
	public RefundPolicySimulateRequestDtoValidator()
	{
		RuleFor(x => x.SettledPaidAmount).GreaterThan(0);
		RuleFor(x => x.PriorProcessedRefund).GreaterThanOrEqualTo(0);
		RuleFor(x => x.ActiveReservation).GreaterThanOrEqualTo(0);
		RuleFor(x => x.StudentYearRefundedOrReserved).GreaterThanOrEqualTo(0);
		RuleFor(x => x.RequestedAmount).GreaterThan(0).When(x => x.RequestedAmount.HasValue);
	}
}

public class RefundPolicySimulateResponseDto
{
	public decimal SettledPaidAmount { get; set; }
	public decimal PriorProcessedRefund { get; set; }
	public decimal ActiveReservation { get; set; }
	public decimal RemainingEligible { get; set; }
	public decimal RawRefund { get; set; }
	public decimal DeductionAmount { get; set; }
	public decimal CapAdjustment { get; set; }
	public decimal FinalRefund { get; set; }
	public string Explanation { get; set; } = string.Empty;
}
