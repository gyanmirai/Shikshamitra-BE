using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

public class ResolvedInstallmentDto
{
public int Sequence { get; set; }
public string Name { get; set; } = string.Empty;
public DateTime CoverageStartDate { get; set; }
public DateTime CoverageEndDate { get; set; }
public DateTime DemandDate { get; set; }
public DateTime DueDate { get; set; }
public decimal AllocationPercentage { get; set; }
public decimal CalculatedAmount { get; set; }
public bool? AllowPartialOverride { get; set; }
public int? GracePeriodDaysOverride { get; set; }
}

public class ResolvedFeeHeadScheduleDto
{
public Guid FeeStructureDetailId { get; set; }
public Guid FeeHeadId { get; set; }
public string FeeHeadName { get; set; } = string.Empty;
public decimal AnnualAmount { get; set; }
public Guid? PaymentTermId { get; set; }
public string? PaymentTermName { get; set; }
public Guid? PaymentTermVersionId { get; set; }
public int? VersionNumber { get; set; }
public ScheduleMode ScheduleMode { get; set; }
public List<ResolvedInstallmentDto> Installments { get; set; } = new();
public List<string> ValidationWarnings { get; set; } = new();
public bool IsValid { get; set; }
}

public class FeeStructureSchedulePreviewDto
{
public Guid FeeStructureId { get; set; }
public string FeeStructureName { get; set; } = string.Empty;
public List<ResolvedFeeHeadScheduleDto> FeeHeadSchedules { get; set; } = new();
public bool IsValid { get; set; }
public List<string> ValidationErrors { get; set; } = new();
public List<string> ValidationWarnings { get; set; } = new();
}

public class ScheduleOverrideDto
{
public Guid SourcePaymentTermInstallmentId { get; set; }
public int Sequence { get; set; }
public string Name { get; set; } = string.Empty;
public DateTime? CoverageStartDate { get; set; }
public DateTime? CoverageEndDate { get; set; }
public DateTime? DemandDate { get; set; }
public DateTime? DueDate { get; set; }
public decimal? AllocationPercentage { get; set; }
}

public class FeeHeadAssignmentWithTermDto
{
public Guid FeeHeadId { get; set; }
public decimal Amount { get; set; }
public bool IsOneTime { get; set; }
public bool IsOptional { get; set; }
public MonthEnum PayableMonth { get; set; }
public int PayableYear { get; set; }
public Guid? PaymentTermId { get; set; }
public Guid? PaymentTermVersionId { get; set; }
public ScheduleMode ScheduleMode { get; set; }
public List<ScheduleOverrideDto> InstallmentOverrides { get; set; } = new();
}

public class GenerateDemandsDto
{
public Guid StudentFeeAssignmentId { get; set; }
public DemandGenerationMode Mode { get; set; }
}

public class DemandGenerationResultDto
{
public int CreatedCount { get; set; }
public int SkippedCount { get; set; }
public decimal TotalAmount { get; set; }
public List<string> CreatedScheduleKeys { get; set; } = new();
}

public class ReceiptAllocationLineDto
{
public Guid DueFeeId { get; set; }
public string FeeHeadName { get; set; } = string.Empty;
public string? InstallmentLabel { get; set; }
public DateTime? CoverageStartDate { get; set; }
public DateTime? CoverageEndDate { get; set; }
public DateTime? DueDate { get; set; }
public double Amount { get; set; }
public double BalanceAfterPayment { get; set; }
public ScheduleSource ScheduleSource { get; set; }
}
