using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	public async Task<ChargeListResponse> GetAllAsync(GetChargesRequest input)
	{
		if (!Enum.IsDefined(typeof(ChargeWorkflowStatus), input.Status))
			throw new Volo.Abp.Validation.AbpValidationException("Invalid charge workflow status.");

		if (input.AcademicYearId == Guid.Empty)
			throw new Volo.Abp.Validation.AbpValidationException("academicYearId is required.");

		if (!await financeScopeAccessService.IsAcademicContextAccessibleAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId))
		{
			return new ChargeListResponse
			{
				Success = true, Items = [], TotalCount = 0,
				CurrentPage = input.Page, PageSize = input.PageSize, TotalPages = 0,
				Message = "Charges retrieved successfully"
			};
		}

		var page = Math.Max(1, input.Page);
		var pageSize = Math.Clamp(input.PageSize, 1, MaxPageSize);
		var scopedStudentIds = await GetScopedStudentIdsAsync(input.AcademicYearId);
		var columnFilters = ChargeColumnFilter.Parse(input.Filters);
		var (items, totalCount) = await chargeRequestRepository.GetChargeRequestsPagedAsync(
			input.AcademicYearId, input.Status, page, pageSize,
			input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId, input.Search,
			input.SortBy, input.SortOrder, scopedStudentIds, columnFilters: columnFilters);
		var statusCounts = await chargeRequestRepository.GetStatusCountsAsync(
			input.AcademicYearId, input.BoardId, input.EducationMediumId,
			input.StandardId, input.DivisionId, scopedStudentIds, search: input.Search, columnFilters: columnFilters);
		var mappedItems = new System.Collections.Generic.List<ChargeListItem>();
		foreach (var item in items)
		{
			var dto = MapToListItem(item);
			dto.AvailableActions = await GetAvailableActionsAsync(
				item.Request.RequestorId, item.PendingCount > 0, item.PendingCount == item.StudentCount,
				item.WorkflowStatus == ChargeWorkflowStatus.InProcess);
			mappedItems.Add(dto);
		}

		return new ChargeListResponse
		{
			Success = true, Items = mappedItems,
			TotalCount = totalCount, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			PendingCount = statusCounts.Pending, ProcessedCount = statusCounts.Processed,
			InProcessCount = statusCounts.Processed, CompletedCount = statusCounts.Completed, RejectedCount = statusCounts.Rejected,
			Message = "Charges retrieved successfully"
		};
	}

	public async Task<ChargeLineListResponse> GetLinesAsync(Guid academicYearId, GetChargeLinesRequest input)
	{
		await EnsureAcademicYearExistsAsync(academicYearId);
		var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var query = (await chargeRequestRepository.GetChargeLinesQueryAsync(
			academicYearId, input.Status, input.BoardId, input.EducationMediumId))
			.Where(x => scopedStudentIds.Contains(x.Student.Id));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var lines = await AsyncExecuter.ToListAsync(
			query.OrderByDescending(x => x.Charge.AppliedOn)
				.Skip((input.Page - 1) * pageSize).Take(pageSize));

		var categoryIds = lines.Select(l => l.Charge.ChargeCategoryId).Distinct().ToList();
		var categories = categoryIds.Count > 0
			? await chargeCategoryRepository.GetListAsync(c => categoryIds.Contains(c.Id)) : [];
		var categoryMap = categories.ToDictionary(c => c.Id);
		var collectedAmounts = await GetCollectedAmountsAsync(lines.Select(line => line.Charge.Id));
		var waivedAmounts = await GetWaivedAmountsAsync(lines.Select(line => line.Charge.Id));

		return new ChargeLineListResponse
		{
			Success = true,
			Items = lines.Select(line =>
			{
				categoryMap.TryGetValue(line.Charge.ChargeCategoryId, out var category);
				var item = MapToLineListItem(line, category);
				item.CollectedAmount = collectedAmounts.GetValueOrDefault(line.Charge.Id);
				item.WaivedAmount = waivedAmounts.GetValueOrDefault(line.Charge.Id);
				item.OutstandingAmount = line.Charge.Status == ChargeStatus.Approved
					? Math.Max(0m, line.Charge.Amount - item.CollectedAmount - item.WaivedAmount) : 0m;
				item.WorkflowStatus = ChargeWorkflowStatusResolver.Resolve([new ChargeWorkflowLine(line.Charge.Status, item.OutstandingAmount)]);
				return item;
			}).ToList(),
			TotalCount = totalCount,
			Message = "Charge lines retrieved successfully"
		};
	}

	public async Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		var detail = await GetChargeRequestDetailsOrThrowAsync(chargeId);
		await EnsureLinesAccessibleAsync(detail.Lines.Select(x => x.Charge.StudentId));
		var audience = ChargeAudienceMapper.DeserializeSnapshot(detail.Request.AudienceSnapshot);

		Volo.Abp.Identity.IdentityUser? lastActor = null;
		var lastLine = detail.Lines.Select(l => l.Charge)
			.Where(c => c.ApprovedBy.HasValue)
			.OrderByDescending(c => c.ApprovedOn)
			.FirstOrDefault();
		if (lastLine?.ApprovedBy is Guid actorId)
			lastActor = await userRepository.FindAsync(actorId);

		var allPending = detail.Lines.Count == 0 || detail.Lines.All(x => x.Charge.Status == ChargeStatus.Pending);
		var hasPending = detail.Lines.Any(x => x.Charge.Status == ChargeStatus.Pending);
		var collectedAmounts = await GetCollectedAmountsAsync(detail.Lines.Select(line => line.Charge.Id));
		var waivedAmounts = await GetWaivedAmountsAsync(detail.Lines.Select(line => line.Charge.Id));
		var studentLines = detail.Lines.Select(line =>
		{
			var item = MapToStudentLine(line);
			item.CollectedAmount = collectedAmounts.GetValueOrDefault(line.Charge.Id);
			item.WaivedAmount = waivedAmounts.GetValueOrDefault(line.Charge.Id);
			item.OutstandingAmount = line.Charge.Status == ChargeStatus.Approved
				? Math.Max(0m, line.Charge.Amount - item.CollectedAmount - item.WaivedAmount) : 0m;
			item.WorkflowStatus = ChargeWorkflowStatusResolver.Resolve([new ChargeWorkflowLine(line.Charge.Status, item.OutstandingAmount)]);
			return item;
		}).ToList();
		var workflowStatus = ChargeWorkflowStatusResolver.Resolve(studentLines.Select(line =>
			new ChargeWorkflowLine((ChargeStatus)line.Status, line.OutstandingAmount)));
		var availableActions = await GetAvailableActionsAsync(
			detail.Request.RequestorId, hasPending, allPending, workflowStatus == ChargeWorkflowStatus.InProcess);

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
				IsApprovalRequired = detail.Request.GetProperty<bool>("IsApprovalRequiredAtCreation", true),
				WorkflowStatus = workflowStatus,
				Students = studentLines,
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
				AcademicYearName = detail.AcademicYear.Name,
				ConcurrencyStamp = detail.Request.ConcurrencyStamp,
				AvailableActions = availableActions
			},
			Message = "Charge details retrieved successfully"
		};
	}
}
