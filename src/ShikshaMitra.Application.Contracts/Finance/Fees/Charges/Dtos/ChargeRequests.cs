using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class CreateChargeRequest
{
	public string ChargeName { get; set; } = string.Empty;

	public Guid ChargeCategoryId { get; set; }

	public string PurposeDescription { get; set; } = string.Empty;

	public decimal Amount { get; set; }

	public DateTimeOffset DueDate { get; set; }

	public int Priority { get; set; } = 1;

	public string? AdditionalNotes { get; set; }

	public ChargeAudienceDto? Audience { get; set; }

	/// <summary>Legacy single-student create. Mapped to explicit audience when <see cref="Audience"/> is null.</summary>
	public Guid? StudentId { get; set; }
}

public class UpdateChargeRequest
{
	public string ConcurrencyStamp { get; set; } = string.Empty;

	public string? ChargeName { get; set; }

	public Guid? ChargeCategoryId { get; set; }

	public string? PurposeDescription { get; set; }

	public decimal? Amount { get; set; }

	public DateTimeOffset? DueDate { get; set; }

	public int? Priority { get; set; }

	public string? AdditionalNotes { get; set; }

	public ChargeAudienceDto? Audience { get; set; }
}

public class ApproveChargeRequest
{
	public string ConcurrencyStamp { get; set; } = string.Empty;

	public List<Guid>? StudentIds { get; set; }

	public string? ApprovalNotes { get; set; }
}

public class RejectChargeRequest
{
	public string ConcurrencyStamp { get; set; } = string.Empty;

	public List<Guid>? StudentIds { get; set; }

	public string RejectionReason { get; set; } = string.Empty;
}

public class GetChargesRequest
{
	public Guid AcademicYearId { get; set; }

	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 20;

	public string? Search { get; set; }

	/// <summary>JSON column filters, applied before counting and pagination.</summary>
	public string? Filters { get; set; }

	/// <summary>Batch workflow: 0 = Pending, 1 = In Process, 2 = Completed, 3 = Rejected.</summary>
	public int Status { get; set; } = 0;

	public string? SortBy { get; set; }

	public string? SortOrder { get; set; } = "asc";

	public Guid? EducationMediumId { get; set; }

	public Guid? BoardId { get; set; }

	/// <summary>Filter by a specific standard (class). Only requests with at least one student in this standard are returned.</summary>
	public Guid? StandardId { get; set; }

	/// <summary>Filter by a specific division. Only requests with at least one student in this division are returned.</summary>
	public Guid? DivisionId { get; set; }
}

public class VoidChargeRequest
{
	public List<Guid>? StudentIds { get; set; }

	public string Reason { get; set; } = string.Empty;

	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class GetChargeLinesRequest
{
	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 100;

	public int? Status { get; set; }

	public Guid? EducationMediumId { get; set; }

	public Guid? BoardId { get; set; }
}
