using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDocumentFileService(IBlobContainer blobs, AdmissionDocumentAccessService access,
	IRepository<AdmissionDocumentEvidence, Guid> evidence, IRepository<AdmissionActivity, Guid> activities)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionDocumentFile> SaveAsync(AdmissionDocumentContext context, AdmissionDocumentRequirement requirement,
		Guid evidenceId, IRemoteStreamContent file)
	{
		if (file == null) throw Invalid("Choose a document to upload.");
		var documentType = await access.ValidateMappingAsync(requirement);
		var limit = (long)(Math.Min(requirement.MaxFileSizeMb, documentType?.MaxSizeMB ?? requirement.MaxFileSizeMb) * 1024 * 1024);
		if (file.ContentLength > limit) throw Invalid("The document exceeds the configured file size limit.");
		await using var input = file.GetStream();
		await using var output = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = await input.ReadAsync(buffer)) > 0)
		{
			if (output.Length + read > limit) throw Invalid("The document exceeds the configured file size limit.");
			await output.WriteAsync(buffer.AsMemory(0, read));
		}
		var content = output.ToArray();
		if (file.ContentLength.HasValue && file.ContentLength != content.LongLength) throw Invalid("The uploaded document length does not match its content.");
		var formats = requirement.AllowedExtensions.Select(extension => Enum.Parse<AcceptedFileFormat>(extension, true)).ToArray();
		DocumentFileValidationResult validation;
		try
		{
			validation = DocumentFileValidator.Validate(file.FileName ?? string.Empty, file.ContentType, content.LongLength, content,
				requirement.Name, requirement.MaxFileSizeMb, formats);
			if (documentType != null) DocumentFileValidator.Validate(file.FileName ?? string.Empty, file.ContentType, content.LongLength, content, documentType);
		}
		catch (BusinessException exception) { throw Invalid("The document does not match the allowed format, file content, or size.").WithData("ValidationCode", exception.Code); }
		if (validation.SafeFileName.Length > 255) throw Invalid("Use a document filename of at most 255 characters.");
		var blobName = $"admission-evidence/{CurrentTenant.Id:N}/{context.Application.Id:N}/{evidenceId:N}";
		await blobs.SaveAsync(blobName, content, overrideExisting: false);
		return new(validation.SafeFileName, validation.ContentType, content.LongLength, validation.Sha256Hash, blobName,
			CurrentUser.Id ?? throw Invalid("An authenticated staff member is required."));
	}

	public async Task<AdmissionDocumentDownloadDto> DownloadAsync(Guid id)
	{
		var row = await evidence.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionDocumentEvidence), id);
		var context = await access.GetContextAsync(row.ApplicationId, row.StageInstanceId, false);
		if (row.BlobName == null) throw Invalid("This evidence revision has no uploaded file.");
		var content = await blobs.GetAllBytesOrNullAsync(row.BlobName) ?? throw Invalid("The private document file is unavailable.");
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("AdmissionDocumentDownloaded",
			$"Downloaded {row.RequirementLabel}, revision {row.VersionNumber}.", context.Application.ApplicantId, row.ApplicationId,
			metadataJson: JsonSerializer.Serialize(new { ActorUserId = CurrentUser.Id, EvidenceId = id, row.VersionNumber })), false);
		return new() { Content = content, FileName = row.FileName!, ContentType = row.ContentType! };
	}
	private static BusinessException Invalid(string message) => new(AdmissionDocumentErrorCodes.InvalidEvidence, message);
}
