using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public class CreateFeeAdjustmentSourceLineDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    [Range(0.01, double.MaxValue)]
    public double Amount { get; set; }
}

public class CreateFeeAdjustmentDto
{
    [Required]
    public Guid StudentId { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreateFeeAdjustmentSourceLineDto> SourceLines { get; set; } = [];
}

public class UpdateFeeAdjustmentDto : CreateFeeAdjustmentDto;

public class GetFeeAdjustmentApplicationsInput : IFinanceClassDivisionFilter
{
    public Guid BoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public FeeAdjustmentStatus? Status { get; set; }

    /// <summary>When true, returns PartiallyApplied and Applied items (approved tab).</summary>
    public bool? CompletedOnly { get; set; }

    public string? Filter { get; set; }
    public string? Filters { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class FeeAdjustmentSourceLineDto
{
    public Guid DueFeeId { get; set; }
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public string InstallmentLabel { get; set; } = string.Empty;
    public MonthEnum Month { get; set; }
    public int Year { get; set; }
    public double PaidAmount { get; set; }
    public double AdjustableAmount { get; set; }
    public double RequestedAmount { get; set; }
}

public class FeeAdjustmentTargetAllocationDto
{
    public Guid DueFeeId { get; set; }
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public string InstallmentLabel { get; set; } = string.Empty;
    public MonthEnum Month { get; set; }
    public int Year { get; set; }
    public double Amount { get; set; }
    public int ApplyBatchNumber { get; set; }
    public DateTime AppliedOn { get; set; }
    public double? OriginalPendingAmount { get; set; }
    public double? FinalPendingAmount { get; set; }
}

public class PaidFeeSummaryDto
{
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public double PaidAmount { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public string? Installment { get; set; }
}

public class FeeAdjustmentDetailDto
{
    public Guid Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? StudentGrNo { get; set; }
    public string? RollNo { get; set; }
    public string? ClassName { get; set; }
    public string? DivisionName { get; set; }
    public string? ContactNo { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid BoardId { get; set; }
    public Guid EducationMediumId { get; set; }
    public string AdjustmentTypeName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public FeeAdjustmentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public double RequestedAmount { get; set; }
    public double AppliedAmount { get; set; }
    public double RemainingAmount { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ApproverNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FeeAdjustmentSourceLineDto> SourceLines { get; set; } = [];
    public List<FeeAdjustmentTargetAllocationDto> TargetAllocations { get; set; } = [];
    public List<PaidFeeSummaryDto> PaidFeeSummary { get; set; } = [];
}

public class FeeAdjustmentApplicationItemDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string GrNumber { get; set; } = string.Empty;
    public string RollNo { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public string DivisionName { get; set; } = string.Empty;
    public double RequestedAmount { get; set; }
    public double AppliedAmount { get; set; }
    public double RemainingAmount { get; set; }
    public FeeAdjustmentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FeeAdjustmentApplicationsDto
{
    public int PendingCount { get; set; }
    public int PartiallyAppliedCount { get; set; }
    public int AppliedCount { get; set; }
    public int RejectedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeAdjustmentApplicationItemDto> Items { get; set; } = [];
}

public class AdjustmentEligibilityMonthDto
{
    public Guid DueFeeId { get; set; }
    public MonthEnum Month { get; set; }
    public int Year { get; set; }
    public double Total { get; set; }
    public double Paid { get; set; }
    public double Refunded { get; set; }
    public double Adjusted { get; set; }
    public double Adjustable { get; set; }
}

public class AdjustmentEligibilityFeeHeadDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double TotalPaid { get; set; }
    public double TotalAdjustable { get; set; }
    public List<AdjustmentEligibilityMonthDto> Months { get; set; } = [];
}

public class AdjustmentEligibilityDto
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalPaid { get; set; }
    public double TotalAdjustable { get; set; }
    public double TotalAlreadyAdjusted { get; set; }
    public List<AdjustmentEligibilityFeeHeadDto> FeeHeads { get; set; } = [];
}

public class FeeAdjustmentPendingFeeDto
{
    public Guid InstallmentId { get; set; }
    public Guid DueFeeId { get; set; }
    public string InstallmentLabel { get; set; } = string.Empty;
    public int MonthOrder { get; set; }
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double TotalFees { get; set; }
    public double PaidAmount { get; set; }
    public double PendingAmount { get; set; }
    public double DueAmount { get; set; }
    public double MaxApplicableAmount { get; set; }
}

public class FeeAdjustmentApplyContextDto
{
    public Guid AdjustmentId { get; set; }
    public Guid StudentId { get; set; }
    public double ApprovedAmount { get; set; }
    public double RemainingAmount { get; set; }
    public double TotalPendingFees { get; set; }
    public List<FeeAdjustmentPendingFeeDto> PendingFees { get; set; } = [];
}

public class ApplyFeeAdjustmentLineDto
{
    [Required]
    public Guid DueFeeId { get; set; }

    public Guid FeeHeadId { get; set; }

    public string? InstallmentLabel { get; set; }

    [Range(0.01, double.MaxValue)]
    public double AppliedAmount { get; set; }
}

public class ApplyFeeAdjustmentDto
{
    public string? ApprovalNotes { get; set; }
    public bool AllowPartialApply { get; set; } = true;

    [Required]
    [MinLength(1)]
    public List<ApplyFeeAdjustmentLineDto> Lines { get; set; } = [];
}

public class ApplyFeeAdjustmentLineResultDto
{
    public Guid DueFeeId { get; set; }
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public string InstallmentLabel { get; set; } = string.Empty;
    public double OriginalPendingAmount { get; set; }
    public double AppliedAmount { get; set; }
    public double FinalPendingAmount { get; set; }
}

public class ApplyFeeAdjustmentResultDto
{
    public Guid AdjustmentId { get; set; }
    public FeeAdjustmentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public double TotalAppliedAmount { get; set; }
    public double RemainingAmount { get; set; }
    public double AppliedInThisBatch { get; set; }
    public int SelectedInstallmentsCount { get; set; }
    public List<ApplyFeeAdjustmentLineResultDto> Lines { get; set; } = [];
    public DateTime ProcessedAt { get; set; }
    public Guid ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
}

public class RejectFeeAdjustmentDto
{
    [Required]
    [MinLength(3)]
    public string Message { get; set; } = string.Empty;
}

public class RejectFeeAdjustmentResultDto
{
    public Guid AdjustmentId { get; set; }
    public FeeAdjustmentStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime RejectedAt { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}

public class CreateFeeAdjustmentDtoValidator : AbstractValidator<CreateFeeAdjustmentDto>
{
    public CreateFeeAdjustmentDtoValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(1000);
        RuleFor(x => x.SourceLines).NotEmpty();
        RuleForEach(x => x.SourceLines).ChildRules(line =>
        {
            line.RuleFor(l => l.DueFeeId).NotEmpty();
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
    }
}

public class ApplyFeeAdjustmentDtoValidator : AbstractValidator<ApplyFeeAdjustmentDto>
{
    public ApplyFeeAdjustmentDtoValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.DueFeeId).NotEmpty();
            line.RuleFor(l => l.AppliedAmount).GreaterThan(0);
        });
    }
}
