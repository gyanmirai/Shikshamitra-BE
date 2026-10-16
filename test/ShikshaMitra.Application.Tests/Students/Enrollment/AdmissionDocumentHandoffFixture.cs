using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static readonly byte[] HandoffPdf = "%PDF-1.7 admission evidence"u8.ToArray();
	private static readonly Guid HandoffReviewer = Guid.Parse("38383838-3838-3838-3838-383838383838");

	private async Task<DocumentHandoffFixture> CreateDocumentHandoffFixtureAsync(
		AdmissionDocumentPolicy policy, bool existingStudent = false)
	{
		if (!await WithUnitOfWorkAsync(() => _scopeRepository.AnyAsync(item => item.UserId == TestUserId)))
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Document handoff fixture scope"), autoSave: true);
		var legacy = await CreateAndPublishCurrentWorkflowAsync();
		var draft = await _workflowAppService.CreateNewVersionAsync(legacy.Id);
		var input = ToUpdateInput(draft);
		var entry = input.Stages.Single(item => item.IsEntryStage);
		var terminal = input.Stages.Single(item => item.IsTerminalStage);
		terminal.DisplayOrder = 2;
		var documentStageId = Guid.NewGuid();
		input.Stages.Add(new CreateUpdateAdmissionWorkflowStageDto
		{
			Id = documentStageId, Code = "DOCUMENTS", Name = "Verify documents", DisplayOrder = 1,
			StageType = AdmissionWorkflowStageType.DocumentVerification, IsRequired = true,
			ConfigurationJson = JsonSerializer.Serialize(new { documentVerification = policy with {
				Requirements = policy.Requirements.Select(requirement => requirement with {
					Requirement = requirement.Requirement == "conditional" ? "required" : requirement.Requirement,
					ConditionJson = null }).ToList() } })
		});
		input.Transitions =
		[
			new() { FromStageId = entry.Id!.Value, ToStageId = documentStageId, IsDefault = true },
			new() { FromStageId = documentStageId, ToStageId = terminal.Id!.Value, IsDefault = true }
		];
		draft = await _workflowAppService.UpdateAsync(draft.Id, input);
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		// Historical published policies retain their original evidence/review settings.
		// Current authoring intentionally normalizes those retired editor options.
		if (!policy.Requirements.Any(requirement => requirement.Requirement == "conditional"))
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
			var stored = await repository.GetAsync(published.Stages.Single(stage => stage.StageType == AdmissionWorkflowStageType.DocumentVerification).Id);
			stored.Update(stored.Code, stored.Name, stored.StageType, stored.DisplayOrder, stored.IsRequired,
				stored.AllowManualSkip, stored.Description, JsonSerializer.Serialize(new { documentVerification = policy }), stored.IsEntryStage, stored.IsTerminalStage);
			await repository.UpdateAsync(stored, true);
		});
		var offering = await CreateAvailableOfferingAsync(published.Id, registrationFormId:
			AdmissionFormStageAccessService.ReadFormDefinitionId(published.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson));
		Guid? studentId = existingStudent ? (await CreateExistingStudentAsync(offering, "Handoff", "Patel")).Id : null;
		var applicant = await CreateCertificationApplicantAsync("Handoff", true, true, studentId);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await _applicationAppService.SubmitAsync(application.Id);
		var workflow = await _applicationAppService.GetWorkflowAsync(application.Id);
		if (workflow.Stages.Single(item => item.Id == workflow.CurrentStageInstanceId).StageType != AdmissionWorkflowStageType.DocumentVerification)
			workflow = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new AdvanceAdmissionWorkflowDto
			{
				ExpectedCurrentStageInstanceId = workflow.CurrentStageInstanceId!.Value,
				ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
				IdempotencyKey = $"handoff-enter-documents-{application.Id:N}"
			});
		return new DocumentHandoffFixture(application, offering, workflow.CurrentStageInstanceId!.Value, policy, studentId);
	}

	private Task RestoreHistoricalDocumentPolicyAsync(DocumentHandoffFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var runtime = await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetAsync(fixture.StageId);
		var repository = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
		var stored = await repository.GetAsync(runtime.DefinitionStageId);
		stored.Update(stored.Code, stored.Name, stored.StageType, stored.DisplayOrder, stored.IsRequired,
			stored.AllowManualSkip, stored.Description, JsonSerializer.Serialize(new { documentVerification = fixture.Policy }), stored.IsEntryStage, stored.IsTerminalStage);
		await repository.UpdateAsync(stored, true);
	});

	private async Task<AdmissionDocumentEvidence> AddHandoffEvidenceAsync(DocumentHandoffFixture fixture,
		AdmissionDocumentRequirement requirement, int version = 1, string kind = "upload", string? decision = "accepted")
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			AdmissionDocumentFile? file = null;
			if (kind == "upload" || kind == "original" && requirement.CollectionMode == "both")
			{
				var blobName = $"admission-documents/{TestTenantId:N}/handoff-test/{Guid.NewGuid():N}";
				await GetRequiredService<IBlobContainer>().SaveAsync(blobName, HandoffPdf);
				file = new AdmissionDocumentFile("proof.pdf", "application/pdf", HandoffPdf.Length,
					Convert.ToHexString(SHA256.HashData(HandoffPdf)).ToLowerInvariant(), blobName, TestUserId);
			}
			var evidence = new AdmissionDocumentEvidence(Guid.NewGuid(), TestTenantId, fixture.Application.Id,
				fixture.StageId, requirement, version, kind, TestUserId, DateTime.UtcNow,
				JsonSerializer.Serialize(new { documentVerification = fixture.Policy }), file,
				kind == "original" ? TestUserId : null, kind == "original" ? DateTime.UtcNow : null,
				kind == "upload" ? null : "Original inspected or waiver reason recorded by authorized staff.");
			await GetRequiredService<IRepository<AdmissionDocumentEvidence, Guid>>().InsertAsync(evidence, autoSave: true);
			if (decision != null && kind != "waiver")
				await GetRequiredService<IRepository<AdmissionDocumentReview, Guid>>().InsertAsync(
					new AdmissionDocumentReview(Guid.NewGuid(), TestTenantId, evidence, fixture.Policy,
						decision, decision == "rejected" ? "Replace the illegible document." : "Identity and evidence verified.",
						HandoffReviewer, DateTime.UtcNow), autoSave: true);
			return evidence;
		});
	}

	private async Task<AdmissionApplicationDto> CompleteDocumentHandoffApplicationAsync(DocumentHandoffFixture fixture)
	{
		await CompleteCertificationWorkflowAsync(fixture.Application.Id, stopBeforeApproval: true);
		var application = await _applicationAppService.GetAsync(fixture.Application.Id);
		application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Approved"));
		await CompleteCertificationWorkflowAsync(fixture.Application.Id);
		return await _applicationAppService.GetAsync(application.Id);
	}

	private async Task<DocumentType> CreateHandoffDocumentTypeAsync() => await WithUnitOfWorkAsync(async () =>
	{
		var type = await GetRequiredService<DocumentTypeManager>().CreateAsync(TestTenantId,
			$"Birth certificate {Guid.NewGuid():N}", 10, [AcceptedFileFormat.Pdf], isRequired: true);
		return await GetRequiredService<IRepository<DocumentType, Guid>>().InsertAsync(type, autoSave: true);
	});

	private static AdmissionDocumentPolicy HandoffPolicy(params AdmissionDocumentRequirement[] requirements) =>
		new() { Version = 1, Enabled = true, AllowWaivers = true, Requirements = requirements.ToList() };

	private sealed record DocumentHandoffFixture(AdmissionApplicationDto Application, AdmissionOffering Offering,
		Guid StageId, AdmissionDocumentPolicy Policy, Guid? ExistingStudentId);
}
