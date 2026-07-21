using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class StandardFeeStructureSaveRequest
{
	[Required]
	public Guid StandardId { get; set; }
	public required StandardFeeStructureRequest StandardFeeStructures { get; set; }
}

public class StandardFeeStructureRequest
{
	public string Name { get; set; }
	public List<FeeHeadAssignmentRequest> FeeHeads { get; set; }
	/// <summary>The Collection Policy governing late fee/waiver for this fee structure. Null clears the attachment (falls back to the academic year's default policy, if any).</summary>
	public Guid? CollectionPolicyId { get; set; }
}

public class FeeHeadAssignmentRequest
{
	[Required]
	public Guid FeeHeadId { get; set; }
	public double Amount { get; set; }
	public bool IsOneTime { get; set; }
	public bool IsOptional { get; set; }
	public MonthEnum PayableMonth { get; set; }
	public int PayableYear { get; set; }

	/// <summary>Optional Payment Term master attached to this fee head.</summary>
	public Guid? PaymentTermId { get; set; }

	/// <summary>Specific published/draft version used for demand generation.</summary>
	public Guid? PaymentTermVersionId { get; set; }

	/// <summary>Inherited master schedule vs structure-specific overrides.</summary>
	public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Inherited;

	/// <summary>Structure-specific installment overrides when ScheduleMode is Customized.</summary>
	public List<ScheduleOverrideDto> InstallmentOverrides { get; set; } = new();
}
