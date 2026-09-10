using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class FeeStructureCreateResponse : StandardFeeStructureResponseItem { }

public class FeeStructureDetailsResponse : EntityDto<Guid>
{
	public List<FeeStructureClassOptionDto> Classes { get; set; } = [];
    public string Name { get; set; }
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; }
    public Guid BoardId { get; set; }
    public string BoardName { get; set; }
    public Guid MediumId { get; set; }
    public string MediumName { get; set; }
    public Guid? ReceiptBookId { get; set; }
    public string? ReceiptBookName { get; set; }
    public int TotalClasses { get; set; }
    public int ConfiguredClasses { get; set; }
}

public class FeeStructureMetadataListResponse
{
    public long TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<FeeStructureDetailsResponse> Items { get; set; }
}

public class StandardFeeStructureResponse
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; }
    public List<StandardFeeStructureResponseItem> StandardFeeStructures { get; set; }
}

public class StandardFeeStructureResponseItem : EntityDto<Guid>
{
	public Dictionary<string, string> PolicyLabels { get; set; } = [];
	public FeeStructurePreviewDto? PublicationReview { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid RevisionRootId { get; set; }
	public Guid? PreviousRevisionId { get; set; }
	public int RevisionNumber { get; set; }
	public Guid? ReceiptBookId { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
    public string Name { get; set; }
	public FeeStructureStatus Status { get; set; }
	public List<FeeHeadAssignmentResponse> FeeHeads { get; set; }
	public Guid? CollectionPolicyId { get; set; }
	public Guid? CollectionPolicyVersionId { get; set; }
	public Guid? ConcessionPolicyId { get; set; }
	public Guid? ConcessionPolicyVersionId { get; set; }
	public Guid? RefundPolicyId { get; set; }
	public Guid? RefundPolicyVersionId { get; set; }
	public Guid? ProrationPolicyId { get; set; }
	public Guid? ProrationPolicyVersionId { get; set; }
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ConcessionFeeHeadScope ConcessionFeeHeadScope { get; set; }
	public List<Guid> ConcessionApplicableFeeHeadIds { get; set; } = [];
}

public class FeeHeadAssignmentResponse
{
	public Guid Id { get; set; }
	public List<ScheduleOverrideDto> InstallmentOverrides { get; set; } = [];
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; }
    public string CategoryName { get; set; }
    public decimal Amount { get; set; }
    public bool IsOneTime { get; set; }
    public bool IsOptional { get; set; }
	public MonthEnum? PayableMonth { get; set; }
	public int? PayableYear { get; set; }

    // ── Payment Term attachment (new) ──────────────────────────────────────────

    /// <summary>The Payment Term attached to this fee head.</summary>
    public Guid? PaymentTermId { get; set; }

    /// <summary>The specific Payment Term version snapshot used for demand generation.</summary>
    public Guid? PaymentTermVersionId { get; set; }

    /// <summary>How this fee head uses the attached Payment Term.</summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
    public ScheduleMode ScheduleMode { get; set; }

    /// <summary>The Payment Term name (for display).</summary>
    public string? PaymentTermName { get; set; }

    /// <summary>Current version number of the attached term.</summary>
    public int? PaymentTermVersionNumber { get; set; }

    /// <summary>Whether a new version is available for adoption.</summary>
    public bool HasNewVersionAvailable { get; set; }

    /// <summary>Whether the schedule is locked by issued dues.</summary>
    public bool IsScheduleLocked { get; set; }
}

public class AcademicYearMonthItem
{
    public MonthEnum Month { get; set; }
    public int Year { get; set; }
}
