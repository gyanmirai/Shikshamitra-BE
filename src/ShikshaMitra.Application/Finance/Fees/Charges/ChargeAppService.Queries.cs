using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;
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

		var page = Math.Max(1, input.Page);
		var pageSize = Math.Clamp(input.PageSize, 1, MaxPageSize);
		var readScope = await GetChargeReadScopeAsync(input.AcademicYearId);
		var scopedStudentIds = readScope.OwnStudentIds.Concat(readScope.ReviewerStudentIds).Distinct().ToArray();
		var columnFilters = ChargeColumnFilter.Parse(input.Filters);
		var (items, totalCount, statusCounts) = await chargeRequestRepository.GetChargeRequestsPagedAsync(
			input.AcademicYearId, input.Status, page, pageSize,
			input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId, input.Search,
			input.SortBy, input.SortOrder, scopedStudentIds, columnFilters: columnFilters,
			readScope: readScope);
		if (!input.BoardId.HasValue || !input.EducationMediumId.HasValue)
			statusCounts = await chargeRequestRepository.GetStatusCountsAsync(
				input.AcademicYearId, input.BoardId, input.EducationMediumId,
				input.StandardId, input.DivisionId, scopedStudentIds, search: input.Search,
				columnFilters: columnFilters, readScope: readScope);
		var pageRequestIds = items.Select(item => item.Request.Id).ToList();
		var pendingByRequest = pageRequestIds.Count == 0
			? new Dictionary<Guid, List<Guid>>()
			: (await chargeRepository.GetListAsync(line =>
				pageRequestIds.Contains(line.ChargeRequestId) && line.Status == ChargeStatus.Pending && line.StudentId.HasValue))
				.GroupBy(line => line.ChargeRequestId)
				.ToDictionary(group => group.Key, group => group.Select(line => line.StudentId!.Value).ToList());
		var actionPermissions = new Dictionary<string, bool>();
		if (items.Any(item => item.PendingCount == item.StudentCount))
		{
			actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update] =
				await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update);
			actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete] =
				await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete);
		}
		var reviewRows = items.Where(item => item.PendingCount > 0 &&
			CurrentUser.Id != item.Request.RequestorId).ToList();
		if (reviewRows.Count > 0)
		{
			actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve] =
				await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve);
			actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject] =
				await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject);
		}
		if (items.Any(item => item.CanCancel))
			actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Void] =
				await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Void);
		ChargeReviewPageCache? reviewCache = null;
		if (CurrentUser.Id.HasValue && reviewRows.Count > 0 &&
			(actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve] ||
			 actionPermissions[ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject]))
		{
			var pendingIds = reviewRows.SelectMany(item =>
				pendingByRequest.GetValueOrDefault(item.Request.Id) ?? []).Distinct().ToArray();
			var isAdmin = await userScopeService.IsAdminUserAsync(CurrentUser.Id.Value);
			var studentsById = isAdmin ? new Dictionary<Guid, Student>() :
				(await studentRepository.GetListAsync(student => pendingIds.Contains(student.Id)))
					.ToDictionary(student => student.Id);
			if (!isAdmin) financeReviewerAccessService.PrepareReviewerAccessForStudents(pendingIds);
			reviewCache = new ChargeReviewPageCache(studentsById, isAdmin);
		}
		var mappedItems = new System.Collections.Generic.List<ChargeListItem>();
		foreach (var item in items)
		{
			var dto = MapToListItem(item);
			var pendingStudentIds = pendingByRequest.GetValueOrDefault(item.Request.Id) ?? [];
			dto.AvailableActions = await GetAvailableActionsAsync(
				item.Request.RequestorId, item.Request.AcademicYearId, pendingStudentIds,
				item.PendingCount > 0, item.PendingCount == item.StudentCount,
				item.CanCancel, actionPermissions, reviewCache);
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
		var readScope = await GetChargeReadScopeAsync(academicYearId);
		var ownStudentIds = readScope.OwnStudentIds.ToArray();
		var reviewerStudentIds = readScope.ReviewerStudentIds.ToArray();
		var reportingMakerIds = readScope.ReportingMakerIds.ToArray();
		var nonEmployeeMakerIds = readScope.NonEmployeeMakerIds;
		var requests = await chargeRequestRepository.GetQueryableAsync();

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var query = (await chargeRequestRepository.GetChargeLinesQueryAsync(
			academicYearId, input.Status, input.BoardId, input.EducationMediumId))
			.Where(x => Enumerable.Contains(ownStudentIds, x.Student.Id) ||
				Enumerable.Contains(reviewerStudentIds, x.Student.Id) &&
				requests.Any(request => request.Id == x.Charge.ChargeRequestId &&
					(Enumerable.Contains(reportingMakerIds, request.RequestorId) ||
					 nonEmployeeMakerIds.Contains(request.RequestorId))));

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
		await EnsureRequestAccessibleAsync(chargeId);
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
		var canCancel = await CanCancelAsync(detail.Lines.Select(line => line.Charge).ToList());
		var availableActions = await GetAvailableActionsAsync(
			detail.Request.RequestorId, detail.Request.AcademicYearId,
			detail.Lines.Where(line => line.Charge.Status == ChargeStatus.Pending)
				.Where(line => line.Charge.StudentId.HasValue).Select(line => line.Charge.StudentId!.Value).ToList(), hasPending, allPending, canCancel);

		return new ChargeDetailResponse
		{
			Success = true,
			Data = new ChargeDetailData
			{
				ChargeId = detail.Request.Id, ChargeName = detail.Request.ChargeName,
				ChargeCategoryId = detail.Request.ChargeCategoryId, CategoryName = detail.Category.Name,
				PurposeDescription = detail.Request.PurposeDescription, Amount = detail.Request.Amount,
				DueDate = detail.Request.DueDate!.Value, AppliedOn = detail.Request.AppliedOn,
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
