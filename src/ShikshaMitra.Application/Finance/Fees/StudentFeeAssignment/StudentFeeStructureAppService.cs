using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Transport;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public partial class StudentFeeStructureAppService(
	IStudentRepository studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.PaymentTerm, Guid> paymentTermRepository,
	IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.PaymentTermVersion, Guid> paymentTermVersionRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid> divisionRepository,
	IRepository<TransportMember, Guid> transportMemberRepository,
	IRepository<TransportMemberRouteAssignment, Guid> transportRouteRepository,
	IUserScopeService userScopeService,
	DueFeeManager dueFeeManager,
	IDataProtectionProvider protectionProvider,
	IUnitOfWorkManager unitOfWorkManager
) : ApplicationService, IStudentFeeStructureAppService
{
	private const string AssignPermission = ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.Add;
	private const string ViewPermission = ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.View;
	private IDataProtector Protector => protectionProvider.CreateProtector("Finance.StudentAssignment.v2");

	[Authorize(AssignPermission)]
	public async Task<StudentFeeAssignmentPreviewDto> PreviewAsync(AssignStudentFeeStructureDto input)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, false);
		if (await studentFeeStructureRepository.AnyAsync(a => a.StudentId == input.StudentId && a.AcademicYearId == input.AcademicYearId))
			throw new BusinessException(AssignmentErrorCodes.AlreadyAssigned);
		var prepared = await PrepareAsync(input);
		var expires = Clock.Now.AddMinutes(20);
		prepared.Preview.PreviewExpiresAt = new DateTimeOffset(expires).ToUniversalTime();
		prepared.Preview.PreviewToken = Protector.Protect(JsonSerializer.Serialize(new PreviewAssertion(
			CurrentTenant.Id, CurrentUser.Id, CommandFingerprint(input), prepared.Fingerprint, expires)));
		return prepared.Preview;
	}

	[Authorize(AssignPermission)]
	public async Task<AssignmentCommitResultDto> AssignAsync(AssignStudentFeeStructureDto input)
		=> await AssignAcceptedAsync(input, Clock.Now);

	private async Task<AssignmentCommitResultDto> AssignAcceptedAsync(AssignStudentFeeStructureDto input, DateTime acceptedAt)
	{
		ValidateInput(input);
		try { return await CommitAsync(input, acceptedAt); }
		catch (Exception exception) when (exception is not OperationCanceledException and not AbpAuthorizationException and not UnauthorizedAccessException)
		{
			// CommitAsync has disposed its write context. Completion/event failures can
			// occur after persistence, so only a fresh scoped read can establish success.
			using var retry = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
			await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, false);
			var existing = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == input.StudentId && x.AcademicYearId == input.AcademicYearId);
			var fingerprint = CommandFingerprint(input);
			if (existing?.AssignmentRequestFingerprint == fingerprint && existing.AssignmentSnapshotJson != null)
			{
				Logger.LogWarning(exception, "Returning the durable assignment after an interrupted commit");
				return await ExistingResultAsync(existing, fingerprint);
			}
			if (exception.GetType().Name is "DbUpdateException" or "AbpDbConcurrencyException")
			{
				if (existing != null) return await ExistingResultAsync(existing, fingerprint);
				Logger.LogError(exception, "Assignment commit failed");
				throw new BusinessException(AssignmentErrorCodes.CommitFailed);
			}
			throw;
		}
	}

	private async Task<AssignmentCommitResultDto> CommitAsync(AssignStudentFeeStructureDto input, DateTime acceptedAt)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, false);
		var commandFingerprint = CommandFingerprint(input);
		var existing = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == input.StudentId && x.AcademicYearId == input.AcademicYearId);
		if (existing != null) return await ExistingResultAsync(existing, commandFingerprint);
		PreviewAssertion assertion;
		try { assertion = JsonSerializer.Deserialize<PreviewAssertion>(Protector.Unprotect(input.PreviewToken ?? ""))!; }
		catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
		{ throw new BusinessException(AssignmentErrorCodes.StalePreview); }
		if (assertion == null || assertion.TenantId != CurrentTenant.Id || assertion.UserId != CurrentUser.Id ||
			assertion.Command != commandFingerprint || assertion.Expires <= acceptedAt)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);
		// Expiry is checked at confirmation, before recalculation. Bulk queue time cannot
		// expire an accepted preview, but changed authoritative facts still fail closed.
		var prepared = await PrepareAsync(input);
		if (assertion.Prepared != prepared.Fingerprint)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);
		var id = GuidGenerator.Create();
		var structure = prepared.Structure;
		var policies = prepared.Preview.Policies.ToDictionary(x => x.Family);
		var concessionPolicy = policies.GetValueOrDefault("concession");
		var concessionVersion = concessionPolicy == null ? null : await financePolicyVersionRepository.GetAsync(concessionPolicy.VersionId);
		var concessionSnapshot = concessionVersion == null ? null : JsonSerializer.Deserialize<ConcessionPolicySnapshot>(concessionVersion.SnapshotJson);
		var assignment = new StudentFeeStructure(id, CurrentTenant.Id, input.StudentId, structure.Id, input.AcademicYearId,
			concessionPolicy?.PolicyId, concessionPolicy?.VersionId, concessionSnapshot?.FeeHeadScope ?? ConcessionFeeHeadScope.EntireFeeStructure, concessionSnapshot?.ApplicableFeeHeadIds ?? [],
			policies["collection"].PolicyId, policies["collection"].VersionId, policies.GetValueOrDefault("refund")?.PolicyId, policies.GetValueOrDefault("refund")?.VersionId,
			policies.GetValueOrDefault("proration")?.PolicyId, policies.GetValueOrDefault("proration")?.VersionId, prepared.Preview.ChargeStartDate, prepared.Preview.ChargeEndDate,
			financeFeeAccountId: prepared.Account?.Id);
		if (prepared.Account != null)
			await LazyServiceProvider.LazyGetRequiredService<AdmissionFeeCollectionAppService>()
				.LinkForAssignmentAsync(prepared.AdmissionSource!, prepared.Account);
		for (var i = 0; i < prepared.Dues.Count; i++)
		{
			prepared.Dues[i].BindAssignment(id);
			prepared.Preview.Lines[i].DueFeeId = prepared.Dues[i].Id;
		}
		assignment.CaptureAssignment(commandFingerprint, JsonSerializer.Serialize(prepared.Preview));
		if (prepared.Account != null && !prepared.ReuseDues)
			prepared.Account.CaptureAssignment(structure.Id, prepared.Preview.RevisionNumber, JsonSerializer.Serialize(prepared.Preview), commandFingerprint);
		if (prepared.NewAccount)
			// Flush the account before adding the reciprocal assignment link. Both
			// writes remain inside this transaction, without a circular INSERT graph.
			await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(prepared.Account!, true);
		await studentFeeStructureRepository.InsertAsync(assignment, autoSave: false);
		if (!prepared.ReuseDues) await dueFeeRepository.InsertManyAsync(prepared.Dues, autoSave: false);
		else await dueFeeRepository.UpdateManyAsync(prepared.Dues, autoSave: false);
		if (prepared.Account != null)
		{
			prepared.Account.BindStudentAssignment(id);
			await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().UpdateAsync(prepared.Account, false);
		}
		var student = await studentRepository.GetForFeeAssignmentAsync(input.StudentId)
			?? throw new BusinessException(AssignmentErrorCodes.EnrollmentRequired);
		if (student.AcademicYearId == input.AcademicYearId) student.AssignFeeStructure(structure.Id);
		await uow.CompleteAsync();
		return new AssignmentCommitResultDto { AssignmentId = id, Assignment = prepared.Preview };
	}

	private async Task<AssignmentCommitResultDto> ExistingResultAsync(StudentFeeStructure existing, string fingerprint)
	{
		if (existing.AssignmentRequestFingerprint != fingerprint || existing.AssignmentSnapshotJson == null)
			throw new BusinessException(AssignmentErrorCodes.AlreadyAssigned);
		var snapshot = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(existing.AssignmentSnapshotJson)!;
		var dues = await dueFeeRepository.GetListAsync(d => d.StudentFeeStructureId == existing.Id && d.StudentId == existing.StudentId);
		foreach (var line in snapshot.Lines)
		{
			var due = dues.FirstOrDefault(d => d.Id == line.DueFeeId);
			if (due == null) throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			line.PaidAmount = due.PaidAmount;
			line.OutstandingAmount = due.OutstandingAmount;
		}
		return new AssignmentCommitResultDto { AssignmentId = existing.Id, Outcome = "AlreadyAssigned",
			Assignment = snapshot };
	}

	private static void ValidateInput(AssignStudentFeeStructureDto input, bool admissionContext = false)
	{
		if ((!admissionContext && input.StudentId == Guid.Empty) || input.AcademicYearId == Guid.Empty || input.StandardFeeStructureId == Guid.Empty ||
			input.SelectedOptionalFeeHeadIds == null || input.FeeHeadDetails == null || input.ServicePeriods == null || input.VariableFeeHeads == null ||
			input.FeeHeadDetails.Any(x => x == null) || input.ServicePeriods.Any(x => x == null) || input.VariableFeeHeads.Any(x => x == null) ||
			input.VariableFeeHeads.Count > 200 || input.SelectedOptionalFeeHeadIds.Count > 200 || input.FeeHeadDetails.Count > 200 || input.ServicePeriods.Count > 200 ||
			input.SelectedOptionalFeeHeadIds.Distinct().Count() != input.SelectedOptionalFeeHeadIds.Count ||
			input.FeeHeadDetails.Select(x => x.FeeHeadId).Distinct().Count() != input.FeeHeadDetails.Count ||
			input.ServicePeriods.Select(x => x.FeeHeadId).Distinct().Count() != input.ServicePeriods.Count ||
            input.VariableFeeHeads.Select(x => x.FeeHeadId).Distinct().Count() != input.VariableFeeHeads.Count ||
            input.VariableFeeHeads.Any(x => x.FeeHeadId == Guid.Empty || !x.Amount.HasValue || x.Amount < 0 || x.Amount > 9999999999.99m ||
                decimal.Round(x.Amount.Value, 2) != x.Amount || x.ZeroAmountReason?.Length > 500 ||
                (x.Amount == 0 && string.IsNullOrWhiteSpace(x.ZeroAmountReason))) ||
			input.FeeHeadDetails.Any(x => x.Amount < 0 || decimal.Round(x.Amount, 2) != x.Amount) || input.OverrideReason?.Length > 500)
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		ValidatePolicies(input.Policies);
		if (input.PolicyOverrideReason?.Length > 500) throw new BusinessException(AssignmentErrorCodes.InvalidInput);
	}

	private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
	private static string CommandFingerprint(AssignStudentFeeStructureDto input) => input.VariableFeeHeads.Count == 0 ? LegacyCommandFingerprint(input) :
        Hash(new { Legacy = LegacyCommandFingerprint(input), VariableHeads = input.VariableFeeHeads.OrderBy(x => x.FeeHeadId).ToArray() });
    private static string LegacyCommandFingerprint(AssignStudentFeeStructureDto input) => Hash(new {
		input.StudentId, input.AcademicYearId, input.StandardFeeStructureId,
		Heads = input.SelectedOptionalFeeHeadIds.Order().ToArray(), Details = input.FeeHeadDetails.OrderBy(x => x.FeeHeadId).ToArray(),
		Periods = input.ServicePeriods.OrderBy(x => x.FeeHeadId).ToArray(), input.ChargeStartDate, input.ChargeEndDate,
		input.ChargePeriodSource, input.ChargePeriodSourceReferenceId, input.ChargePeriodOverrideReason, input.OverrideReason,
		Policies = input.Policies.OrderBy(x => x.Family).ToArray(), input.PolicyOverrideReason });
	private sealed record PreviewAssertion(Guid? TenantId, Guid? UserId, string Command, string Prepared, DateTime Expires);
	private sealed record PreparedAssignment(FeeStructure Structure, StudentFeeAssignmentPreviewDto Preview, List<DueFee> Dues, string Fingerprint,
		FinanceFeeAccount? Account = null, AdmissionFinanceContext? AdmissionSource = null, bool ReuseDues = false, bool NewAccount = false);
}
