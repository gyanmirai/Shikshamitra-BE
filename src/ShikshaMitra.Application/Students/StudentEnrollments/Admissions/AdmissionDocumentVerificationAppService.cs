using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionDocumentVerificationAppService(AdmissionDocumentReadService reader, AdmissionDocumentCommandService commands,
	AdmissionDocumentFileService files, IRepository<DocumentType, Guid> documentTypes) : ApplicationService, IAdmissionDocumentVerificationAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionDocumentVerificationDto> GetAsync(Guid applicationId, Guid stageInstanceId) => reader.GetAsync(applicationId, stageInstanceId);
	[Authorize(Permissions.Upload)]
	public Task<AdmissionDocumentVerificationDto> UploadAsync(Guid applicationId, Guid stageInstanceId, string code, AdmissionDocumentUploadInput input) => commands.UploadAsync(applicationId, stageInstanceId, code, input);
	[Authorize(Permissions.RecordOriginal)]
	public Task<AdmissionDocumentVerificationDto> RecordOriginalAsync(Guid applicationId, Guid stageInstanceId, string code, AdmissionDocumentActionInput input) => commands.RecordOriginalAsync(applicationId, stageInstanceId, code, input);
	[Authorize(Permissions.Waive)]
	public Task<AdmissionDocumentVerificationDto> WaiveAsync(Guid applicationId, Guid stageInstanceId, string code, AdmissionDocumentWaiverInput input) => commands.WaiveAsync(applicationId, stageInstanceId, code, input);
	[Authorize(Permissions.Review)]
	public Task<AdmissionDocumentVerificationDto> ReviewAsync(Guid evidenceId, AdmissionDocumentReviewInput input) => commands.ReviewAsync(evidenceId, input);
	[Authorize(Permissions.Download)]
	public Task<AdmissionDocumentDownloadDto> DownloadAsync(Guid evidenceId) => files.DownloadAsync(evidenceId);
	[Authorize(WorkflowPermissions.View)]
	public async Task<List<AdmissionDocumentTypeDto>> GetDocumentTypesAsync() => (await documentTypes.GetListAsync()).OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name)
		.Select(item => new AdmissionDocumentTypeDto { Id = item.Id, Name = item.Name, IsRequired = item.IsRequired,
			AllowedExtensions = item.GetAcceptedFormats().Select(format => format.ToString().ToLowerInvariant()).ToArray(), MaxFileSizeMb = item.MaxSizeMB }).ToList();
}
