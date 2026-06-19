using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	public async Task<ChargeListResponse> GetAllAsync(GetChargesRequest input)
	{
		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(
			null, input.BoardId, input.EducationMediumId, null, null))
		{
			return new ChargeListResponse
			{
				Success = true, Items = [], TotalCount = 0,
				CurrentPage = input.Page, PageSize = input.PageSize, TotalPages = 0,
				Message = "Charges retrieved successfully"
			};
		}

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var (items, totalCount) = await chargeRequestRepository.GetChargeRequestsPagedAsync(
			input.Status, input.Page, pageSize,
			input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId, input.Search);
		var statusCounts = await chargeRequestRepository.GetStatusCountsAsync(
			input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

		return new ChargeListResponse
		{
			Success = true, Items = items.Select(MapToListItem).ToList(),
			TotalCount = totalCount, CurrentPage = input.Page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			PendingCount = statusCounts.Pending, ProcessedCount = statusCounts.Processed,
			Message = "Charges retrieved successfully"
		};
	}

	public async Task<ChargeLineListResponse> GetLinesAsync(Guid academicYearId, GetChargeLinesRequest input)
	{
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var query = (await chargeRequestRepository.GetChargeLinesQueryAsync(
			input.Status, input.BoardId, input.EducationMediumId))
			.Where(x => scopedStudentIds.Contains(x.Student.Id));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var lines = await AsyncExecuter.ToListAsync(
			query.OrderByDescending(x => x.Charge.AppliedOn)
				.Skip((input.Page - 1) * pageSize).Take(pageSize));

		var categoryIds = lines.Select(l => l.Charge.ChargeCategoryId).Distinct().ToList();
		var categories = categoryIds.Count > 0
			? await chargeCategoryRepository.GetListAsync(c => categoryIds.Contains(c.Id)) : [];
		var categoryMap = categories.ToDictionary(c => c.Id);

		return new ChargeLineListResponse
		{
			Success = true,
			Items = lines.Select(line =>
			{
				categoryMap.TryGetValue(line.Charge.ChargeCategoryId, out var category);
				return MapToLineListItem(line, category);
			}).ToList(),
			TotalCount = totalCount,
			Message = "Charge lines retrieved successfully"
		};
	}

	public async Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		var detail = await GetChargeRequestDetailsOrThrowAsync(chargeId);
		var audience = ChargeAudienceMapper.DeserializeSnapshot(detail.Request.AudienceSnapshot);

		Volo.Abp.Identity.IdentityUser? lastActor = null;
		var lastLine = detail.Lines.Select(l => l.Charge)
			.Where(c => c.ApprovedBy.HasValue)
			.OrderByDescending(c => c.ApprovedOn)
			.FirstOrDefault();
		if (lastLine?.ApprovedBy is Guid actorId)
			lastActor = await userRepository.FindAsync(actorId);

		return new ChargeDetailResponse
		{
			Success = true,
			Data = new ChargeDetailData
			{
				ChargeId = detail.Request.Id, ChargeName = detail.Request.ChargeName,
				ChargeCategoryId = detail.Request.ChargeCategoryId, CategoryName = detail.Category.Name,
				PurposeDescription = detail.Request.PurposeDescription, Amount = detail.Request.Amount,
				DueDate = detail.Request.DueDate, AppliedOn = detail.Request.AppliedOn,
				Priority = (int)detail.Request.Priority,
				Status = (int)DeriveRequestStatus(detail.Lines.Select(l => l.Charge.Status).ToList()),
				AdditionalNotes = detail.Request.AdditionalNotes, Audience = audience,
				AppliedCount = detail.Lines.Count(l => l.Charge.Status == ChargeStatus.Approved),
				IsApprovalRequired = detail.Category.IsApprovalRequired,
				Students = detail.Lines.Select(MapToStudentLine).ToList(),
				Requestor = new RequestorInfo
				{
					RequestorId = detail.Requestor.Id,
					RequestorName = detail.Requestor.Name ?? detail.Requestor.UserName ?? string.Empty,
					RequestorRole = "User", RequestedOn = detail.Request.AppliedOn
				},
				ApprovalDetails = new ApprovalDetails
				{
					ApprovedBy = lastLine?.ApprovedBy, ApprovedByName = lastActor?.Name ?? lastActor?.UserName,
					ApprovedOn = lastLine?.ApprovedOn, ApprovalNotes = lastLine?.ApprovalNotes
				},
				AcademicYearId = detail.Request.AcademicYearId,
				AcademicYearName = detail.AcademicYear.Name
			},
			Message = "Charge details retrieved successfully"
		};
	}
}
