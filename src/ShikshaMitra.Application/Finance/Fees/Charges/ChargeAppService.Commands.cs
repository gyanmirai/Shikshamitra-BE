using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input)
	{
		await EnsureAcademicYearExistsAsync(academicYearId);

		var audienceDto = await ResolveAudienceDtoAsync(input);
		var audience = ChargeAudienceMapper.ToDomain(audienceDto);
		var snapshot = ChargeAudienceMapper.SerializeSnapshot(audienceDto);

		var (request, lines) = await chargeRequestManager.CreateAsync(
			CurrentTenant.Id, academicYearId,
			input.ChargeName, input.ChargeCategoryId, input.PurposeDescription,
			input.Amount, input.DueDate, (ChargePriority)input.Priority,
			audience, snapshot, currentUser.Id!.Value, input.AdditionalNotes);

		await CurrentUnitOfWork!.SaveChangesAsync();

		await financeAuditWriter.WriteAsync(
			academicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.AddCharge, $"Charge request created for {lines.Count} student(s).",
			context: new { request.Id, StudentCount = lines.Count });

		logger.LogInformation("Charge request created: {ChargeRequestId}, Lines={Count}", request.Id, lines.Count);

		return await BuildChargeResponseAsync(request, lines.Count,
			lines.All(l => l.Status == ChargeStatus.Pending)
				? "Charge created successfully and pending approval"
				: "Charge created and approved successfully");
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
				request, audience, snapshot,
				input.ChargeName, input.ChargeCategoryId, input.PurposeDescription,
				input.Amount, input.DueDate,
				input.Priority.HasValue ? (ChargePriority)input.Priority.Value : null,
				input.AdditionalNotes, request.AcademicYearId);
		}
		else
		{
			request.Update(input.ChargeName, input.ChargeCategoryId, input.PurposeDescription,
				input.Amount, input.DueDate,
				input.Priority.HasValue ? (ChargePriority)input.Priority.Value : null,
				input.AdditionalNotes);

			var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeId && c.Status == ChargeStatus.Pending);
			var category = await chargeCategoryRepository.FindAsync(request.ChargeCategoryId);
			foreach (var line in lines)
			{
				line.Update(request.ChargeCategoryId, request.PurposeDescription, request.Amount,
					request.DueDate, request.Priority, request.AdditionalNotes);
				if (category is not null && !category.IsApprovalRequired)
					line.AutoApprove();
			}
		}

		await chargeRequestRepository.UpdateAsync(request, autoSave: true);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.Update, "Charge request updated.");

		var lineCount = await chargeRepository.CountAsync(c => c.ChargeRequestId == chargeId);
		return await BuildChargeResponseAsync(request, lineCount, "Charge updated successfully");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	public async Task<ApprovalResponse> ApproveAsync(Guid chargeId, ApproveChargeRequest input)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);

		if (CurrentUser.Id == request.RequestorId && !IsPrivilegedUser())
			throw new BusinessException(FinanceChargeErrorCodes.ChargeApproverCannotBeRequestor)
				.WithData("Message", "The approver must differ from the charge requestor");

		var (processed, remaining) = await chargeRequestManager.ApproveLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.ApprovalNotes);

		await CurrentUnitOfWork!.SaveChangesAsync();

		var approver = await userRepository.FindAsync(currentUser.Id!.Value);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.Approve, $"Approved charge lines for {processed} student(s).",
			reason: input.ApprovalNotes);

		// Publish per-student events for the approved lines
		var approvedLines = await chargeRepository.GetListAsync(
			c => c.ChargeRequestId == request.Id
			  && c.Status == ChargeStatus.Approved
			  && (input.StudentIds == null || input.StudentIds.Contains(c.StudentId)));
		foreach (var line in approvedLines)
		{
			await eventBus.PublishAsync(new ChargeApprovedEto
			{
				ChargeId       = line.Id,
				StudentId      = line.StudentId,
				AcademicYearId = request.AcademicYearId,
				Amount         = line.Amount,
				ChargeName     = request.ChargeName,
				TenantId       = CurrentTenant.Id,
				ApprovedAt     = Clock.Now,
			});
		}

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = request.Id, Status = (int)ChargeStatus.Approved,
				ProcessedCount = processed, RemainingPendingCount = remaining,
				ApprovedBy = currentUser.Id!.Value,
				ApprovedByName = approver?.Name ?? approver?.UserName ?? currentUser.UserName ?? string.Empty,
				ApprovedOn = System.DateTime.UtcNow, ApprovalNotes = input.ApprovalNotes
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

		if (CurrentUser.Id == request.RequestorId && !IsPrivilegedUser())
			throw new BusinessException(FinanceChargeErrorCodes.ChargeApproverCannotBeRequestor)
				.WithData("Message", "The approver must differ from the charge requestor");

		var (processed, remaining) = await chargeRequestManager.RejectLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.RejectionReason);

		await CurrentUnitOfWork!.SaveChangesAsync();

		var rejector = await userRepository.FindAsync(currentUser.Id!.Value);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.Reject, $"Rejected charge lines for {processed} student(s).",
			reason: input.RejectionReason);

		return new RejectionResponse
		{
			Success = true,
			Data = new RejectionData
			{
				ChargeId = request.Id, Status = (int)ChargeStatus.Rejected,
				ProcessedCount = processed, RemainingPendingCount = remaining,
				RejectedBy = currentUser.Id!.Value,
				RejectedByName = rejector?.Name ?? rejector?.UserName ?? currentUser.UserName ?? string.Empty,
				RejectedOn = System.DateTime.UtcNow, RejectionReason = input.RejectionReason
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
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.Delete, "Charge request deleted.");
	}
}
