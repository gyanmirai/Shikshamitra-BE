using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class ChargeResponse
{
	public bool Success { get; set; } = true;
	public ChargeData Data { get; set; }
	public string Message { get; set; }
}

public class ChargeData
{
	public Guid ChargeId { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; }
	public string PurposeDescription { get; set; }
	public decimal Amount { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	public int Status { get; set; }
	public Guid BoardId { get; set; }
	public string BoardName { get; set; }
	public Guid EducationMediumId { get; set; }
	public string EducationMediumName { get; set; }
	public Guid StandardId { get; set; }
	public string StandardName { get; set; }
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string GrNumber { get; set; }
	public string RollNumber { get; set; }
	public string? AdditionalNotes { get; set; }
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; }
	public string RequestorRole { get; set; }
	public DateTime RequestedOn { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public class ChargeListResponse
{
	public bool Success { get; set; } = true;
	public List<ChargeListItem> Items { get; set; }
	public long TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public int PendingCount { get; set; }
	public int ApprovedCount { get; set; }
	public int RejectedCount { get; set; }
	public string Message { get; set; }
}

public class ChargeListItem
{
	public Guid ChargeId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string GrNumber { get; set; }
	public string RollNumber { get; set; }
	public Guid BoardId { get; set; }
	public string BoardName { get; set; }
	public Guid EducationMediumId { get; set; }
	public string EducationMediumName { get; set; }
	public Guid StandardId { get; set; }
	public string StandardName { get; set; }
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; }
	public decimal Amount { get; set; }
	public string PurposeDescription { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	public int Status { get; set; }
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; }
	public string RequestorRole { get; set; }
	public DateTime RequestedOn { get; set; }
	public string? AdditionalNotes { get; set; }
}

public class ChargeDetailResponse
{
	public bool Success { get; set; } = true;
	public ChargeDetailData Data { get; set; }
	public string Message { get; set; }
}

public class ChargeDetailData
{
	public Guid ChargeId { get; set; }
	public Guid ChargeCategoryId { get; set; }
	public string CategoryName { get; set; }
	public string PurposeDescription { get; set; }
	public decimal Amount { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime AppliedOn { get; set; }
	public int Priority { get; set; }
	public int Status { get; set; }
	public string? AdditionalNotes { get; set; }
	public StudentInfo Student { get; set; }
	public RequestorInfo Requestor { get; set; }
	public ApprovalDetails ApprovalDetails { get; set; }
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public class StudentInfo
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string GrNumber { get; set; }
	public string RollNumber { get; set; }
	public Guid BoardId { get; set; }
	public string BoardName { get; set; }
	public Guid EducationMediumId { get; set; }
	public string EducationMediumName { get; set; }
	public Guid StandardId { get; set; }
	public string StandardName { get; set; }
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; }
	public string? ContactNumber { get; set; }
	public string? EmailAddress { get; set; }
}

public class RequestorInfo
{
	public Guid RequestorId { get; set; }
	public string RequestorName { get; set; }
	public string RequestorRole { get; set; }
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
	public ApprovalData Data { get; set; }
	public string Message { get; set; }
}

public class ApprovalData
{
	public Guid ChargeId { get; set; }
	public int Status { get; set; }
	public Guid ApprovedBy { get; set; }
	public string ApprovedByName { get; set; }
	public DateTime ApprovedOn { get; set; }
	public string? ApprovalNotes { get; set; }
}

public class RejectionResponse
{
	public bool Success { get; set; } = true;
	public RejectionData Data { get; set; }
	public string Message { get; set; }
}

public class RejectionData
{
	public Guid ChargeId { get; set; }
	public int Status { get; set; }
	public Guid RejectedBy { get; set; }
	public string RejectedByName { get; set; }
	public DateTime RejectedOn { get; set; }
	public string RejectionReason { get; set; }
}
