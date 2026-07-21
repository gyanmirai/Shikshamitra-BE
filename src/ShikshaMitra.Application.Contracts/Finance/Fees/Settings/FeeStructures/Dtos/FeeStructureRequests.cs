using ShikshaMitra.Finance.Fees.Collections;
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
}
