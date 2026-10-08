using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Events;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	[UnitOfWork(IsDisabled = true)]
	public async Task<ChargeResponse> CreateAsync(Guid academicYearId, CreateChargeRequest input)
	{
		var autoApprove = await userScopeService.IsAdminUserAsync(currentUser.Id!.Value);
		if (autoApprove)
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve);
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		await EnsureAcademicYearExistsAsync(academicYearId);

		var audienceDto = await ResolveAudienceDtoAsync(input);
		var audience = ChargeAudienceMapper.ToDomain(audienceDto);
		var snapshot = ChargeAudienceMapper.SerializeSnapshot(audienceDto);
		var resolvedStudentIds = await chargeAudienceResolver.ResolveAsync(audience, academicYearId);
		await EnsureLinesAccessibleAsync(resolvedStudentIds);

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
		if (autoApprove)
		{
			await chargeRequestManager.ApproveLinesAsync(request, null, currentUser.Id!.Value,
				"Automatically approved on administrator creation.");
			await chargeRequestRepository.UpdateAsync(request, autoSave: false);
			await CurrentUnitOfWork!.SaveChangesAsync();
			await financeAuditWriter.WriteAsync(
				academicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
				FinanceAuditActions.Approve, $"Automatically approved charge lines for {lines.Count} student(s).",
				reason: "Administrator creation; automatic approval.");
		}
		await uow.CompleteAsync();
		if (autoApprove)
			await PublishApprovedEventsAsync(request, lines);

		logger.LogInformation("Charge request created: {ChargeRequestId}, Lines={Count}", request.Id, lines.Count);

		return await BuildChargeResponseAsync(request, lines.Count, autoApprove
			? "Charge created and approved successfully"
			: "Charge created successfully and pending approval");
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	public async Task<ChargeResponse> UpdateAsync(Guid chargeId, UpdateChargeRequest input)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		await EnsureRequestAccessibleAsync(chargeId);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);
		await EnsureAllLinesPendingOrThrowAsync(chargeId, forDelete: false);

		if (input.Audience is not null)
		{
			var audience = ChargeAudienceMapper.ToDomain(input.Audience);
			var resolvedStudentIds = await chargeAudienceResolver.ResolveAsync(audience, request.AcademicYearId);
			await EnsureLinesAccessibleAsync(resolvedStudentIds);
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
			await chargeRequestManager.SetIncomeCategoryFromChargeCategoryAsync(request);

			var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeId && c.Status == ChargeStatus.Pending);
			foreach (var line in lines)
			{
				line.SetIncomeCategory(request.IncomeCategoryId!.Value, request.IncomeCategoryName!);
				line.Update(request.ChargeCategoryId, request.PurposeDescription, request.Amount,
					request.DueDate, request.Priority, request.AdditionalNotes);
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
		await EnsureRequestAccessibleAsync(chargeId);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);

		if (CurrentUser.Id == request.RequestorId)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeApproverCannotBeRequestor)
				.WithData("Message", "The approver must differ from the charge requestor");

		var pendingLines = await chargeRepository.GetListAsync(
			c => c.ChargeRequestId == request.Id && c.Status == ChargeStatus.Pending);
		var approvedLines = input.StudentIds is { Count: > 0 }
			? pendingLines.Where(line => input.StudentIds.Contains(line.StudentId)).ToList()
			: pendingLines;
		var (processed, remaining) = await chargeRequestManager.ApproveLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.ApprovalNotes);
		await chargeRequestRepository.UpdateAsync(request, autoSave: false);

		await CurrentUnitOfWork!.SaveChangesAsync();

		var approver = await userRepository.FindAsync(currentUser.Id!.Value);

		await financeAuditWriter.WriteAsync(
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.Approve, $"Approved charge lines for {processed} student(s).",
			reason: input.ApprovalNotes);

		await PublishApprovedEventsAsync(request, approvedLines);

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = request.Id, Status = (int)ChargeStatus.Approved,
				WorkflowStatus = await GetWorkflowStatusAsync(await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id)),
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
		await EnsureRequestAccessibleAsync(chargeId);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);

		if (CurrentUser.Id == request.RequestorId)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeApproverCannotBeRequestor)
				.WithData("Message", "The approver must differ from the charge requestor");

		var (processed, remaining) = await chargeRequestManager.RejectLinesAsync(
			request, input.StudentIds, currentUser.Id!.Value, input.RejectionReason);
		await chargeRequestRepository.UpdateAsync(request, autoSave: false);

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
				WorkflowStatus = await GetWorkflowStatusAsync(await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id)),
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Void)]
	public async Task<ApprovalResponse> VoidAsync(Guid chargeId, VoidChargeRequest input)
	{
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		await EnsureRequestAccessibleAsync(chargeId);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);

		var approvedLines = await chargeRepository.GetListAsync(
			c => c.ChargeRequestId == chargeId && c.Status == ChargeStatus.Approved);
		if (input.StudentIds is { Count: > 0 })
		{
			var requested = input.StudentIds.ToHashSet();
			approvedLines = approvedLines.Where(line => requested.Contains(line.StudentId)).ToList();
			if (approvedLines.Count != requested.Count)
				throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeLineSubset);
		}
		if (approvedLines.Count == 0)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCannotBeVoided);

		var lineIds = approvedLines.Select(line => line.Id).ToList();
		var allocations = await chargeCollectionRecordRepository.GetListAsync(x => lineIds.Contains(x.ChargeId));
		var transactionIds = allocations.Select(x => x.FeeRecordMetadataId).Distinct().ToList();
		var reversedIds = (await feeRecordMetadataRepository.GetListAsync(x =>
			transactionIds.Contains(x.Id) && x.Status == FeeCollectionStatus.Reversed))
			.Select(x => x.Id).ToHashSet();
		if (allocations.Any(x => !reversedIds.Contains(x.FeeRecordMetadataId)))
			throw new BusinessException(FinanceChargeErrorCodes.ChargeAlreadyCollected);

		foreach (var line in approvedLines)
			line.Void(currentUser.Id!.Value, input.Reason);
		await chargeRequestRepository.UpdateAsync(request, autoSave: false);
		await financeAuditWriter.WriteAsync(
			request.AcademicYearId, FinanceAuditEntityTypes.FeeCharge, request.Id, request.ChargeName,
			FinanceAuditActions.RemoveCharge, $"Voided charge lines for {approvedLines.Count} student(s).",
			reason: input.Reason);
		await uow.CompleteAsync();

		return new ApprovalResponse
		{
			Success = true,
			Data = new ApprovalData
			{
				ChargeId = request.Id, Status = (int)ChargeStatus.Voided,
				WorkflowStatus = await GetWorkflowStatusAsync(await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id)),
				ProcessedCount = approvedLines.Count,
				RemainingPendingCount = await chargeRepository.CountAsync(
					c => c.ChargeRequestId == chargeId && c.Status == ChargeStatus.Pending),
				ApprovedBy = currentUser.Id.Value,
				ApprovedByName = currentUser.Name ?? currentUser.UserName ?? string.Empty,
				ApprovedOn = Clock.Now,
				ApprovalNotes = input.Reason
			},
			Message = $"Charge voided for {approvedLines.Count} student(s)"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete)]
	public async Task DeleteAsync(Guid chargeId)
	{
		var request = await GetChargeRequestOrThrowAsync(chargeId);
		await EnsureRequestAccessibleAsync(chargeId);
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

	private async Task PublishApprovedEventsAsync(ChargeRequest request, IEnumerable<Charge> lines)
	{
		foreach (var line in lines)
		{
			await eventBus.PublishAsync(new ChargeApprovedEto
			{
				ChargeId = line.Id,
				StudentId = line.StudentId,
				AcademicYearId = request.AcademicYearId,
				Amount = line.Amount,
				ChargeName = request.ChargeName,
				TenantId = CurrentTenant.Id,
				ApprovedAt = (line.ApprovedOn ?? Clock.Now).UtcDateTime,
			});
		}
	}
}
