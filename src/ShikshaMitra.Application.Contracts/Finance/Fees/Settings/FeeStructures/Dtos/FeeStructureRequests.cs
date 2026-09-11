using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ShikshaMitra.Domain.Finance.Policies;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class StandardFeeStructureSaveRequest
{
	public Guid? RequestId { get; set; }
	[Required]
	public Guid StandardId { get; set; }
	public required StandardFeeStructureRequest StandardFeeStructures { get; set; }
}

public class StandardFeeStructureRequest
{
	public string? ConcurrencyStamp { get; set; }
	/// <summary>Incomplete drafts can be saved; publication always validates the complete configuration.</summary>
	public bool SaveAsDraft { get; set; } = true;
	public Guid? ReceiptBookId { get; set; }
	[Required]
	public string Name { get; set; } = string.Empty;
	[Required]
	public List<FeeHeadAssignmentRequest> FeeHeads { get; set; } = new();
	/// <summary>The active Collection Policy governing late fee/waiver for this fee structure. Required for publication; an incomplete draft may omit it.</summary>
	public Guid? CollectionPolicyId { get; set; }
	/// <summary>Exact immutable version. When omitted on an unchanged attachment, retain the saved version.</summary>
	public Guid? CollectionPolicyVersionId { get; set; }
	/// <summary>Active Concession Policy attached to this fee structure. The server resolves and stores its exact version.</summary>
	public Guid? ConcessionPolicyId { get; set; }
	public Guid? ConcessionPolicyVersionId { get; set; }
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ConcessionFeeHeadScope ConcessionFeeHeadScope { get; set; } = ConcessionFeeHeadScope.EntireFeeStructure;
	public List<Guid> ConcessionApplicableFeeHeadIds { get; set; } = [];
	/// <summary>Optional Active Refund Policy. The server retains the selected immutable version.</summary>
	public Guid? RefundPolicyId { get; set; }
	/// <summary>Exact version assertion; the server validates ownership and availability.</summary>
	public Guid? RefundPolicyVersionId { get; set; }
	/// <summary>Optional Active Proration Policy. The server retains the selected immutable version.</summary>
	public Guid? ProrationPolicyId { get; set; }
	/// <summary>Exact version assertion; the server validates ownership and availability.</summary>
	public Guid? ProrationPolicyVersionId { get; set; }
}

public class CollectionPolicyAttachmentImpactRequest
{
	[Required]
	public Guid CollectionPolicyId { get; set; }
	[Required]
	public Guid FeeStructureMetadataId { get; set; }
	public List<Guid> FeeHeadIds { get; set; } = new();
}

public class CollectionPolicyAttachmentImpactResponse
{
	public Guid PolicyId { get; set; }
	public Guid PolicyVersionId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	/// <summary>Fee heads in both the structure and the policy late-fee scope (S ∩ L).</summary>
	public List<Guid> CoveredLateFeeHeadIds { get; set; } = new();
	/// <summary>Structure fee heads that do not receive late fees from this policy (S − L).</summary>
	public List<Guid> StructureHeadsWithoutLateFee { get; set; } = new();
	/// <summary>Policy late-fee heads absent from this structure; ignored for this attachment (L − S).</summary>
	public List<Guid> PolicyLateFeeHeadsNotInStructure { get; set; } = new();
	/// <summary>Fee heads in both the structure and the policy waiver scope (S ∩ W).</summary>
	public List<Guid> CoveredWaiverHeadIds { get; set; } = new();
	/// <summary>Structure fee heads that do not receive waivers from this policy (S − W).</summary>
	public List<Guid> StructureHeadsWithoutWaiver { get; set; } = new();
	/// <summary>Policy waiver heads absent from this structure; ignored for this attachment (W − S).</summary>
	public List<Guid> PolicyWaiverHeadsNotInStructure { get; set; } = new();
	public List<string> Warnings { get; set; } = new();
	/// <summary>
	/// True when the active policy belongs to the structure's academic year. Missing head overlap is
	/// reported as a warning because applicability is intersection-based, not a reason to reject reuse.
	/// </summary>
	public bool IsAttachable { get; set; }
}

public class FeeHeadAssignmentRequest
{
	public Guid? Id { get; set; }
	[Required]
	public Guid FeeHeadId { get; set; }
	public decimal Amount { get; set; }
	public bool IsOneTime { get; set; }
	public bool IsOptional { get; set; }
	public MonthEnum PayableMonth { get; set; }
	public int PayableYear { get; set; }

	/// <summary>Optional Payment Term master attached to this fee head.</summary>
	public Guid? PaymentTermId { get; set; }

	/// <summary>Specific published or superseded version; draft versions are rejected.</summary>
	public Guid? PaymentTermVersionId { get; set; }

	/// <summary>Inherited master schedule vs structure-specific overrides.</summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Inherited;

	/// <summary>Structure-specific installment overrides when ScheduleMode is Customized.</summary>
	public List<ScheduleOverrideDto> InstallmentOverrides { get; set; } = new();
}
