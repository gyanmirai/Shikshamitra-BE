using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeAppService
{
	private async Task<ChargeAudienceDto> ResolveAudienceDtoAsync(CreateChargeRequest input)
	{
		if (input.Audience is not null)
			return input.Audience;

		if (!input.StudentId.HasValue)
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience);

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
		var workflowStatus = await GetWorkflowStatusAsync(lines);

		return new ChargeResponse
		{
			Success = true,
			Data = new ChargeData
			{
				ChargeId = request.Id, ChargeName = request.ChargeName,
				ResolvedStudentCount = studentCount,
			ChargeCategoryId = request.ChargeCategoryId, CategoryName = category.Name,
				PurposeDescription = request.PurposeDescription, Amount = request.Amount,
				DueDate = request.DueDate, AppliedOn = request.AppliedOn,
				Priority = (int)request.Priority, Status = (int)status, WorkflowStatus = workflowStatus,
				BoardId = request.BoardId, EducationMediumId = request.EducationMediumId,
				AdditionalNotes = request.AdditionalNotes,
				RequestorId = request.RequestorId,
				RequestorName = requestor.Name ?? requestor.UserName ?? string.Empty,
				RequestedOn = request.AppliedOn,
				AcademicYearId = request.AcademicYearId, AcademicYearName = academicYear.Name,
				ConcurrencyStamp = request.ConcurrencyStamp,
				AvailableActions = await GetAvailableActionsAsync(
					request.RequestorId,
					lines.Any(l => l.Status == ChargeStatus.Pending),
					lines.Count == 0 || lines.All(l => l.Status == ChargeStatus.Pending),
					workflowStatus == ChargeWorkflowStatus.InProcess)
			},
			Message = message
		};
	}

	private static ChargeListItem MapToListItem(ChargeRequestJoinResult r) =>
		new()
		{
			ChargeId = r.Request.Id, ChargeName = r.Request.ChargeName,
			BoardId = r.Request.BoardId, EducationMediumId = r.Request.EducationMediumId,
			ChargeCategoryId = r.Category.Id, CategoryName = r.Category.Name,
			Amount = r.Request.Amount, PurposeDescription = r.Request.PurposeDescription,
			DueDate = r.Request.DueDate, AppliedOn = r.Request.AppliedOn,
			Priority = (int)r.Request.Priority,
			DisplayStatus = (int)r.WorkflowStatus, WorkflowStatus = r.WorkflowStatus,
			StudentCount = r.StudentCount, ProcessedCount = r.ProcessedCount,
			RequestorId = r.Requestor.Id,
			RequestorName = r.Requestor.Name ?? r.Requestor.UserName ?? string.Empty,
			RequestedOn = r.Request.AppliedOn, AdditionalNotes = r.Request.AdditionalNotes
			,ConcurrencyStamp = r.Request.ConcurrencyStamp
		};

	private static ChargeLineListItem MapToLineListItem(ChargeLineJoinResult line, ChargeCategory? category)
	{
		var student = line.Student;
		return new ChargeLineListItem
		{
			ChargeId = line.Charge.ChargeRequestId, ChargeLineId = line.Charge.Id,
			ChargeName = line.Charge.ChargeName, StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo?.ToString() ?? string.Empty,
			StandardName = line.Standard.Name, DivisionName = line.Division.Name,
			ChargeCategoryId = line.Charge.ChargeCategoryId, CategoryName = category?.Name ?? string.Empty,
			Amount = line.Charge.Amount, PurposeDescription = line.Charge.PurposeDescription,
			DueDate = line.Charge.DueDate, Status = (int)line.Charge.Status,
			CollectedAmount = 0m, OutstandingAmount = line.Charge.Status == ChargeStatus.Approved ? line.Charge.Amount : 0m
		};
	}

	private static ChargeStudentLineDto MapToStudentLine(ChargeLineJoinResult line)
	{
		var student = line.Student;
		return new ChargeStudentLineDto
		{
			ChargeLineId = line.Charge.Id,
			StudentId = student.Id,
			StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim(),
			GrNumber = student.GrNo?.ToString() ?? string.Empty,
			RollNumber = student.RollNo,
			StandardName = line.Standard.Name, DivisionName = line.Division.Name,
			Status = (int)line.Charge.Status,
			CollectedAmount = 0m,
			OutstandingAmount = line.Charge.Status == ChargeStatus.Approved ? line.Charge.Amount : 0m
		};
	}

	private static ChargeStatus DeriveRequestStatus(IReadOnlyList<ChargeStatus> lineStatuses)
	{
		if (lineStatuses.Count == 0 || lineStatuses.Any(s => s == ChargeStatus.Pending))
			return ChargeStatus.Pending;
		if (lineStatuses.All(s => s == ChargeStatus.Rejected)) return ChargeStatus.Rejected;
		if (lineStatuses.All(s => s == ChargeStatus.Voided)) return ChargeStatus.Voided;
		return ChargeStatus.Approved;
	}

	private async Task<ChargeRequest> GetChargeRequestOrThrowAsync(Guid chargeRequestId) =>
		await chargeRequestRepository.FindAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);

	private async Task<ChargeRequestDetailResult> GetChargeRequestDetailsOrThrowAsync(Guid chargeRequestId) =>
		await chargeRequestRepository.GetWithLinesAsync(chargeRequestId)
			?? throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFound)
				.WithData("ChargeId", chargeRequestId);

	private async Task EnsureAcademicYearExistsAsync(Guid academicYearId)
	{
		if (await academicYearRepository.FindAsync(academicYearId) is null)
			throw new BusinessException(FinanceChargeErrorCodes.AcademicYearNotFound)
				.WithData("AcademicYearId", academicYearId);
	}

	private async Task EnsureAllLinesPendingOrThrowAsync(Guid chargeRequestId, bool forDelete)
	{
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeRequestId);
		if (lines.Count == 0) return;
		if (lines.Any(l => l.Status != ChargeStatus.Pending))
			throw new BusinessException(FinanceChargeErrorCodes.ChargeRequestNotFullyPending);
	}

	private static void EnsureConcurrencyStamp(ChargeRequest request, string? concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp))
			throw new BusinessException(FinanceChargeErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(request.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(FinanceChargeErrorCodes.ConcurrencyConflict);
	}

	private async Task<List<Guid>> GetScopedStudentIdsAsync(Guid academicYearId)
	{
		var query = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var studentQuery = await studentRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(studentQuery
			.Where(student => student.AcademicYearId == academicYearId && query.Contains(student.Id))
			.Select(student => student.Id));
	}

	private async Task EnsureLinesAccessibleAsync(IEnumerable<Guid> studentIds)
	{
		var ids = studentIds.Distinct().ToList();
		var scopedQuery = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
		var accessibleCount = await AsyncExecuter.CountAsync(scopedQuery.Where(id => ids.Contains(id)));
		if (accessibleCount != ids.Count)
			throw new Volo.Abp.Authorization.AbpAuthorizationException(
				"Access denied: one or more charge lines are outside your user scope.");
	}

	private async Task EnsureRequestAccessibleAsync(Guid chargeRequestId)
	{
		var lines = await chargeRepository.GetListAsync(c => c.ChargeRequestId == chargeRequestId);
		await EnsureLinesAccessibleAsync(lines.Select(line => line.StudentId));
	}

	private async Task<List<string>> GetAvailableActionsAsync(
		Guid requestorId, bool hasPending, bool allPending, bool hasApproved)
	{
		var actions = new List<string> { "View" };
		if (allPending && await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update))
			actions.Add("Edit");
		if (allPending && await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Delete))
			actions.Add("Delete");
		if (hasPending && CurrentUser.Id != requestorId && await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve))
			actions.Add("Approve");
		if (hasPending && CurrentUser.Id != requestorId && await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject))
			actions.Add("Reject");
		if (hasApproved && await AuthorizationService.IsGrantedAsync(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Void))
			actions.Add("Void");
		return actions;
	}

	private async Task<ChargeWorkflowStatus> GetWorkflowStatusAsync(IReadOnlyList<Charge> lines)
	{
		var collected = await GetCollectedAmountsAsync(lines.Select(line => line.Id));
		var waived = await GetWaivedAmountsAsync(lines.Select(line => line.Id));
		return ChargeWorkflowStatusResolver.Resolve(lines.Select(line => new ChargeWorkflowLine(
			line.Status, Math.Max(0m, line.Amount - collected.GetValueOrDefault(line.Id) - waived.GetValueOrDefault(line.Id)))));
	}

	private async Task<Dictionary<Guid, decimal>> GetWaivedAmountsAsync(IEnumerable<Guid> chargeLineIds)
	{
		var ids = chargeLineIds.Distinct().ToList();
		if (ids.Count == 0) return [];
		var waivers = await chargeWaiverRepository.GetListAsync(x => ids.Contains(x.ChargeId));
		return waivers.GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.EffectiveAmount));
	}

	private async Task<Dictionary<Guid, decimal>> GetCollectedAmountsAsync(IEnumerable<Guid> chargeLineIds)
	{
		var ids = chargeLineIds.Distinct().ToList();
		if (ids.Count == 0) return [];
		var allocations = await chargeCollectionRecordRepository.GetListAsync(x => ids.Contains(x.ChargeId));
		var transactionIds = allocations.Select(x => x.FeeRecordMetadataId).Distinct().ToList();
		var activeIds = (await feeRecordMetadataRepository.GetListAsync(x =>
			transactionIds.Contains(x.Id) && x.Status != ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Reversed))
			.Select(x => x.Id).ToHashSet();
		return allocations
			.Where(x => activeIds.Contains(x.FeeRecordMetadataId))
			.GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(row => Convert.ToDecimal(row.Amount)));
	}
}
