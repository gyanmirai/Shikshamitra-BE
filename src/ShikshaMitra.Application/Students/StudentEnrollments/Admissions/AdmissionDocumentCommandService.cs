using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDocumentCommandService(IRepository<AdmissionDocumentEvidence, Guid> evidence,
	IRepository<AdmissionDocumentReview, Guid> reviews, IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities, AdmissionDocumentAccessService access,
	AdmissionDocumentEvaluationService evaluator, AdmissionDocumentReadService reader, AdmissionDocumentFileService files)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionDocumentVerificationDto> UploadAsync(Guid applicationId, Guid stageId, string code, AdmissionDocumentUploadInput input, bool onboarding = false)
	{
		var (context, item) = await PrepareAsync(applicationId, stageId, code, input, allowEarlyCollection: true, onboarding: onboarding);
		if (item.Requirement.CollectionMode == "original") throw Invalid("This requirement records an original document check only.");
		var id = GuidGenerator.Create();
		var file = await files.SaveAsync(context, item.Requirement, id, input.File);
		var row = NewEvidence(id, context, item, "upload", input.Notes, file);
		await evidence.InsertAsync(row, false);
		return await SaveAsync(context, row, "AdmissionDocumentUploaded");
	}

	public async Task<AdmissionDocumentVerificationDto> RecordOriginalAsync(Guid applicationId, Guid stageId, string code, AdmissionDocumentActionInput input, bool onboarding = false)
	{
		var (context, item) = await PrepareAsync(applicationId, stageId, code, input, onboarding: onboarding);
		if (item.Requirement.CollectionMode == "upload") throw Invalid("This requirement collects an uploaded document only.");
		await access.ValidateMappingAsync(item.Requirement);
		var row = NewEvidence(GuidGenerator.Create(), context, item, "original", input.Notes, item.Latest?.GetFile());
		await evidence.InsertAsync(row, false);
		return await SaveAsync(context, row, "AdmissionDocumentOriginalSeen");
	}

	public async Task<AdmissionDocumentVerificationDto> WaiveAsync(Guid applicationId, Guid stageId, string code, AdmissionDocumentWaiverInput input)
	{
		var (context, item) = await PrepareAsync(applicationId, stageId, code, input);
		if (!context.Policy!.AllowWaivers || item.Status == "waived") throw Invalid("This requirement cannot be waived under the configured policy.");
		var row = NewEvidence(GuidGenerator.Create(), context, item, "waiver", input.Reason);
		await evidence.InsertAsync(row, false);
		return await SaveAsync(context, row, "AdmissionDocumentWaived");
	}

	public async Task<AdmissionDocumentVerificationDto> ReviewAsync(Guid id, AdmissionDocumentReviewInput input, bool onboarding = false)
	{
		var row = await evidence.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionDocumentEvidence), id);
		var (context, item) = await PrepareAsync(row.ApplicationId, row.StageInstanceId, row.RequirementCode, input, onboarding: onboarding);
		if (item.Latest?.Id != id || item.Status != "pendingReview") throw Invalid("Only the latest unreviewed document revision can be reviewed.");
		var review = new AdmissionDocumentReview(GuidGenerator.Create(), CurrentTenant.Id, row, context.Policy!, input.Decision,
			input.Reason, Actor(), DateTime.UtcNow);
		await reviews.InsertAsync(review, false);
		return await SaveAsync(context, row, "AdmissionDocumentReviewed", review);
	}

	private async Task<(AdmissionDocumentContext Context, AdmissionDocumentRequirementEvaluation Item)> PrepareAsync(
		Guid applicationId, Guid stageId, string code, AdmissionDocumentActionInput input, bool allowEarlyCollection = false, bool onboarding = false)
	{
		var context = await access.GetContextAsync(applicationId, stageId, !allowEarlyCollection, onboarding);
		if (context.Policy == null || !context.IsActive && !(allowEarlyCollection && context.CanCollectDuringForm))
			throw Invalid("Upload documents during the configured Form stage or the active Document Verification stage.");
		if (input.Notes?.Length > 1024) throw Invalid("Document notes allow 1024 characters.");
		AdmissionDocumentAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var requirement = AdmissionDocumentAccessService.GetRequirement(context, code);
		var item = (await evaluator.EvaluateAsync(context.Application, stageId, context.Policy!)).Requirements.Single(item => item.Requirement.Code == requirement.Code);
		if (item.ConditionStatus is "notMatched" or "undetermined") throw Invalid("Resolve the requirement's application condition before collecting or reviewing evidence.");
		return (context, item);
	}
	private AdmissionDocumentEvidence NewEvidence(Guid id, AdmissionDocumentContext context, AdmissionDocumentRequirementEvaluation item,
		string kind, string? notes, AdmissionDocumentFile? file = null) => new(id, CurrentTenant.Id, context.Application.Id,
		context.Stage.Id, item.Requirement, (item.Latest?.VersionNumber ?? 0) + 1, kind, Actor(), DateTime.UtcNow,
		JsonSerializer.Serialize(new { documentVerification = context.Policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
		file, kind == "original" ? Actor() : null, kind == "original" ? DateTime.UtcNow : null, notes);
	private async Task<AdmissionDocumentVerificationDto> SaveAsync(AdmissionDocumentContext context, AdmissionDocumentEvidence row, string action,
		AdmissionDocumentReview? review = null)
	{
		await applications.UpdateAsync(context.Application, false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, action,
			$"{row.RequirementLabel}: evidence revision {row.VersionNumber}.", context.Application.ApplicantId, context.Application.Id,
			metadataJson: JsonSerializer.Serialize(new { ActorUserId = Actor(), EvidenceId = row.Id, row.StageInstanceId, row.RequirementCode,
				row.VersionNumber, row.Kind, row.Sha256Hash, row.OriginalSeen, row.Notes, ReviewId = review?.Id, review?.Decision, review?.Reason })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(context.Application.Id, context.Stage.Id, context.IsOnboarding);
	}
	private Guid Actor() => CurrentUser.Id ?? throw Invalid("An authenticated staff member is required.");
	private static BusinessException Invalid(string message) => new(AdmissionDocumentErrorCodes.InvalidState, message);
}
