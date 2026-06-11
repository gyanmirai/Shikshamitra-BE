using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class ChargeResponse
{
	public bool Success { get; set; } = true;
	public ChargeData Data { get; set; } = new();
	public string Message { get; set; } = string.Empty;
}

public class ChargeData
{
	public Guid ChargeId { get; set; }
	public string ChargeName { get; set; } = string.Empty;
	public int ResolvedStudentCount { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public string PurposeDescription { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	public int Status { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public string? AdditionalNotes { get; set; }
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; } = string.Empty;
	public DateTime RequestedOn { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
}

public class ChargeListResponse
{
	public bool Success { get; set; } = true;
	public List<ChargeListItem> Items { get; set; } = [];
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	/// <summary>Number of charge requests that still have at least one pending line.</summary>
	public int PendingCount { get; set; }
	/// <summary>Number of charge requests where all lines are actioned (approved or rejected).</summary>
	public int ProcessedCount { get; set; }
	public string Message { get; set; } = string.Empty;
}

public class ChargeListItem
{
	public Guid ChargeId { get; set; }
	public string ChargeName { get; set; } = string.Empty;
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string PurposeDescription { get; set; } = string.Empty;
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	/// <summary>0 = Pending, 1 = Processed</summary>
	public int DisplayStatus { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	/// <summary>Total number of student lines in this charge request.</summary>
	public int StudentCount { get; set; }
	/// <summary>Number of lines that are no longer pending (approved + rejected).</summary>
	public int ProcessedCount { get; set; }
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; } = string.Empty;
	public DateTime RequestedOn { get; set; }
	public string? AdditionalNotes { get; set; }
}

public class ChargeLineListResponse
{
	public bool Success { get; set; } = true;
	public List<ChargeLineListItem> Items { get; set; } = [];
	public long TotalCount { get; set; }
	public string Message { get; set; } = string.Empty;
}

public class ChargeLineListItem
{
	public Guid ChargeId { get; set; }
	public Guid ChargeLineId { get; set; }
	public string ChargeName { get; set; } = string.Empty;
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string GrNumber { get; set; } = string.Empty;
	public string RollNumber { get; set; } = string.Empty;
	public string StandardName { get; set; } = string.Empty;
	public string DivisionName { get; set; } = string.Empty;
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string PurposeDescription { get; set; } = string.Empty;
	public DateTime DueDate { get; set; }
	public int Status { get; set; }
}

public class ChargeDetailResponse
{
	public bool Success { get; set; } = true;
	public ChargeDetailData Data { get; set; } = new();
	public string Message { get; set; } = string.Empty;
}

public class ChargeDetailData
{
	public Guid ChargeId { get; set; }
	public string ChargeName { get; set; } = string.Empty;
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; } = string.Empty;
	public string PurposeDescription { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	public int Status { get; set; }
	public string? AdditionalNotes { get; set; }
	public ChargeAudienceDto Audience { get; set; } = new();
	public int AppliedCount { get; set; }
	public bool IsApprovalRequired { get; set; }
	public List<ChargeStudentLineDto> Students { get; set; } = [];
	public RequestorInfo Requestor { get; set; } = new();
	public ApprovalDetails ApprovalDetails { get; set; } = new();
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
}

public class ChargeStudentLineDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string GrNumber { get; set; } = string.Empty;
	public int? RollNumber { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public string DivisionName { get; set; } = string.Empty;
	public int Status { get; set; }
}

public class RequestorInfo
{
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; } = string.Empty;
	public string RequestorRole { get; set; } = "User";
	public DateTime RequestedOn { get; set; }
}

public class ApprovalDetails
{
	public Guid? ApprovedBy { get; set; }
	public string? ApprovedByName { get; set; }
	public DateTime? ApprovedOn { get; set; }
	public string? ApprovalNotes { get; set; }
}

public class ApprovalResponse
{
	public bool Success { get; set; } = true;
	public ApprovalData Data { get; set; } = new();
	public string Message { get; set; } = string.Empty;
}

public class ApprovalData
{
	public Guid ChargeId { get; set; }
	public int Status { get; set; }
	public int ProcessedCount { get; set; }
	public int RemainingPendingCount { get; set; }
	public Guid ApprovedBy { get; set; }
	public string ApprovedByName { get; set; } = string.Empty;
	public DateTime ApprovedOn { get; set; }
	public string? ApprovalNotes { get; set; }
}

public class RejectionResponse
{
	public bool Success { get; set; } = true;
	public RejectionData Data { get; set; } = new();
	public string Message { get; set; } = string.Empty;
}

public class RejectionData
{
	public Guid ChargeId { get; set; }
	public int Status { get; set; }
	public int ProcessedCount { get; set; }
	public int RemainingPendingCount { get; set; }
	public Guid RejectedBy { get; set; }
	public string RejectedByName { get; set; } = string.Empty;
	public DateTime RejectedOn { get; set; }
	public string RejectionReason { get; set; } = string.Empty;
}
