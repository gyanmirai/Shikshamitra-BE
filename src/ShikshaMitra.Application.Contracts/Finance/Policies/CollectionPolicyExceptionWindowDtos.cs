using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyExceptionWindowDto : EntityDto<Guid>
{
	/// <summary>"SchoolWide" | "AcademicYear" | "FeeStructure" | "ClassSection"</summary>
	public string Scope { get; set; } = "SchoolWide";
	public Guid? ScopeId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	/// <summary>"SuspendLateFeeAccrual" | "ExtendGracePeriod" | "DisableOnlineCollection" | "Custom"</summary>
	public string Effect { get; set; } = "SuspendLateFeeAccrual";
	public int? ExtensionDays { get; set; }
	public string ReasonCode { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public Guid CreatedByUserId { get; set; }
	public bool IsActive { get; set; }
	public DateTime CreationTime { get; set; }
}

public class CreateCollectionPolicyExceptionWindowDto
{
	public string Scope { get; set; } = "SchoolWide";
	public Guid? ScopeId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public string Effect { get; set; } = "SuspendLateFeeAccrual";
	public int? ExtensionDays { get; set; }
	public string ReasonCode { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}

public class CollectionPolicyExceptionWindowListRequestDto
{
	/// <summary>Optional filter — "SchoolWide" | "AcademicYear" | "FeeStructure" | "ClassSection".</summary>
	public string? Scope { get; set; }
	/// <summary>Only windows covering this date (StartDate &lt;= activeOn &lt;= EndDate). Omit to list all.</summary>
	public DateTime? ActiveOn { get; set; }
}
