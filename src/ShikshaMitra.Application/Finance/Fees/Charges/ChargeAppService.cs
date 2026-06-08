using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Charges;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
public class ChargeAppService(
	IChargeRequestRepository chargeRequestRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	IRepository<Student, Guid> studentRepository,
	FinanceScopeAccessService financeScopeAccessService,
	ICurrentUser currentUser,
	ChargeRequestManager chargeRequestManager,
	FinanceAuditWriter financeAuditWriter,
	ILogger<ChargeAppService> logger)
	: ApplicationService, IChargeAppService
{
	private const int MaxPageSize = 100;

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input)
	{
		await EnsureAcademicYearExistsAsync(academicYearId);

		var audienceDto = await ResolveAudienceDtoAsync(input);
		var audience = ChargeAudienceMapper.ToDomain(audienceDto);
		var snapshot = ChargeAudienceMapper.SerializeSnapshot(audienceDto);

		var (request, lines) = await chargeRequestManager.CreateAsync(
			CurrentTenant.Id,
			academicYearId,
			input.ChargeName,
			input.ChargeCategoryId,
			input.PurposeDescription,
			input.Amount,
			input.DueDate,
			(ChargePriority)input.Priority,
			audience,
			snapshot,
			currentUser.Id!.Value,
			input.AdditionalNotes);

		await CurrentUnitOfWork!.SaveChangesAsync();

		await financeAuditWriter.WriteAsync(
			academicYearId,
			FinanceAuditEntityTypes.FeeCharge,
			request.Id,
			request.ChargeName,
			FinanceAuditActions.AddCharge,
			$"Charge request created for {lines.Count} student(s).",
			context: new { request.Id, StudentCount = lines.Count });

		logger.LogInformation("Charge request created: {ChargeRequestId}, Lines={Count}", request.Id, lines.Count);

		return await BuildChargeResponseAsync(request, lines.Count,
			lines.All(l => l.Status == ChargeStatus.Pending)
				? "Charge created successfully and pending approval"
				: "Charge created and approved successfully");
	}

	public async Task<ChargeListResponse> GetAllAsync(Guid academicYearId, GetChargesRequest input)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			academicYearId,
			input.BoardId,
			input.EducationMediumId,
			null,
			null);

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var (items, totalCount) = await chargeRequestRepository.GetChargeRequestsPagedAsync(
			academicYearId, input.Status, input.Page, pageSize,
			input.BoardId, input.EducationMediumId, input.Search);
		var statusCounts = await chargeRequestRepository.GetStatusCountsAsync(academicYearId);

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

	public async Task<ChargeLineListResponse> GetLinesAsync(Guid academicYearId, GetChargeLinesRequest input)
	{
		await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
			academicYearId,
			input.BoardId,
			input.EducationMediumId,
			null,
			null);

		var pageSize = Math.Min(input.PageSize, MaxPageSize);
		var query = await chargeRequestRepository.GetChargeLinesQueryAsync(
			academicYearId, input.Status, input.BoardId, input.EducationMediumId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var lines = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.Charge.AppliedOn)
				.Skip((input.Page - 1) * pageSize)
				.Take(pageSize));

		var categoryIds = lines.Select(l => l.Charge.ChargeCategoryId).Distinct().ToList();
		var categories = categoryIds.Count > 0
			? await chargeCategoryRepository.GetListAsync(c => categoryIds.Contains(c.Id))
			: [];
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
		var category = detail.Category;
		var audience = ChargeAudienceMapper.DeserializeSnapshot(detail.Request.AudienceSnapshot);

		IdentityUser? lastActor = null;
		var lastLine = detail.Lines
			.Select(l => l.Charge)
			.Where(c => c.ApprovedBy.HasValue)
			.OrderByDescending(c => c.ApprovedOn)
			.FirstOrDefault();

		if (lastLine?.ApprovedBy is Guid actorId)
		{
			lastActor = await userRepository.FindAsync(actorId);
		}

		return new ChargeDetailResponse
		{
			Success = true,
			Data = new ChargeDetailData
			{
				ChargeId = detail.Request.Id,
				ChargeName = detail.Request.ChargeName,
				ChargeCategoryId = detail.Request.ChargeCategoryId,
				CategoryName = category.Name,
				PurposeDescription = detail.Request.PurposeDescription,
				Amount = detail.Request.Amount,
				DueDate = detail.Request.DueDate,
				AppliedOn = detail.Request.AppliedOn,
				Priority = (int)detail.Request.Priority,
				Status = (int)DeriveRequestStatus(detail.Lines.Select(l => l.Charge.Status).ToList()),
				AdditionalNotes = detail.Request.AdditionalNotes,
				Audience = audience,
				AppliedCount = detail.Lines.Count(l => l.Charge.Status == ChargeStatus.Approved),
				IsApprovalRequired = category.IsApprovalRequired,
				Students = detail.Lines.Select(MapToStudentLine).ToList(),
				Requestor = new RequestorInfo
				{
					RequestorId = detail.Requestor.Id,
					RequestorName = detail.Requestor.Name ?? detail.Requestor.UserName ?? string.Empty,
					RequestorRole = "User",
					RequestedOn = detail.Request.AppliedOn
				},
				ApprovalDetails = new ApprovalDetails
				{
					ApprovedBy = lastLine?.ApprovedBy,
					ApprovedByName = lastActor?.Name ?? lastActor?.UserName,
					ApprovedOn = lastLine?.ApprovedOn,
					ApprovalNotes = lastLine?.ApprovalNotes
				},
				AcademicYearId = detail.Request.AcademicYearId,
				AcademicYearName = detail.AcademicYear.Name
			},
			Message = "Charge details retrieved successfully"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public async Task<ChargeResponse> UpdateAsync(Guid chargeId, UpdateChargeRequest input)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		await EnsureAllLinesPendingOrThrowAsync(chargeId, forDelete: false);

		if (input.Audience is not null)
		{
			var audience = ChargeAudienceMapper.ToDomain(input.Audience);
			var snapshot = ChargeAudienceMapper.SerializeSnapshot(input.Audience);
			await chargeRequestManager.SyncAudienceAsync(
				request,
				audience,
				snapshot,
				input.ChargeName,
				input.ChargeCategoryId,
				input.PurposeDescription,
				input.Amount,
				input.DueDate,
				input.Priority.HasValue ? (ChargePriority)input.Priority.Value : null,
				input.AdditionalNotes,
				request.AcademicYearId);
		}
		else
		{
			request.Update(
				input.ChargeName,
				input.ChargeCategoryId,
				input.PurposeDescription,
				input.Amount,
				input.DueDate,
				input.Priority.HasValue ? (ChargePriority)input.Priority.Value : null,
				input.AdditionalNotes);

			var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeId && c.Status == ChargeStatus.Pending);
			var category = await chargeCategoryRepository.FindAsync(request.ChargeCategoryId);
			foreach (var line in lines)
			{
				line.Update(
					request.ChargeCategoryId,
					request.PurposeDescription,
					request.Amount,
					request.DueDate,
					request.Priority,
					request.AdditionalNotes);

				if (category is not null && !category.IsApprovalRequired)
				{
					line.AutoApprove();
				}
			}
		}

		await chargeRequestRepository.UpdateAsync(request, autoSave: true);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId,
			FinanceAuditEntityTypes.FeeCharge,
			request.Id,
			request.ChargeName,
			FinanceAuditActions.Update,
			"Charge request updated.");

		var lineCount = await chargeRepository.CountAsync(c => c.ChargeRequestId == chargeId);
		return await BuildChargeResponseAsync(request, lineCount, "Charge updated successfully");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	public async Task<ApprovalResponse> ApproveAsync(Guid chargeId, ApproveChargeRequest input)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		var (processed, remaining) = await chargeRequestManager.ApproveLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.ApprovalNotes);

		await CurrentUnitOfWork!.SaveChangesAsync();

		var approver = await userRepository.FindAsync(currentUser.Id!.Value);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId,
			FinanceAuditEntityTypes.FeeCharge,
			request.Id,
			request.ChargeName,
			FinanceAuditActions.Approve,
			$"Approved charge lines for {processed} student(s).",
			reason: input.ApprovalNotes);

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = request.Id,
				Status = (int)ChargeStatus.Approved,
				ProcessedCount = processed,
				RemainingPendingCount = remaining,
				ApprovedBy = currentUser.Id!.Value,
				ApprovedByName = approver?.Name ?? approver?.UserName ?? currentUser.UserName ?? string.Empty,
				ApprovedOn = DateTime.UtcNow,
				ApprovalNotes = input.ApprovalNotes
			},
			Message = processed == 1
				? "Charge approved successfully for 1 student"
				: $"Charge approved successfully for {processed} students"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject)]
	public async Task<RejectionResponse> RejectAsync(Guid chargeId, RejectChargeRequest input)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		var (processed, remaining) = await chargeRequestManager.RejectLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.RejectionReason);

		await CurrentUnitOfWork!.SaveChangesAsync();

		var rejector = await userRepository.FindAsync(currentUser.Id!.Value);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId,
			FinanceAuditEntityTypes.FeeCharge,
			request.Id,
			request.ChargeName,
			FinanceAuditActions.Reject,
			$"Rejected charge lines for {processed} student(s).",
			reason: input.RejectionReason);

		return new RejectionResponse
		{
			Success = true,
			Data = new RejectionData
			{
				ChargeId = request.Id,
				Status = (int)ChargeStatus.Rejected,
				ProcessedCount = processed,
				RemainingPendingCount = remaining,
				RejectedBy = currentUser.Id!.Value,
				RejectedByName = rejector?.Name ?? rejector?.UserName ?? currentUser.UserName ?? string.Empty,
				RejectedOn = DateTime.UtcNow,
				RejectionReason = input.RejectionReason
			},
			Message = processed == 1
				? "Charge rejected successfully for 1 student"
				: $"Charge rejected successfully for {processed} students"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete)]
	public async Task DeleteAsync(Guid chargeId)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		await EnsureAllLinesPendingOrThrowAsync(chargeId, forDelete: true);

		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeId);
		foreach (var line in lines)
		{
			line.ValidateCanDelete();
			await chargeRepository.DeleteAsync(line, autoSave: false);
		}

		await chargeRequestRepository.DeleteAsync(request, autoSave: true);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId,
			FinanceAuditEntityTypes.FeeCharge,
			request.Id,
			request.ChargeName,
			FinanceAuditActions.Delete,
			"Charge request deleted.");
	}

	private async Task<ChargeAudienceDto> ResolveAudienceDtoAsync(CreateChargeRequest input)
	{
		if (input.Audience is not null)
		{
			return input.Audience;
		}

		if (!input.StudentId.HasValue)
		{
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience);
		}

		var student = await studentRepository.FindAsync(input.StudentId.Value)
			?? throw new BusinessException(FinanceChargeErrorCodes.StudentNotFound)
				.WithData("StudentId", input.StudentId.Value);

		return ChargeAudienceMapper.BuildLegacyAudience(student, input.StudentId.Value);
	}

	private async Task<ChargeResponse> BuildChargeResponseAsync(ChargeRequest request, int studentCount, string message)
	{
		var category = await chargeCategoryRepository.GetAsync(request.ChargeCategoryId);
		var requestor = await userRepository.GetAsync(request.RequestorId);
		var academicYear = await academicYearRepository.GetAsync(request.AcademicYearId);
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id);
		var status = DeriveRequestStatus(lines.Select(l => l.Status).ToList());

		return new ChargeResponse
		{
			Success = true,
			Data = new ChargeData
			{
				ChargeId = request.Id,
				ChargeName = request.ChargeName,
				ResolvedStudentCount = studentCount,
				ChargeCategoryId = request.ChargeCategoryId,
				CategoryName = category.Name,
				PurposeDescription = request.PurposeDescription,
				Amount = request.Amount,
				DueDate = request.DueDate,
				AppliedOn = request.AppliedOn,
				Priority = (int)request.Priority,
				Status = (int)status,
				BoardId = request.BoardId,
				EducationMediumId = request.EducationMediumId,
				AdditionalNotes = request.AdditionalNotes,
				RequestorId = request.RequestorId,
				RequestorName = requestor.Name ?? requestor.UserName ?? string.Empty,
				RequestedOn = request.AppliedOn,
				AcademicYearId = request.AcademicYearId,
				AcademicYearName = academicYear.Name
			},
			Message = message
		};
	}

	private static ChargeListItem MapToListItem(ChargeRequestJoinResult r)
	{
		var partiallyProcessed = r.PendingCount == 0 && r.ApprovedCount > 0 && r.RejectedCount > 0;
		var status = r.PendingCount > 0
			? ChargeStatus.Pending
			: r.RejectedCount == r.StudentCount
				? ChargeStatus.Rejected
				: ChargeStatus.Approved;

		return new ChargeListItem
		{
			ChargeId = r.Request.Id,
			ChargeName = r.Request.ChargeName,
			BoardId = r.Request.BoardId,
			EducationMediumId = r.Request.EducationMediumId,
			ChargeCategoryId = r.Category.Id,
			CategoryName = r.Category.Name,
			Amount = r.Request.Amount,
			PurposeDescription = r.Request.PurposeDescription,
			DueDate = r.Request.DueDate,
			AppliedOn = r.Request.AppliedOn,
			Priority = (int)r.Request.Priority,
			Status = (int)status,
			StudentCount = r.StudentCount,
			PendingCount = r.PendingCount,
			ApprovedCount = r.ApprovedCount,
			RejectedCount = r.RejectedCount,
			PartiallyProcessed = partiallyProcessed,
			RequestorId = r.Requestor.Id,
			RequestorName = r.Requestor.Name ?? r.Requestor.UserName ?? string.Empty,
			RequestedOn = r.Request.AppliedOn,
			AdditionalNotes = r.Request.AdditionalNotes
		};
	}

	private static ChargeLineListItem MapToLineListItem(ChargeLineJoinResult line, ChargeCategory? category)
	{
		var student = line.Student;
		return new ChargeLineListItem
		{
			ChargeId = line.Charge.ChargeRequestId,
			ChargeLineId = line.Charge.Id,
			ChargeName = line.Charge.ChargeName,
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo?.ToString() ?? string.Empty,
			StandardName = line.Standard.Name,
			DivisionName = line.Division.Name,
			ChargeCategoryId = line.Charge.ChargeCategoryId,
			CategoryName = category?.Name ?? string.Empty,
			Amount = line.Charge.Amount,
			PurposeDescription = line.Charge.PurposeDescription,
			DueDate = line.Charge.DueDate,
			Status = (int)line.Charge.Status
		};
	}

	private static ChargeStudentLineDto MapToStudentLine(ChargeLineJoinResult line)
	{
		var student = line.Student;
		return new ChargeStudentLineDto
		{
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo,
			StandardName = line.Standard.Name,
			DivisionName = line.Division.Name,
			Status = (int)line.Charge.Status
		};
	}

	private static ChargeStatus DeriveRequestStatus(IReadOnlyList<ChargeStatus> lineStatuses)
	{
		if (lineStatuses.Count == 0)
		{
			return ChargeStatus.Pending;
		}

		if (lineStatuses.Any(s => s == ChargeStatus.Pending))
		{
			return ChargeStatus.Pending;
		}

		if (lineStatuses.All(s => s == ChargeStatus.Rejected))
		{
			return ChargeStatus.Rejected;
		}

		return ChargeStatus.Approved;
	}

	private async Task<ChargeRequest> GetChargeRequestOrThrowAsync(Guid chargeRequestId)
	{
		return await chargeRequestRepository.FindAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);
	}

	private async Task<ChargeRequestDetailResult> GetChargeRequestDetailsOrThrowAsync(Guid chargeRequestId)
	{
		return await chargeRequestRepository.GetWithLinesAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);
	}

	private async Task EnsureAcademicYearExistsAsync(Guid academicYearId)
	{
		if (await academicYearRepository.FindAsync(academicYearId) is null)
		{
			throw new BusinessException(FinanceChargeErrorCodes.AcademicYearNotFound)
				.WithData("AcademicYearId", academicYearId);
		}
	}

	private async Task EnsureAllLinesPendingOrThrowAsync(Guid chargeRequestId, bool forDelete)
	{
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeRequestId);
		if (lines.Count == 0)
		{
			return;
		}

		if (forDelete)
		{
			if (lines.Any(l => l.Status != ChargeStatus.Pending))
			{
				throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFullyPending);
			}

			return;
		}

		if (lines.Any(l => l.Status == ChargeStatus.Approved))
		{
			// Updates allowed when some lines approved — audience sync handles restrictions
		}
	}
}
