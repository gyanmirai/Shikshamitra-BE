using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationImpactWorkflow(
	AdmissionCrmConfigurationSnapshotReader snapshotReader,
	AdmissionCrmConfigurationReferenceWorkflow references,
	AdmissionCrmConfigurationYearAccess yearAccess) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewAsync(Guid yearId, PreviewAdmissionCrmConfigurationImpactDto input) =>
		BuildAsync(yearId, input, "PreviewImpactAsync", true);

	public async Task EnsureFingerprintAsync(Guid yearId, PreviewAdmissionCrmConfigurationImpactDto input,
		string? submittedFingerprint, string operation)
	{
		if (string.IsNullOrWhiteSpace(submittedFingerprint))
			throw new BusinessException("AdmissionCrm:ConfigurationImpactPreviewRequired").WithData("Operation", operation);
		var preview = await BuildAsync(yearId, input, operation, false);
		if (!string.Equals(preview.Fingerprint, submittedFingerprint.Trim(), StringComparison.Ordinal))
			throw new BusinessException("AdmissionCrm:ConfigurationImpactFingerprintMismatch")
				.WithData("ExpectedFingerprint", preview.Fingerprint).WithData("Operation", operation);
	}

	public async Task<string> CreateCycleFingerprintAsync(Guid yearId, UpdateAdmissionCrmCycleConfigurationDto input, string operation) =>
		(await BuildAsync(yearId, new PreviewAdmissionCrmConfigurationImpactDto { ProposedCycleConfiguration = input }, operation, false)).Fingerprint;

	private async Task<AdmissionCrmConfigurationImpactPreviewDto> BuildAsync(Guid yearId,
		PreviewAdmissionCrmConfigurationImpactDto input, string operation, bool enforceAccess)
	{
		AdmissionCrmConfigurationImpactAnalyzer.Validate(input);
		if (enforceAccess) await yearAccess.EnsureAsync(yearId, operation);
		var snapshot = await snapshotReader.LoadAsync(yearId);
		var stage = input.PipelineStageId.HasValue
			? snapshot.PipelineStages.SingleOrDefault(x => x.Id == input.PipelineStageId.Value) : null;
		if (input.PipelineStageId.HasValue && stage == null)
			throw new BusinessException("AdmissionCrm:PipelineStageNotFound")
				.WithData("PipelineStageId", input.PipelineStageId.Value).WithData("AcademicYearId", yearId);
		var kind = stage == null ? input.Kind : AdmissionCrmConfigurationReferenceKind.Stage;
		var code = stage?.StageKey ?? input.Code;
		var applicantCount = await snapshotReader.CountApplicantsAsync(yearId);
		var families = kind.HasValue ? await references.BuildFamiliesAsync(snapshot, kind.Value,
			AdmissionCrmConfigurationReferenceWorkflow.Normalize(code!)) : [];
		var changed = AdmissionCrmConfigurationImpactAnalyzer.ChangedFields(snapshot.Cycle, input.ProposedCycleConfiguration)
			.Concat(AdmissionCrmConfigurationImpactAnalyzer.ChangedFields(stage, input.ProposedPipelineStage))
			.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
		var referenceCount = families.Sum(x => x.ReferenceCount);
		var fingerprint = Compute(JsonSerializer.Serialize(new
		{
			Type = "AdmissionCrmConfigurationImpact", AcademicYearId = yearId,
			Snapshot = AdmissionCrmConfigurationSnapshotSignature.Build(snapshot), ApplicantCount = applicantCount,
			Input = AdmissionCrmConfigurationImpactAnalyzer.InputSignature(kind, code, input.ProposedCycleConfiguration,
				input.PipelineStageId, input.ProposedPipelineStage), ChangedFields = changed, ReferenceCount = referenceCount
		}, JsonOptions));
		return new AdmissionCrmConfigurationImpactPreviewDto { AcademicYearId = yearId,
			AffectedApplicantCount = changed.Count == 0 && !kind.HasValue ? 0 : applicantCount,
			ConfigurationReferenceCount = referenceCount, ChangedFields = changed, ReferenceFamilies = families,
			Warnings = AdmissionCrmConfigurationImpactAnalyzer.Warnings(applicantCount, referenceCount, changed, kind, code),
			Fingerprint = fingerprint };
	}

	public static string Compute(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
