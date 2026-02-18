using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.ConcessionRequests;

namespace ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;

// ─── Step 1 POST Response ─────────────────────────────────────────────────────

public class CreateBasicDetailsResponse
{
	public bool Success { get; set; }
	public CreateBasicDetailsData Data { get; set; }
}

public class CreateBasicDetailsData
{
	public Guid ConcessionRequestId { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid StudentId { get; set; }
	public string AcademicYear { get; set; }
	public int Status { get; set; }
	public int CurrentStep { get; set; }
	public List<int> CompletedSteps { get; set; } = new();
	public DateTime CreatedAt { get; set; }
}

// ─── Step 1 GET Response ──────────────────────────────────────────────────────

public class GetBasicDetailsResponse
{
	public bool Success { get; set; }
	public GetBasicDetailsData Data { get; set; }
}

public class GetBasicDetailsData
{
	public Guid ConcessionRequestId { get; set; }
	public string AcademicYear { get; set; }
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
	public Guid? FeeStructureId { get; set; }
	public string FeeStructureName { get; set; }
	public List<FeeHeadObjectDto> FeeHeads { get; set; } = new();
	public decimal Totalamount { get; set; }
	public int Status { get; set; }
	public int CurrentStep { get; set; }
	public List<int> CompletedSteps { get; set; } = new();
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
}

public class FeeHeadObjectDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; }
	public decimal Amount { get; set; }
}

// ─── Step 2 Response ──────────────────────────────────────────────────────────

public class SaveConcessionTypesResponse
{
	public bool Success { get; set; }
	public SaveConcessionTypesData Data { get; set; }
}

public class SaveConcessionTypesData
{
	public Guid ConcessionRequestId { get; set; }
	public List<SelectedConcessionTypeDto> SelectedConcessionTypes { get; set; } = new();
}

public class SelectedConcessionTypeDto
{
	public Guid ConcessionTypeId { get; set; }
	public string Name { get; set; }
}

// ─── Step 3 Response ──────────────────────────────────────────────────────────

public class SaveFeeHeadsDataResponse
{
	public bool Success { get; set; }
}

// ─── Step 4 Response ──────────────────────────────────────────────────────────

public class SaveValueEntryResponse
{
	public bool Success { get; set; }
	public SaveValueEntryData Data { get; set; }
}

public class SaveValueEntryData
{
	public Guid ConcessionRequestId { get; set; }
	public int RequestStatus { get; set; }
	public List<ConcessionTypeValueResult> ConcessionTypeValues { get; set; } = new();
	public double GrandTotalDiscountAmount { get; set; }
	public double GrandTotalNetAmount { get; set; }
}

public class ConcessionTypeValueResult
{
	public Guid ConcessionTypeId { get; set; }
	public string ConcessionTypeName { get; set; }
	//public int ConcessionType { get; set; }
	public List<FeeHeadValueResult> FeeHeadValues { get; set; } = new();
	public double TotalDiscountAmount { get; set; }
	public double TotalNetAmount { get; set; }
}

public class FeeHeadValueResult
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; }
	public double AnnualBaseAmount { get; set; }
	public int BasedOn { get; set; }
	public double Amount { get; set; }
	public double DiscountAmount { get; set; }
	public double NetAmount { get; set; }
}

// ─── Get Student Concession Requests ─────────────────────────────────────────

public class ConcessionListResponse
{
	public StudentBasicDetailsDto StudentDetails { get; set; }
	public ApplicationConcessionBasicDetail ConcessionBasicDetail { get; set; }
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<ConcessionRequestItemDto> Items { get; set; } = new();
}

public class StudentBasicDetailsDto
{
	public Guid StudentId { get; set; }
	public string Name { get; set; }
	public string RollNo { get; set; }
	public string GrNumber { get; set; }
	public string StandardName { get; set; }
	public string DivisionName { get; set; }
}

public class ApplicationConcessionBasicDetail
{
	public int TotalApplications { get; set; }
	public int PendingApplications { get; set; }
	public int ApprovedApplications { get; set; }
	public int RejectedApplications { get; set; }
}

public class ConcessionRequestItemDto
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	//public int ConcessionType { get; set; }
	public string ConcessionTypeName { get; set; }
	public int Status { get; set; }
	public double TotalAmount { get; set; }
	public List<FeeHeadItemDto> FeeHeads { get; set; } = new();
	public List<SubmittedDocumentDto> SubmittedDocuments { get; set; } = new();
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public string? Message { get; set; }
	public DateTime? ApprovedAt { get; set; }
}

public class FeeHeadItemDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; }
	public double Amount { get; set; }
	public double NetAmount { get; set; }
	public string? Description { get; set; }
}

public class SubmittedDocumentDto
{
	public Guid DocumentId { get; set; }
	public string DocumentName { get; set; }
	public string DocumentUrl { get; set; }
	public DateTime UploadedAt { get; set; }
}

// ─── Approve / Reject Responses ───────────────────────────────────────────────

public class ApprovalResponse
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	public int Status { get; set; }
	public string StatusName { get; set; }
	public DateTime ApprovedAt { get; set; }
	public Guid ApproverId { get; set; }
	public string? ApproverMessage { get; set; }
	public string Message { get; set; }
}

public class RejectionResponse
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	public int Status { get; set; }
	public string StatusName { get; set; }
	public DateTime RejectedAt { get; set; }
	public Guid RejectorId { get; set; }
	public string RejectionReason { get; set; }
	public string Message { get; set; }
}

// ─── Get Concession Applications List ────────────────────────────────────────

public class ConcessionApplicationsResponse
{
	public int PendingCount { get; set; }
	public int ApprovedCount { get; set; }
	public int RejectedCount { get; set; }
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<ConcessionApplicationDto> Items { get; set; } = new();
}

public class ConcessionApplicationDto
{
	public Guid ConcessionRequestId { get; set; }
	public string StudentName { get; set; }
	public string RollNo { get; set; }
	public string? GrNumber { get; set; }
	public string StandardName { get; set; }
	public string DivisionName { get; set; }
	public string Email { get; set; }
	public string ContactNo { get; set; }
	public int TotalConcessionApplication { get; set; }
}
