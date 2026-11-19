using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private const string StopOptionalServicePermission = ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.StopOptionalService;
	private const string OptionalServiceStopped = "optional_fee_service_stopped";
	private IDataProtector OptionalServiceProtector => protectionProvider.CreateProtector("Finance.OptionalFeeServiceStop.v1");
	private static readonly JsonSerializerOptions OptionalServiceAuditJson = new(JsonSerializerDefaults.Web);

	[Authorize(ViewPermission)]
	[Authorize(StopOptionalServicePermission)]
	public async Task<OptionalServiceStopResult> PreviewOptionalServiceStopAsync(Guid studentId, OptionalServiceStopInput input)
	{
		var command = await ValidateOptionalServiceStopAsync(studentId, input);
		var (assignment, lines) = await PlanOptionalServiceStopAsync(studentId, input);
		var fingerprint = OptionalServiceStopFingerprint(assignment, lines);
		var assertion = new PreviewAssertion(CurrentTenant.Id, CurrentUser.Id, command, fingerprint, Clock.Now.AddMinutes(20));
		return new(lines.Count, lines.Sum(line => line.OutstandingAmount), OptionalServiceProtector.Protect(JsonSerializer.Serialize(assertion)));
	}

	[Authorize(ViewPermission)]
	[Authorize(StopOptionalServicePermission)]
	public async Task<OptionalServiceStopResult> StopOptionalServiceAsync(Guid studentId, OptionalServiceStopInput input)
	{
		var acceptedAt = Clock.Now;
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
			{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var command = await ValidateOptionalServiceStopAsync(studentId, input);
		var history = LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>();
		var replay = await history.FindAsync(x => x.Id == input.RequestId && x.TenantId == CurrentTenant.Id);
		if (replay != null)
		{
			var saved = replay.Action == OptionalServiceStopped && replay.StudentId == studentId &&
				replay.AcademicYearId == input.AcademicYearId && replay.ContextJson != null
				? JsonSerializer.Deserialize<OptionalServiceStopEvidence>(replay.ContextJson, OptionalServiceAuditJson)
				: null;
			if (saved?.Command != command) throw new BusinessException(AssignmentErrorCodes.StalePreview);
			return saved.Result;
		}

		PreviewAssertion? assertion;
		try { assertion = JsonSerializer.Deserialize<PreviewAssertion>(OptionalServiceProtector.Unprotect(input.PreviewToken ?? "")); }
		catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
		{ throw new BusinessException(AssignmentErrorCodes.StalePreview); }
		if (assertion == null || assertion.TenantId != CurrentTenant.Id || assertion.UserId != CurrentUser.Id ||
			assertion.Command != command || assertion.Expires <= acceptedAt)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);

		var (assignment, lines) = await PlanOptionalServiceStopAsync(studentId, input);
		if (OptionalServiceStopFingerprint(assignment, lines) != assertion.Prepared)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);

		var result = new OptionalServiceStopResult(lines.Count, lines.Sum(line => line.OutstandingAmount));
		var before = lines.Select(line => new { line.Id, line.CoverageStartDate, line.CoverageEndDate, line.OutstandingAmount }).ToArray();
		foreach (var line in lines)
		{
			line.CancelOptionalServiceInstallment(assignment.Id, input.RequestId, input.LastServiceDate, DateOnly.FromDateTime(acceptedAt));
			await dueFeeRepository.UpdateAsync(line, autoSave: false);
		}

		var audit = LazyServiceProvider.LazyGetRequiredService<FinanceAuditWriter>();
		await audit.WriteAsync(input.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, assignment.Id,
			"Optional fee service cancellation", OptionalServiceStopped, "Cancelled future unused installments for an optional fee.",
			new() { StudentId = studentId }, input.Reason.Trim(),
			context: new OptionalServiceStopEvidence(command, result, input.FeeHeadId, input.LastServiceDate, before),
			eventId: input.RequestId);
		await uow.CompleteAsync();
		return result;
	}

	private async Task<string> ValidateOptionalServiceStopAsync(Guid studentId, OptionalServiceStopInput input)
	{
		if (studentId == Guid.Empty || input.AcademicYearId == Guid.Empty || input.FeeHeadId == Guid.Empty || input.RequestId == Guid.Empty ||
			string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		await RequireEnrollmentAsync(studentId, input.AcademicYearId, true);
		return Hash(new { CurrentUser.Id, StudentId = studentId, input.AcademicYearId, input.FeeHeadId,
			input.LastServiceDate, Reason = input.Reason.Trim(), input.RequestId });
	}

	private async Task<(StudentFeeStructure Assignment, List<DueFee> Lines)> PlanOptionalServiceStopAsync(Guid studentId, OptionalServiceStopInput input)
	{
		var assignment = await studentFeeStructureRepository.FindAsync(x => x.StudentId == studentId && x.AcademicYearId == input.AcademicYearId)
			?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
		var snapshot = SavedAssignment(assignment);
		var head = snapshot?.Heads.SingleOrDefault(x => x.FeeHeadId == input.FeeHeadId && x.Included && x.IsOptional && !x.IsOneTime);
		var today = DateOnly.FromDateTime(Clock.Now);
		if (snapshot == null || snapshot.StudentId != studentId || snapshot.AcademicYearId != input.AcademicYearId ||
			head?.ServicePeriod?.StartDate is not DateTime start || head.ServicePeriod.EndDate is not DateTime end ||
			input.LastServiceDate < DateOnly.FromDateTime(start) || input.LastServiceDate >= DateOnly.FromDateTime(end))
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);

		var rows = await dueFeeRepository.GetListAsync(due => due.StudentFeeStructureId == assignment.Id &&
			due.StudentId == studentId && due.FeeHeadId == input.FeeHeadId);
		var evidence = snapshot.Lines.Where(line => line.FeeHeadId == input.FeeHeadId).ToList();
		if (rows.Count != evidence.Count || rows.Any(due => evidence.Count(line => line.DueFeeId == due.Id &&
			line.ProratedAmount == due.OriginalAmount && line.ScheduledAmount == due.ScheduledAmount) != 1))
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);

		var eligible = rows.Where(due => due.CanCancelOptionalServiceInstallment(assignment.Id, input.LastServiceDate, today)).ToList();
		if (eligible.Count == 0) throw new BusinessException(AssignmentErrorCodes.NoFutureOptionalInstallments);

		// Historical or pending references can exist even when the due has no applied balance change.
		var protectedIds = await OptionalServiceProtectedDueIdsAsync(studentId, input.AcademicYearId, eligible.Select(due => due.Id).ToArray());
		eligible.RemoveAll(due => protectedIds.Contains(due.Id));
		if (eligible.Count == 0) throw new BusinessException(AssignmentErrorCodes.NoFutureOptionalInstallments);
		return (assignment, eligible);
	}

	private async Task<HashSet<Guid>> OptionalServiceProtectedDueIdsAsync(Guid studentId, Guid yearId, Guid[] dueIds)
	{
		using var history = LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		var ids = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId).ToHashSet();
		ids.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId));
		ids.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeAdjustmentSourceLine, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId));
		ids.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeAdjustmentTargetAllocation, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId));
		ids.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<ConcessionDueFeeAllocation, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId));
		ids.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<LateFeeWaiverApplication, Guid>>()
			.GetListAsync(x => Enumerable.Contains(dueIds, x.DueFeeId))).Select(x => x.DueFeeId));
		var refunds = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetQueryableAsync();
		var refundLines = await AsyncExecuter.ToListAsync(refunds.SelectMany(request => request.Lines)
			.Where(line => line.DueFeeId.HasValue && Enumerable.Contains(dueIds, line.DueFeeId.Value) ||
				line.LateFeeDueFeeId.HasValue && Enumerable.Contains(dueIds, line.LateFeeDueFeeId.Value))
			.Select(line => new { line.DueFeeId, line.LateFeeDueFeeId }));
		ids.UnionWith(refundLines.SelectMany(line => new[] { line.DueFeeId, line.LateFeeDueFeeId }).OfType<Guid>());

		var pendingConcessions = await LazyServiceProvider.LazyGetRequiredService<IRepository<ConcessionRequest, Guid>>()
			.GetListAsync(request => request.StudentId == studentId && request.AcademicYearId == yearId &&
				(request.Status == ConcessionRequestStatus.Pending || request.Status == ConcessionRequestStatus.Draft));
		ids.UnionWith(pendingConcessions.SelectMany(request => request.GetSelectedDueFeeIds()));
		return ids;
	}

	private static string OptionalServiceStopFingerprint(StudentFeeStructure assignment, List<DueFee> lines) => Hash(new
	{
		assignment.Id, assignment.ConcurrencyStamp,
		Lines = lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.ConcurrencyStamp, line.OutstandingAmount })
	});

	private sealed record OptionalServiceStopEvidence(string Command, OptionalServiceStopResult Result, Guid FeeHeadId,
		DateOnly LastServiceDate, object Lines);
}
