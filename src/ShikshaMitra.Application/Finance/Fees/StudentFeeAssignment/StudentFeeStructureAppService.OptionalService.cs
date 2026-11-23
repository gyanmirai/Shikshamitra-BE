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
		return DescribeOptionalServiceStop(lines, OptionalServiceProtector.Protect(JsonSerializer.Serialize(assertion)));
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

		var result = DescribeOptionalServiceStop(lines);
		var before = lines.Select(line => new { line.Id, line.CoverageStartDate, line.CoverageEndDate,
			line.NetPayableAmount, line.OutstandingAmount, line.PaidAmount }).ToArray();
		foreach (var line in lines)
		{
			line.CancelOptionalServiceInstallment(assignment.Id, input.RequestId, input.LastServiceDate);
			await dueFeeRepository.UpdateAsync(line, autoSave: false);
		}
		assignment.RecordOptionalServiceEndDate(input.FeeHeadId, input.LastServiceDate.AddDays(1));
		await studentFeeStructureRepository.UpdateAsync(assignment, autoSave: false);

		var audit = LazyServiceProvider.LazyGetRequiredService<FinanceAuditWriter>();
		await audit.WriteAsync(input.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, assignment.Id,
			"Optional fee service cancellation", OptionalServiceStopped, "Applied the optional service end date to eligible installments.",
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
		var period = assignment.GetOptionalServicePeriods(input.FeeHeadId).LastOrDefault();
		var head = snapshot?.Heads.SingleOrDefault(x => x.FeeHeadId == input.FeeHeadId && (x.Included || period != null) && x.IsOptional && !x.IsOneTime);
		var start = period?.StartDate ?? (head?.ServicePeriod?.StartDate is DateTime savedStart ? DateOnly.FromDateTime(savedStart) : null);
		var end = period?.EndDate ?? (head?.ServicePeriod?.EndDate is DateTime savedEnd ? DateOnly.FromDateTime(savedEnd) : null);
		if (snapshot == null || snapshot.StudentId != studentId || snapshot.AcademicYearId != input.AcademicYearId ||
			head == null || !start.HasValue || !end.HasValue || input.LastServiceDate.DayNumber < start.Value.DayNumber - 1 ||
			input.LastServiceDate >= end.Value)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);

		var recordedEnd = assignment.GetOptionalServiceEndDate(input.FeeHeadId);
		if (recordedEnd.HasValue && recordedEnd != input.LastServiceDate.AddDays(1))
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceAlreadyStopped);

		var rows = await dueFeeRepository.GetListAsync(due => due.StudentFeeStructureId == assignment.Id &&
			due.StudentId == studentId && due.FeeHeadId == input.FeeHeadId);
		rows.RemoveAll(due => due.GetOptionalServicePeriodId() != period?.Id);
		if (!recordedEnd.HasValue && rows.Any(due => due.GetOptionalServiceCancellationId().HasValue))
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceAlreadyStopped);

		var evidence = snapshot.Lines.Where(line => line.FeeHeadId == input.FeeHeadId).ToList();
		if (period == null && (rows.Count != evidence.Count || rows.Any(due => evidence.Count(line => line.DueFeeId == due.Id &&
			line.ProratedAmount == due.OriginalAmount && line.ScheduledAmount == due.ScheduledAmount) != 1)) ||
			period != null && !AssignmentDueEvidence.MatchesPeriod(period, rows))
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);

		var eligible = rows.Where(due => due.CanCancelOptionalServiceInstallment(assignment.Id, input.LastServiceDate)).ToList();
		if (eligible.Count > 0)
		{
			var protectedIds = await OptionalServiceProtectedDueIdsAsync(studentId, input.AcademicYearId, eligible.Select(due => due.Id).ToArray());
			eligible.RemoveAll(due => protectedIds.Contains(due.Id));
		}
		if (recordedEnd.HasValue && eligible.Count == 0)
			throw new BusinessException(AssignmentErrorCodes.OptionalServiceAlreadyStopped);
		return (assignment, eligible);
	}

	private async Task<HashSet<Guid>> OptionalServiceProtectedDueIdsAsync(Guid studentId, Guid yearId, Guid[] dueIds)
	{
		using var history = LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		// Cleared principal payments remain on their receipts. Only unsettled instruments block this credit.
		var pendingReceipts = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetQueryableAsync())
			.Where(receipt => receipt.AcademicYearId == yearId &&
				receipt.Status == ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.InReconciliation)
			.Select(receipt => receipt.Id);
		var allocations = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().GetQueryableAsync();
		var ids = (await AsyncExecuter.ToListAsync(allocations
			.Where(line => Enumerable.Contains(dueIds, line.DueFeeId) && pendingReceipts.Contains(line.FeeRecordMetadataId))
			.Select(line => line.DueFeeId))).ToHashSet();
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
		Lines = lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.ConcurrencyStamp,
			line.NetPayableAmount, line.OutstandingAmount, line.PaidAmount, line.RefundedAmount })
	});

	private static OptionalServiceStopResult DescribeOptionalServiceStop(List<DueFee> lines, string? token = null)
	{
		var cancelled = lines.Sum(line => line.NetPayableAmount);
		var outstanding = lines.Sum(line => line.OutstandingAmount);
		return new(lines.Count, cancelled, token, outstanding, cancelled - outstanding);
	}

	private sealed record OptionalServiceStopEvidence(string Command, OptionalServiceStopResult Result, Guid FeeHeadId,
		DateOnly LastServiceDate, object Lines);
}
