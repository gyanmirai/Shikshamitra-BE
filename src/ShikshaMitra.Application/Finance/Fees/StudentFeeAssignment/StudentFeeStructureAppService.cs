using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

[Authorize]
[RemoteService(false)]
public partial class StudentFeeStructureAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<ConcessionPolicy, Guid> concessionPolicyRepository,
	IRepository<RefundPolicy, Guid> refundPolicyRepository,
	IRepository<ProrationPolicy, Guid> prorationPolicyRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	DueFeeManager dueFeeManager,
	FinanceScopeAccessService financeScopeAccessService,
	IRepository<CollectionPolicy, Guid> collectionPolicyRepository
) : ApplicationService, IStudentFeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.Add)]
	public async Task<StudentFeeAssignmentPreviewDto> PreviewAsync(AssignStudentFeeStructureDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId);
		if (!feeStructure.IsLocked)
			throw new BusinessException("Finance:FeeStructureMustBeLocked");
		if (student.StandardId != feeStructure.StandardId)
			throw new BusinessException("Finance:FeeStructureStandardMismatch");

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
		await ValidateConcessionPolicySnapshotAsync(feeStructure, academicYear.Id);
		await ValidateCollectionPolicySnapshotAsync(feeStructure, academicYear.Id);
		await ValidateRefundPolicySnapshotAsync(feeStructure, academicYear.Id);
		var prorationSnapshot = await ValidateProrationPolicySnapshotAsync(feeStructure, academicYear.Id);
		if (prorationSnapshot != null)
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Simulate);
		var chargePeriod = await ResolveChargePeriodAsync(
			input.ChargeStartDate, input.ChargeEndDate, input.ChargePeriodSource,
			input.ChargePeriodSourceReferenceId, input.ChargePeriodOverrideReason, academicYear);

		var details = await feeStructureDetailRepository.GetListAsync(x => x.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(x => feeStructure.FeeStructureDetails.All(y => y.Id != x.Id)))
			feeStructure.AddDetail(detail);
		var lines = await dueFeeManager.GenerateForStudentAsync(
			student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds,
			BuildOptionalDetails(input.FeeHeadDetails), prorationSnapshot?.Snapshot,
			prorationSnapshot?.Version.Id, chargePeriod.Start, chargePeriod.End);
		var headIds = lines.Select(x => x.FeeHeadId).Distinct().ToList();
		var headNames = (await feeHeadRepository.GetListAsync(x => headIds.Contains(x.Id)))
			.ToDictionary(x => x.Id, x => x.Name);
		var resultLines = lines.Select(x => new StudentFeeAssignmentPreviewLineDto
		{
			FeeHeadId = x.FeeHeadId,
			FeeHeadName = headNames.GetValueOrDefault(x.FeeHeadId, string.Empty),
			InstallmentLabel = x.GetDisplayInstallmentLabel(),
			CoverageStartDate = x.CoverageStartDate,
			CoverageEndDate = x.CoverageEndDate,
			ScheduledAmount = x.ScheduledAmount,
			ProratedAmount = x.OriginalAmount,
			ReductionAmount = x.ScheduledAmount - x.OriginalAmount,
			Factor = x.ProrationFactor,
			RuleType = x.ProrationRuleType?.ToString(),
			RoundingMode = x.ProrationRoundingMode?.ToString(),
			Explanation = x.ProrationExplanation
		}).ToList();
		return new StudentFeeAssignmentPreviewDto
		{
			ProrationPolicyId = prorationSnapshot?.Policy.Id,
			ProrationPolicyVersionId = prorationSnapshot?.Version.Id,
			ChargeStartDate = chargePeriod.Start,
			ChargeEndDate = chargePeriod.End,
			ScheduledTotal = resultLines.Sum(x => x.ScheduledAmount),
			ProratedTotal = resultLines.Sum(x => x.ProratedAmount),
			ReductionTotal = resultLines.Sum(x => x.ReductionAmount),
			Lines = resultLines
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_StructureAssign.Add)]
	public async Task AssignAsync(AssignStudentFeeStructureDto input)
	{
		var student = await studentRepository.GetAsync(input.StudentId)
			?? throw new BusinessException("Student with given id does not exist.");

		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);

		var feeStructure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId)
			?? throw new BusinessException("Fee structure with given id does not exist.");
		if (!feeStructure.IsLocked)
			throw new BusinessException("Finance:FeeStructureMustBeLocked");
		if (student.StandardId != feeStructure.StandardId)
			throw new BusinessException("Finance:FeeStructureStandardMismatch")
				.WithData("StudentStandardId", student.StandardId)
				.WithData("FeeStructureStandardId", feeStructure.StandardId);
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructure.FeeStructureMetadataId);
		var academicYear = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
		var concessionSnapshot = await ValidateConcessionPolicySnapshotAsync(feeStructure, academicYear.Id);
		var collectionSnapshot = await ValidateCollectionPolicySnapshotAsync(feeStructure, academicYear.Id);
		var refundSnapshot = await ValidateRefundPolicySnapshotAsync(feeStructure, academicYear.Id);
		var prorationSnapshot = await ValidateProrationPolicySnapshotAsync(feeStructure, academicYear.Id);
		var chargePeriod = await ResolveChargePeriodAsync(
			input.ChargeStartDate, input.ChargeEndDate, input.ChargePeriodSource,
			input.ChargePeriodSourceReferenceId, input.ChargePeriodOverrideReason, academicYear);
		var requestFingerprint = BuildAssignmentFingerprint(
			input.StandardFeeStructureId, input.SelectedOptionalFeeHeadIds, input.FeeHeadDetails,
			chargePeriod.Start, chargePeriod.End, chargePeriod.Source,
			chargePeriod.SourceReferenceId, chargePeriod.OverrideReason);
		var existingAssignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x =>
			x.StudentId == student.Id && x.AcademicYearId == academicYear.Id);
		if (existingAssignment != null)
		{
			if (string.Equals(
				existingAssignment.AssignmentRequestFingerprint,
				requestFingerprint,
				StringComparison.Ordinal))
				return;
			throw new BusinessException(
				"A fee structure is already assigned to this student for the selected academic year.");
		}

		student.AssignFeeStructure(input.StandardFeeStructureId);
		var assignmentId = GuidGenerator.Create();
		await studentFeeStructureRepository.InsertAsync(
			new StudentFeeStructure(
				id: assignmentId,
				tenantId: CurrentTenant.Id,
				studentId: student.Id,
				feeStructureStandardId: feeStructure.Id,
				academicYearId: academicYear.Id,
				concessionPolicyId: concessionSnapshot.Policy.Id,
				concessionPolicyVersionId: concessionSnapshot.Version.Id,
				concessionFeeHeadScope: feeStructure.ConcessionFeeHeadScope,
				concessionApplicableFeeHeadIds: feeStructure.GetConcessionApplicableFeeHeadIds(),
				collectionPolicyId: collectionSnapshot.Policy.Id,
				collectionPolicyVersionId: collectionSnapshot.Version.Id,
				refundPolicyId: refundSnapshot?.Policy.Id,
				refundPolicyVersionId: refundSnapshot?.Version.Id,
				prorationPolicyId: prorationSnapshot?.Policy.Id,
				prorationPolicyVersionId: prorationSnapshot?.Version.Id,
				chargeStartDate: chargePeriod.Start,
				chargeEndDate: chargePeriod.End,
				chargePeriodSource: chargePeriod.Source,
				chargePeriodSourceReferenceId: chargePeriod.SourceReferenceId,
				chargePeriodOverrideReason: chargePeriod.OverrideReason,
				assignmentRequestFingerprint: requestFingerprint),
			autoSave: false);

		var details = await feeStructureDetailRepository.GetListAsync(d => d.FeeStructureId == feeStructure.Id);
		foreach (var detail in details.Where(d => feeStructure.FeeStructureDetails.All(x => x.Id != d.Id)))
			feeStructure.AddDetail(detail);

		var optionalDetails = BuildOptionalDetails(input.FeeHeadDetails);

		var dueFees = await dueFeeManager.GenerateForStudentAsync(
			student.Id, feeStructure, academicYear, input.SelectedOptionalFeeHeadIds, optionalDetails,
			prorationSnapshot?.Snapshot, prorationSnapshot?.Version.Id, chargePeriod.Start, chargePeriod.End);

		await dueFeeRepository.InsertManyAsync(dueFees, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task<(ConcessionPolicy Policy, FinancePolicyVersion Version)> ValidateConcessionPolicySnapshotAsync(
		FeeStructure feeStructure, Guid academicYearId)
	{
		if (!feeStructure.ConcessionPolicyId.HasValue || !feeStructure.ConcessionPolicyVersionId.HasValue)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotAttached);
		var policy = await concessionPolicyRepository.FindAsync(feeStructure.ConcessionPolicyId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != ConcessionPolicyStatus.Active)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != academicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.AcademicYearMismatch);
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.ConcessionPolicyVersionId.Value)
			?? throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "concession" || version.PolicyId != policy.Id || version.AcademicYearId != academicYearId)
			throw new BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);
		return (policy, version);
	}

	private async Task<(CollectionPolicy Policy, FinancePolicyVersion Version)> ValidateCollectionPolicySnapshotAsync(
		FeeStructure feeStructure, Guid academicYearId)
	{
		if (!feeStructure.CollectionPolicyId.HasValue || !feeStructure.CollectionPolicyVersionId.HasValue)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotAttached);
		}
		var policy = await collectionPolicyRepository.FindAsync(feeStructure.CollectionPolicyId.Value)
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != CollectionPolicyStatus.Active)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotActive);
		}
		if (policy.AcademicYearId != academicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.AcademicYearMismatch);
		}
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.CollectionPolicyVersionId.Value)
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "collection" || version.PolicyId != policy.Id || version.AcademicYearId != academicYearId)
		{
			throw new BusinessException(CollectionPolicyErrorCodes.PolicyVersionNotFound);
		}
		return (policy, version);
	}

	private async Task<(RefundPolicy Policy, FinancePolicyVersion Version)?> ValidateRefundPolicySnapshotAsync(
		FeeStructure feeStructure, Guid academicYearId)
	{
		if (!feeStructure.RefundPolicyId.HasValue && !feeStructure.RefundPolicyVersionId.HasValue)
			return null;
		if (!feeStructure.RefundPolicyId.HasValue || !feeStructure.RefundPolicyVersionId.HasValue)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
		var policy = await refundPolicyRepository.FindAsync(feeStructure.RefundPolicyId.Value)
			?? throw new BusinessException(RefundPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != RefundPolicyStatus.Active)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != academicYearId)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearMismatch);
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.RefundPolicyVersionId.Value)
			?? throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "refund" || version.PolicyId != policy.Id || version.AcademicYearId != academicYearId)
			throw new BusinessException(RefundPolicyErrorCodes.PolicyVersionNotFound);
		return (policy, version);
	}

	private async Task<(ProrationPolicy Policy, FinancePolicyVersion Version, ProrationPolicySnapshot Snapshot)?>
		ValidateProrationPolicySnapshotAsync(FeeStructure feeStructure, Guid academicYearId)
	{
		if (!feeStructure.ProrationPolicyId.HasValue && !feeStructure.ProrationPolicyVersionId.HasValue)
			return null;
		if (!feeStructure.ProrationPolicyId.HasValue || !feeStructure.ProrationPolicyVersionId.HasValue)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		var policy = await prorationPolicyRepository.FindAsync(feeStructure.ProrationPolicyId.Value)
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyNotFound);
		if (policy.Status != ProrationPolicyStatus.Active)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyNotActive);
		if (policy.AcademicYearId != academicYearId)
			throw new BusinessException(ProrationPolicyErrorCodes.AcademicYearMismatch);
		var version = await financePolicyVersionRepository.FindAsync(feeStructure.ProrationPolicyVersionId.Value)
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		if (version.PolicyType != "proration" || version.PolicyId != policy.Id || version.AcademicYearId != academicYearId)
			throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		var snapshot = System.Text.Json.JsonSerializer.Deserialize<ProrationPolicySnapshot>(version.SnapshotJson)
			?? throw new BusinessException(ProrationPolicyErrorCodes.PolicyVersionNotFound);
		return (policy, version, snapshot);
	}

	private async Task<(DateTime Start, DateTime End, ChargePeriodSource Source, Guid? SourceReferenceId, string? OverrideReason)>
		ResolveChargePeriodAsync(
			DateTime? requestedStart,
			DateTime? requestedEnd,
			string? requestedSource,
			Guid? sourceReferenceId,
			string? overrideReason,
			AcademicYear academicYear)
	{
		if (!Enum.TryParse<ChargePeriodSource>(requestedSource, true, out var source))
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidChargePeriod);
		var start = (requestedStart ?? academicYear.StartDate).Date;
		var end = (requestedEnd ?? academicYear.EndDate).Date;
		if (start > end || start < academicYear.StartDate.Date || end > academicYear.EndDate.Date)
			throw new BusinessException(ProrationPolicyErrorCodes.InvalidChargePeriod);
		if (source == ChargePeriodSource.ManualOverride)
		{
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.OverrideChargePeriod);
			if (string.IsNullOrWhiteSpace(overrideReason))
				throw new BusinessException(ProrationPolicyErrorCodes.ManualOverrideReasonRequired);
		}
		return (start, end, source, sourceReferenceId,
			string.IsNullOrWhiteSpace(overrideReason) ? null : overrideReason.Trim());
	}

	private static string BuildAssignmentFingerprint(
		Guid feeStructureId,
		IEnumerable<Guid> selectedOptionalFeeHeadIds,
		IEnumerable<StudentFeeHeadDetailDto> feeHeadDetails,
		DateTime chargeStart,
		DateTime chargeEnd,
		ChargePeriodSource source,
		Guid? sourceReferenceId,
		string? overrideReason)
	{
		var selected = string.Join(",", selectedOptionalFeeHeadIds.Distinct().Order());
		var details = string.Join(";", feeHeadDetails.OrderBy(x => x.FeeHeadId).Select(x => string.Join("/",
			x.FeeHeadId, x.Amount, x.PayableMonth, x.PayableYear,
			x.StartPayableMonth, x.StartPayableYear, x.EndPayableMonth, x.EndPayableYear,
			x.ChargeStartDate?.Date.ToString("O"), x.ChargeEndDate?.Date.ToString("O"))));
		var canonical = string.Join("|", feeStructureId, selected, details,
			chargeStart.Date.ToString("O"), chargeEnd.Date.ToString("O"), source,
			sourceReferenceId, overrideReason?.Trim());
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
	}
}
