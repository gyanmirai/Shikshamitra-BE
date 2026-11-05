using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionParentRegistrationPublicAppService
{
	public async Task<AdmissionParentPublicContextDto> UploadAsync(string token, Guid stageId, string code, AdmissionParentUploadInput input)
	{
		// Authorize before consuming an untrusted file; reauthorize within the mutation transaction.
		using var anonymous = principals.Change(new ClaimsPrincipal(new ClaimsIdentity()));
		var authorizedGrant = await access.ResolveAsync(token);
		using var tenant = CurrentTenant.Change(authorizedGrant.TenantId);
		var authorizedApplication = await access.AuthorizeAsync(authorizedGrant);
		var requirement = (await documents.ReadAsync(authorizedApplication, authorizedGrant)).SingleOrDefault(item => item.Dto.StageInstanceId == stageId && item.Dto.Code == code);
		if (requirement == null || !authorizedGrant.AllowsDocuments) throw AdmissionParentRegistrationData.Unavailable();
		var limit = (long)(requirement.Dto.MaxFileSizeMb * 1024 * 1024);
		if (input.File == null || input.File.ContentLength > limit) throw AdmissionParentRegistrationData.Invalid("Choose a document within the configured size limit.");
		await using var source = input.File.GetStream();
		await using var memory = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = await source.ReadAsync(buffer)) > 0)
		{
			if (memory.Length + read > limit) throw AdmissionParentRegistrationData.Invalid("The document is too large.");
			await memory.WriteAsync(buffer.AsMemory(0, read));
		}
		var bytes = memory.ToArray();
		var fingerprint = new { stageId, code, input.Notes, input.SnapshotToken, input.File.FileName, input.File.ContentType, Hash = Convert.ToHexString(SHA256.HashData(bytes)) };
		return await MutateAsync(token, input, "upload", fingerprint, async (grant, application) =>
		{
			if (input.Notes?.Length > 1024) throw AdmissionParentRegistrationData.Invalid("Document notes allow 1024 characters.");
			var item = (await documents.ReadAsync(application, grant)).SingleOrDefault(item => item.Dto.StageInstanceId == stageId && item.Dto.Code == code);
			if (item?.Dto.CanUpload != true) throw AdmissionParentRegistrationData.Invalid("This document is not available for upload through this request.");
			var id = GuidGenerator.Create();
			using var content = new RemoteStreamContent(new MemoryStream(bytes, false), input.File.FileName, input.File.ContentType, bytes.Length);
			var file = await LazyServiceProvider.LazyGetRequiredService<AdmissionDocumentFileService>().SaveForParentAsync(item.Context, item.Evaluation.Requirement, id, content);
			var evidence = new AdmissionDocumentEvidence(id, CurrentTenant.Id, application.Id, stageId, item.Evaluation.Requirement,
				(item.Evaluation.Latest?.VersionNumber ?? 0) + 1, "upload", null, Clock.Now,
				JsonSerializer.Serialize(new { documentVerification = item.Context.Policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
				file, notes: input.Notes, parentRegistrationId: grant.Id, parentContactJson: grant.RecipientJson);
			await data.Repo<AdmissionDocumentEvidence>().InsertAsync(evidence, false);
			grant.Save(Clock.Now);
		});
	}
	public async Task<AdmissionDocumentDownloadDto> DownloadAsync(string token, Guid evidenceId)
	{
		using var anonymous = principals.Change(new ClaimsPrincipal(new ClaimsIdentity()));
		var grant = await access.ResolveAsync(token);
		using var tenant = CurrentTenant.Change(grant.TenantId);
		var application = await access.AuthorizeAsync(grant);
		var requirements = await documents.ReadAsync(application, grant);
		if (!requirements.Any(item => item.Dto.Evidence.Any(file => file.Id == evidenceId && file.CanDownload))) throw AdmissionParentRegistrationData.Unavailable();
		var evidence = await data.Repo<AdmissionDocumentEvidence>().GetAsync(evidenceId);
		if (evidence.ApplicationId != application.Id || evidence.TenantId != grant.TenantId || evidence.BlobName == null) throw AdmissionParentRegistrationData.Unavailable();
		var bytes = await LazyServiceProvider.LazyGetRequiredService<IBlobContainer>().GetAllBytesOrNullAsync(evidence.BlobName) ?? throw AdmissionParentRegistrationData.Unavailable();
		return new() { Content = bytes, FileName = evidence.FileName!, ContentType = evidence.ContentType! };
	}
}
