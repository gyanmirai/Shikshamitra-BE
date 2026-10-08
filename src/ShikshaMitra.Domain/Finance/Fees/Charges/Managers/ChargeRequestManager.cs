using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
﻿using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeRequestManager(
	IRepository<ChargeRequest, Guid> chargeRequestRepository,
	IRepository<Charge, Guid> chargeRepository,
	IStudentRepository studentRepository,
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	FinanceRoutingManager routingManager,
	ChargeAudienceResolver audienceResolver,
	IGuidGenerator guidGenerator,
	ILogger<ChargeRequestManager> logger) : DomainService
{
	public async Task<(ChargeRequest Request, IReadOnlyList<Charge> Lines)> CreateAsync(
		Guid? tenantId,
		Guid academicYearId,
		string chargeName,
		Guid chargeCategoryId,
		string purposeDescription,
		decimal amount,
		DateTimeOffset dueDate,
		ChargePriority priority,
		ChargeAudience audience,
		string audienceSnapshot,
		Guid requestorId,
		string? additionalNotes = null)
	{
		var category = await GetActiveCategoryAsync(chargeCategoryId, academicYearId);
		var studentIds = await audienceResolver.ResolveAsync(audience, academicYearId);
		var initialStatus = ChargeStatus.Pending;

		var request = new ChargeRequest(
			guidGenerator.Create(),
			tenantId,
			academicYearId,
			chargeName,
			chargeCategoryId,
			purposeDescription,
			amount,
			dueDate,
			priority,
			audience.Scope.BoardId,
			audience.Scope.EducationMediumId,
			requestorId,
			audienceSnapshot,
			additionalNotes);

		await SetIncomeCategoryFromChargeCategoryAsync(request, category);
		await chargeRequestRepository.InsertAsync(request, autoSave: false);

		var lines = await CreateLinesAsync(
			request, studentIds, initialStatus, requestorId, category);

		logger.LogInformation(
			"Charge request created: RequestId={RequestId}, LineCount={LineCount}, Status={Status}",
			request.Id, lines.Count, initialStatus);

		return (request, lines);
	}

	public async Task<IReadOnlyList<Charge>> SyncAudienceAsync(
		ChargeRequest request,
		ChargeAudience audience,
		string audienceSnapshot,
		string? chargeName,
		Guid? chargeCategoryId,
		string? purposeDescription,
		decimal? amount,
		DateTimeOffset? dueDate,
		ChargePriority? priority,
		string? additionalNotes,
		Guid academicYearId)
	{
		var resolvedIds = await audienceResolver.ResolveAsync(audience, academicYearId);
		var existingLines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == request.Id);

		var toRemove = existingLines
			.Where(l => l.Status == ChargeStatus.Pending && !resolvedIds.Contains(l.StudentId))
			.ToList();

		var approvedToRemove = existingLines
			.Where(l => l.Status != ChargeStatus.Pending && !resolvedIds.Contains(l.StudentId))
			.ToList();

		if (approvedToRemove.Count > 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.CannotRemoveAppliedChargeLine)
				.WithData("StudentIds", string.Join(",", approvedToRemove.Select(l => l.StudentId)));
		}

		foreach (var line in toRemove)
		{
			line.ValidateCanDelete();
			await chargeRepository.DeleteAsync(line, autoSave: false);
		}

		request.Update(
			chargeName,
			chargeCategoryId,
			purposeDescription,
			amount,
			dueDate,
			priority,
			additionalNotes,
			audienceSnapshot);

		var category = await GetActiveCategoryAsync(request.ChargeCategoryId, academicYearId);
		await SetIncomeCategoryFromChargeCategoryAsync(request, category);
		var existingStudentIds = existingLines.Select(l => l.StudentId).ToHashSet();
		var newStudentIds = resolvedIds.Where(id => !existingStudentIds.Contains(id)).ToList();

		var initialStatus = ChargeStatus.Pending;
		var newLines = await CreateLinesAsync(
			request,
			newStudentIds,
			initialStatus,
			request.RequestorId,
			category);

		foreach (var line in existingLines.Where(l => l.Status == ChargeStatus.Pending))
		{
			line.Update(
				request.ChargeCategoryId,
				request.PurposeDescription,
				request.Amount,
				request.DueDate,
				request.Priority,
				request.AdditionalNotes);
			line.SetIncomeCategory(request.IncomeCategoryId!.Value, request.IncomeCategoryName!);
		}

		return existingLines.Concat(newLines).ToList();
	}

	public async Task<(int Processed, int RemainingPending)> ApproveLinesAsync(
		ChargeRequest request,
		IReadOnlyList<Guid>? studentIds,
		Guid approverId,
		string? approvalNotes)
	{
		await routingManager.RequireCategoryAsync(request.AcademicYearId, request.IncomeCategoryId, TransactionType.Income);
		var lines = await GetActionableLinesAsync(request.Id, studentIds, ChargeStatus.Pending);

		foreach (var line in lines)
		{
			line.Approve(approverId, approvalNotes);
		}

		var remaining = await CountPendingLinesAsync(request.Id);
		return (lines.Count, remaining);
	}

	public async Task<(int Processed, int RemainingPending)> RejectLinesAsync(
		ChargeRequest request,
		IReadOnlyList<Guid>? studentIds,
		Guid rejectorId,
		string rejectionReason)
	{
		var lines = await GetActionableLinesAsync(request.Id, studentIds, ChargeStatus.Pending);

		foreach (var line in lines)
		{
			line.Reject(rejectorId, rejectionReason);
		}

		var remaining = await CountPendingLinesAsync(request.Id);
		return (lines.Count, remaining);
	}

	private async Task<List<Charge>> CreateLinesAsync(
		ChargeRequest request,
		IReadOnlyList<Guid> studentIds,
		ChargeStatus initialStatus,
		Guid requestorId,
		ChargeCategory category)
	{
		var lines = new List<Charge>();
		var distinctStudentIds = studentIds.Distinct().ToList();
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var students = await AsyncExecuter.ToListAsync(
			studentQuery
				.Where(student => distinctStudentIds.Contains(student.Id))
				.Select(student => new
				{
					student.Id,
					student.AcademicBoardId,
					student.AcademicMediumId,
					student.StandardId,
					student.DivisionId
				}));
		var studentMap = students.ToDictionary(student => student.Id);

		foreach (var studentId in distinctStudentIds)
		{
			if (!studentMap.TryGetValue(studentId, out var student))
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentNotFound)
					.WithData("StudentId", studentId);
			}

			if (student.StandardId is null || student.DivisionId is null)
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentMissingAcademicScope)
					.WithData("StudentId", studentId);
			}

			var line = new Charge(
				guidGenerator.Create(),
				request.TenantId,
				request.AcademicYearId,
				request.Id,
				request.ChargeName,
				request.ChargeCategoryId,
				studentId,
				student.AcademicBoardId ?? Guid.Empty,
				student.AcademicMediumId,
				student.StandardId.Value,
				student.DivisionId.Value,
				request.PurposeDescription,
				request.Amount,
				request.DueDate,
				request.Priority,
				initialStatus,
				requestorId,
				request.AdditionalNotes);
			line.SetIncomeCategory(request.IncomeCategoryId!.Value, request.IncomeCategoryName!);

			await chargeRepository.InsertAsync(line, autoSave: false);
			lines.Add(line);
		}

		return lines;
	}

	public async Task SetIncomeCategoryFromChargeCategoryAsync(ChargeRequest request)
	{
		var category = await GetActiveCategoryAsync(request.ChargeCategoryId, request.AcademicYearId);
		await SetIncomeCategoryFromChargeCategoryAsync(request, category);
	}

	private async Task SetIncomeCategoryFromChargeCategoryAsync(ChargeRequest request, ChargeCategory category)
	{
		var income = await routingManager.RequireCategoryAsync(request.AcademicYearId,
			category.IncomeCategoryId, TransactionType.Income);
		request.SetIncomeCategory(income.Id, income.Name);
	}

	private async Task<ChargeCategory> GetActiveCategoryAsync(Guid chargeCategoryId, Guid academicYearId)
	{
		var category = await chargeCategoryRepository.FindAsync(chargeCategoryId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryNotFound)
				.WithData("ChargeCategoryId", chargeCategoryId);

		if (!category.IsActive)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryInactive)
				.WithData("ChargeCategoryId", chargeCategoryId);
		}

		if (category.AcademicYearId != academicYearId)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCategoryAcademicYearMismatch)
				.WithData("ChargeCategoryId", chargeCategoryId)
				.WithData("AcademicYearId", academicYearId);
		}

		return category;
	}

	private async Task<List<Charge>> GetActionableLinesAsync(
		Guid chargeRequestId,
		IReadOnlyList<Guid>? studentIds,
		ChargeStatus requiredStatus)
	{
		var lines = await chargeRepository.GetListAsync(
			c => c.ChargeRequestId == chargeRequestId && c.Status == requiredStatus);

		if (lines.Count == 0)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeNotPending);

		if (studentIds is { Count: > 0 })
		{
			var idSet = studentIds.ToHashSet();
			var matched = lines.Where(l => idSet.Contains(l.StudentId)).ToList();
			var unknown = studentIds.Where(id => lines.All(l => l.StudentId != id)).ToList();
			if (unknown.Count > 0)
			{
				throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeLineSubset)
					.WithData("StudentIds", string.Join(",", unknown));
			}

			return matched;
		}

		return lines;
	}

	private async Task<int> CountPendingLinesAsync(Guid chargeRequestId)
	{
		return await chargeRepository.CountAsync(
			c => c.ChargeRequestId == chargeRequestId && c.Status == ChargeStatus.Pending);
	}
}
