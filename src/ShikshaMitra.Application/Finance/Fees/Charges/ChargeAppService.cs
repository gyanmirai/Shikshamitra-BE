using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Handles student charge CRUD, approval, and rejection.
/// Business rules (scope derivation, maker-checker) are enforced by
/// <see cref="ChargeManager"/> and the <see cref="Charge"/> aggregate root.
/// </summary>
[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
public class ChargeAppService(
	IChargeRepository chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
	ICurrentUser currentUser,
	ChargeManager chargeManager,
	ILogger<ChargeAppService> logger)
	: ApplicationService, IChargeAppService
{
	private const int MaxPageSize = 100;

	// ── Create ──────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input)
	{
		logger.LogDebug(
			"[ChargeAppService] Creating charge: AcademicYearId={AcademicYearId}, StudentId={StudentId}, CategoryId={CategoryId}, Amount={Amount}",
			academicYearId, input.StudentId, input.ChargeCategoryId, input.Amount);

		await EnsureAcademicYearExistsAsync(academicYearId);

		var charge = await chargeManager.CreateAsync(
			tenantId: CurrentTenant.Id,
			academicYearId: academicYearId,
			studentId: input.StudentId,
			chargeCategoryId: input.ChargeCategoryId,
			purposeDescription: input.PurposeDescription,
			amount: input.Amount,
			dueDate: input.DueDate,
			priority: (ChargePriority)input.Priority,
			requestorId: currentUser.Id!.Value,
			additionalNotes: input.AdditionalNotes);

		logger.LogDebug(
			"[ChargeAppService] Charge created: ChargeId={ChargeId}, Status={Status}",
			charge.Id, charge.Status);

		var detail = await GetChargeDetailsOrThrowAsync(charge.Id);
		return BuildChargeResponse(detail, message: charge.Status == ChargeStatus.Pending
			? "Charge created successfully and pending approval"
			: "Charge created and approved successfully");
	}

	// ── Read ─────────────────────────────────────────────────

	public async Task<ChargeListResponse> GetAllAsync(Guid academicYearId, GetChargesRequest input)
	{
		var pageSize = Math.Min(input.PageSize, MaxPageSize);

		var filteredQuery = await chargeRepository.GetChargesQueryAsync(
			academicYearId,
			input.Status,
			input.BoardId,
			input.EducationMediumId,
			input.Search);
		var statusCounts = await GetStatusCountsAsync(academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(filteredQuery);

		var items = await AsyncExecuter.ToListAsync(
			filteredQuery
				.Skip((input.Page - 1) * pageSize)
				.Take(pageSize));

		return new ChargeListResponse
		{
			Success = true,
			Items = items.Select(MapToListItem).ToList(),
			TotalCount = totalCount,
			CurrentPage = input.Page,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			PendingCount = statusCounts.Pending,
			ApprovedCount = statusCounts.Approved,
			RejectedCount = statusCounts.Rejected,
			Message = "Charges retrieved successfully"
		};
	}

	public async Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		var detail = await GetChargeDetailsOrThrowAsync(chargeId);

		IdentityUser? approver = null;
		if (detail.Charge.ApprovedBy.HasValue)
			approver = await userRepository.FindAsync(detail.Charge.ApprovedBy.Value);

		return new ChargeDetailResponse
		{
			Success = true,
			Data = MapToDetailData(detail, approver),
			Message = "Charge details retrieved successfully"
		};
	}

	// ── Update ───────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public async Task<ChargeResponse> UpdateAsync(Guid chargeId, UpdateChargeRequest input)
	{
		var charge = await GetChargeOrThrowAsync(chargeId);

		charge.Update(
			input.ChargeCategoryId,
			input.PurposeDescription,
			input.Amount,
			input.DueDate,
			input.Priority.HasValue ? (ChargePriority)input.Priority.Value : null,
			input.AdditionalNotes);

		await chargeRepository.UpdateAsync(charge, autoSave: true);

		var detail = await GetChargeDetailsOrThrowAsync(chargeId);
		return BuildChargeResponse(detail, message: "Charge updated successfully");
	}

	// ── Approve / Reject ─────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	public async Task<ApprovalResponse> ApproveAsync(Guid chargeId, ApproveChargeRequest input)
	{
		var charge = await GetChargeOrThrowAsync(chargeId);
		charge.Approve(currentUser.Id!.Value, input.ApprovalNotes);
		await chargeRepository.UpdateAsync(charge);

		var approver = await userRepository.FindAsync(currentUser.Id!.Value);

		logger.LogInformation(
			"[ChargeAppService] Charge approved: ChargeId={ChargeId}, ApprovedBy={ApprovedBy}",
			charge.Id, currentUser.Id.Value);

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = charge.Id,
				Status = (int)ChargeStatus.Approved,
				ApprovedBy = currentUser.Id!.Value,
				ApprovedByName = approver?.Name ?? approver?.UserName ?? currentUser.UserName ?? string.Empty,
				ApprovedOn = charge.ApprovedOn!.Value,
				ApprovalNotes = input.ApprovalNotes
			},
			Message = "Charge approved successfully"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject)]
	public async Task<RejectionResponse> RejectAsync(Guid chargeId, RejectChargeRequest input)
	{
		var charge = await GetChargeOrThrowAsync(chargeId);
		charge.Reject(currentUser.Id!.Value, input.RejectionReason);
		await chargeRepository.UpdateAsync(charge, autoSave: true);

		var rejector = await userRepository.FindAsync(currentUser.Id!.Value);

		logger.LogInformation(
			"[ChargeAppService] Charge rejected: ChargeId={ChargeId}, RejectedBy={RejectedBy}",
			charge.Id, currentUser.Id.Value);

		return new RejectionResponse
		{
			Success = true,
			Data = new RejectionData
			{
				ChargeId = charge.Id,
				Status = (int)ChargeStatus.Rejected,
				RejectedBy = currentUser.Id!.Value,
				RejectedByName = rejector?.Name ?? rejector?.UserName ?? currentUser.UserName ?? string.Empty,
				RejectedOn = charge.ApprovedOn!.Value,
				RejectionReason = input.RejectionReason
			},
			Message = "Charge rejected successfully"
		};
	}

	// ── Delete ───────────────────────────────────────────────

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete)]
	public async Task DeleteAsync(Guid chargeId)
	{
		var charge = await GetChargeOrThrowAsync(chargeId);
		charge.ValidateCanDelete();
		await chargeRepository.DeleteAsync(charge, autoSave: true);
	}

	// ── Private guards ───────────────────────────────────────

	/// <summary>Returns the charge or throws <see cref="BusinessException"/> when not found.</summary>
	private async Task<Charge> GetChargeOrThrowAsync(Guid chargeId)
	{
		return await chargeRepository.FindAsync(chargeId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeNotFound)
				.WithData("ChargeId", chargeId);
	}

	/// <summary>Returns the charge with all joined details or throws when not found.</summary>
	private async Task<ChargeJoinResult> GetChargeDetailsOrThrowAsync(Guid chargeId)
	{
		return await chargeRepository.GetChargeWithDetailsAsync(chargeId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeNotFound)
				.WithData("ChargeId", chargeId);
	}

	private async Task EnsureAcademicYearExistsAsync(Guid academicYearId)
	{
		var exists = await academicYearRepository.FindAsync(academicYearId);
		if (exists is null)
			throw new BusinessException(FinanceChargeErrorCodes.AcademicYearNotFound)
				.WithData("AcademicYearId", academicYearId);
	}

	// ── Query building ───────────────────────────────────────

	private static IQueryable<ChargeJoinResult> ApplySorting(IQueryable<ChargeJoinResult> query, string? sortBy, string? sortOrder)
	{
		// Sorting is deferred to a future implementation with dynamic LINQ or
		// explicit switch on allow-listed column names. Default order (AppliedOn DESC)
		// is provided by the DB index on the charge table.
		return query;
	}

	private async Task<StatusCounts> GetStatusCountsAsync(Guid academicYearId)
	{
		var charges = await chargeRepository.GetQueryableAsync();
		var counts = await AsyncExecuter.ToListAsync(
			charges
				.Where(c => c.AcademicYearId == academicYearId)
				.GroupBy(c => c.Status)
				.Select(g => new { Status = g.Key, Count = g.Count() }));

		return new StatusCounts(
			Pending: counts.FirstOrDefault(x => x.Status == ChargeStatus.Pending)?.Count ?? 0,
			Approved: counts.FirstOrDefault(x => x.Status == ChargeStatus.Approved)?.Count ?? 0,
			Rejected: counts.FirstOrDefault(x => x.Status == ChargeStatus.Rejected)?.Count ?? 0);
	}

	// ── Mapping helpers ──────────────────────────────────────

	private static ChargeResponse BuildChargeResponse(ChargeJoinResult r, string message)
	{
		return new ChargeResponse
		{
			Success = true,
			Data = MapToChargeData(r),
			Message = message
		};
	}

	private static ChargeData MapToChargeData(ChargeJoinResult r)
	{
		return new ChargeData
		{
			ChargeId = r.Charge.Id,
			ChargeCategoryId = r.Charge.ChargeCategoryId,
			CategoryName = r.Category.Name,
			PurposeDescription = r.Charge.PurposeDescription,
			Amount = r.Charge.Amount,
			DueDate = r.Charge.DueDate,
			AppliedOn = r.Charge.AppliedOn,
			Priority = (int)r.Charge.Priority,
			Status = (int)r.Charge.Status,
			BoardId = r.Board.Id,
			BoardName = r.Board.Type.ToString(),
			EducationMediumId = r.Medium.Id,
			EducationMediumName = r.Medium.Name,
			StandardId = r.Standard.Id,
			StandardName = r.Standard.Name,
			DivisionId = r.Division.Id,
			DivisionName = r.Division.Name,
			StudentId = r.Student.Id,
			StudentName = $"{r.Student.FirstName} {r.Student.MiddleName} {r.Student.LastName}".Trim(),
			GrNumber = r.Student.GrNo?.ToString() ?? "",
			RollNumber = r.Student.RollNo?.ToString() ?? "",
			AdditionalNotes = r.Charge.AdditionalNotes,
			RequestorId = r.Requestor.Id,
			RequestorName = r.Requestor.Name ?? r.Requestor.UserName,
			RequestorRole = "User",
			RequestedOn = r.Charge.AppliedOn,
			AcademicYearId = r.Charge.AcademicYearId,
			AcademicYearName = r.AcademicYear.Name,
			CreatedAt = r.Charge.CreationTime,
			UpdatedAt = r.Charge.LastModificationTime ?? r.Charge.CreationTime
		};
	}

	private static ChargeDetailData MapToDetailData(ChargeJoinResult r, IdentityUser? approver)
	{
		return new ChargeDetailData
		{
			ChargeId = r.Charge.Id,
			ChargeCategoryId = r.Charge.ChargeCategoryId,
			CategoryName = r.Category.Name,
			PurposeDescription = r.Charge.PurposeDescription,
			Amount = r.Charge.Amount,
			DueDate = r.Charge.DueDate,
			AppliedOn = r.Charge.AppliedOn,
			Priority = (int)r.Charge.Priority,
			Status = (int)r.Charge.Status,
			AdditionalNotes = r.Charge.AdditionalNotes,
			AcademicYearId = r.Charge.AcademicYearId,
			AcademicYearName = r.AcademicYear.Name,
			CreatedAt = r.Charge.CreationTime,
			UpdatedAt = r.Charge.LastModificationTime ?? r.Charge.CreationTime,
			Student = MapToStudentInfo(r),
			Requestor = new RequestorInfo
			{
				RequestorId = r.Requestor.Id,
				RequestorName = r.Requestor.Name ?? r.Requestor.UserName,
				RequestorRole = "User",
				RequestedOn = r.Charge.AppliedOn
			},
			ApprovalDetails = new ApprovalDetails
			{
				ApprovedBy = r.Charge.ApprovedBy,
				ApprovedByName = approver?.Name ?? approver?.UserName,
				ApprovedOn = r.Charge.ApprovedOn,
				ApprovalNotes = r.Charge.ApprovalNotes
			}
		};
	}

	private static StudentInfo MapToStudentInfo(ChargeJoinResult r)
	{
		return new StudentInfo
		{
			StudentId = r.Student.Id,
			StudentName = $"{r.Student.FirstName} {r.Student.MiddleName} {r.Student.LastName}".Trim(),
			GrNumber = r.Student.GrNo?.ToString() ?? "",
			RollNumber = r.Student.RollNo?.ToString() ?? "",
			BoardId = r.Board.Id,
			BoardName = r.Board.Type.ToString(),
			EducationMediumId = r.Medium.Id,
			EducationMediumName = r.Medium.Name,
			StandardId = r.Standard.Id,
			StandardName = r.Standard.Name,
			DivisionId = r.Division.Id,
			DivisionName = r.Division.Name,
			ContactNumber = r.Student.ContactDetail?.PrimaryContactNo,
			EmailAddress = r.Student.ContactDetail?.Email
		};
	}

	private static ChargeListItem MapToListItem(ChargeJoinResult r)
	{
		return new ChargeListItem
		{
			ChargeId = r.Charge.Id,
			StudentId = r.Student.Id,
			StudentName = $"{r.Student.FirstName} {r.Student.MiddleName} {r.Student.LastName}".Trim(),
			GrNumber = r.Student.GrNo?.ToString() ?? "",
			RollNumber = r.Student.RollNo?.ToString() ?? "",
			BoardId = r.Board.Id,
			BoardName = r.Board.Type.ToString(),
			EducationMediumId = r.Medium.Id,
			EducationMediumName = r.Medium.Name,
			StandardId = r.Standard.Id,
			StandardName = r.Standard.Name,
			DivisionId = r.Division.Id,
			DivisionName = r.Division.Name,
			ChargeCategoryId = r.Category.Id,
			CategoryName = r.Category.Name,
			Amount = r.Charge.Amount,
			PurposeDescription = r.Charge.PurposeDescription,
			DueDate = r.Charge.DueDate,
			AppliedOn = r.Charge.AppliedOn,
			Priority = (int)r.Charge.Priority,
			Status = (int)r.Charge.Status,
			RequestorId = r.Requestor.Id,
			RequestorName = r.Requestor.Name ?? r.Requestor.UserName,
			RequestorRole = "User",
			RequestedOn = r.Charge.AppliedOn,
			AdditionalNotes = r.Charge.AdditionalNotes
		};
	}

	// ── Private types ────────────────────────────────────────

	private record StatusCounts(int Pending, int Approved, int Rejected);
}