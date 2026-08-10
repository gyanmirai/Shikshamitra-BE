using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationCloneWorkflow(
	AdmissionCrmConfigurationSnapshotReader snapshotReader,
	AdmissionCrmConfigurationCloneWriter cloneWriter,
	AdmissionCrmConfigurationYearAccess yearAccess,
	IAbpDistributedLock distributedLock,
	IUnitOfWorkManager unitOfWorkManager) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmConfigurationClonePreviewDto> PreviewAsync(PreviewAdmissionCrmConfigurationCloneDto input)
	{
		AdmissionCrmConfigurationCloneMetadata.ValidateYearPair(input.SourceAcademicYearId, input.TargetAcademicYearId);
		await yearAccess.EnsureAsync(input.SourceAcademicYearId, "PreviewCloneAsync");
		await yearAccess.EnsureAsync(input.TargetAcademicYearId, "PreviewCloneAsync");
		var source = await snapshotReader.LoadAsync(input.SourceAcademicYearId);
		var target = await snapshotReader.LoadAsync(input.TargetAcademicYearId);
		return AdmissionCrmConfigurationCloneAnalyzer.BuildPreview(source, target,
			await snapshotReader.CountApplicantsAsync(input.TargetAcademicYearId));
	}

	public async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmAsync(ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		AdmissionCrmConfigurationCloneMetadata.ValidateYearPair(input.SourceAcademicYearId, input.TargetAcademicYearId);
		var key = $"AdmissionCrm:ConfigurationClone:{CurrentTenant.Id?.ToString("D") ?? "host"}:{input.TargetAcademicYearId:D}";
		await using var handle = await distributedLock.TryAcquireAsync(key, TimeSpan.FromSeconds(30));
		if (handle == null) throw new BusinessException("AdmissionCrm:ConfigurationCloneBusy")
			.WithData("TargetAcademicYearId", input.TargetAcademicYearId);
		using var unitOfWork = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var result = await ConfirmCoreAsync(input);
		await unitOfWork.CompleteAsync();
		return result;
	}

	private async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCoreAsync(ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		await yearAccess.EnsureAsync(input.SourceAcademicYearId, "ConfirmCloneAsync");
		var targetYear = await yearAccess.EnsureAsync(input.TargetAcademicYearId, "ConfirmCloneAsync");
		var source = await snapshotReader.LoadAsync(input.SourceAcademicYearId);
		var target = await snapshotReader.LoadAsync(input.TargetAcademicYearId);
		if (AdmissionCrmConfigurationCloneMetadata.TryRead(target.Cycle, out var sourceId, out var fingerprint))
		{
			if (sourceId != input.SourceAcademicYearId || !string.Equals(fingerprint, input.PreviewFingerprint, StringComparison.Ordinal))
				throw new BusinessException("AdmissionCrm:ConfigurationCloneFingerprintMismatch");
			var mapping = AdmissionCrmConfigurationHierarchyMapper.Build(source, target);
			return new AdmissionCrmConfigurationCloneResultDto
			{
				SourceAcademicYearId = input.SourceAcademicYearId, TargetAcademicYearId = input.TargetAcademicYearId,
				AlreadyApplied = true, Families = AdmissionCrmConfigurationCloneAnalyzer.BuildFamilies(source, target, mapping),
				Warnings = AdmissionCrmConfigurationCloneAnalyzer.BuildWarnings(source, mapping),
				PreviewFingerprint = input.PreviewFingerprint
			};
		}
		var preview = AdmissionCrmConfigurationCloneAnalyzer.BuildPreview(source, target,
			await snapshotReader.CountApplicantsAsync(input.TargetAcademicYearId));
		if (!string.Equals(preview.Fingerprint, input.PreviewFingerprint, StringComparison.Ordinal))
			throw new BusinessException("AdmissionCrm:ConfigurationCloneFingerprintMismatch")
				.WithData("ExpectedFingerprint", preview.Fingerprint);
		if (!preview.CanConfirm) throw new BusinessException(AdmissionCrmConfigurationCloneMetadata.ConflictCode(preview))
			.WithData("Conflicts", string.Join(" ", preview.Conflicts));
		var hierarchy = AdmissionCrmConfigurationHierarchyMapper.Build(source, target);
		await cloneWriter.WriteAsync(source, targetYear, input.PreviewFingerprint, hierarchy.IdMap);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return new AdmissionCrmConfigurationCloneResultDto
		{
			SourceAcademicYearId = input.SourceAcademicYearId, TargetAcademicYearId = input.TargetAcademicYearId,
			AlreadyApplied = false, Families = preview.Families.Select(x => new AdmissionCrmConfigurationCloneFamilyDto
			{
				Family = x.Family, SourceCount = x.SourceCount, CloneableCount = x.CloneableCount,
				TargetCount = x.CloneableCount, CloneBehavior = x.CloneBehavior
			}).ToList(), Warnings = preview.Warnings, PreviewFingerprint = input.PreviewFingerprint
		};
	}
}
