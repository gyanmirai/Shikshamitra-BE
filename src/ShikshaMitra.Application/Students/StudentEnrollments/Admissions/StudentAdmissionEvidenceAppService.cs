using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Documents;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
[Authorize(Permissions.View)]
public class StudentAdmissionEvidenceAppService(
	StudentScopeAccessService studentAccess,
	AdmissionStudentEvidenceReader reader,
	IPermissionChecker permissions,
	IBlobContainer blobs,
	IRepository<AdmissionActivity, Guid> activityRepository) : ApplicationService, IStudentAdmissionEvidenceAppService
{
	public async Task<List<StudentAdmissionEvidenceDto>> GetListAsync(Guid studentId)
	{
		await studentAccess.GetStudentAsync(studentId, nameof(GetListAsync));
		var evidence = await reader.GetLinkedAsync(studentId);
		var reviews = (await reader.GetReviewsAsync(evidence)).ToLookup(item => item.EvidenceId);
		var canDownload = await permissions.IsGrantedAsync(Permissions.Download);
		return evidence.OrderByDescending(item => item.SubmittedAt).ThenByDescending(item => item.VersionNumber)
			.Select(item => ToDto(item, reviews[item.Id].ToList(), canDownload)).ToList();
	}

	[Authorize(Permissions.Download)]
	public async Task<StudentDocumentDownloadDto> DownloadAsync(Guid studentId, Guid evidenceId)
	{
		await studentAccess.GetStudentAsync(studentId, nameof(DownloadAsync));
		var evidence = (await reader.GetLinkedAsync(studentId)).SingleOrDefault(item => item.Id == evidenceId);
		if (evidence == null || evidence.Kind == "waiver" || string.IsNullOrWhiteSpace(evidence.BlobName))
			throw new EntityNotFoundException(typeof(AdmissionDocumentEvidence), evidenceId);
		await using var stream = await blobs.GetAsync(evidence.BlobName);
		using var content = new MemoryStream();
		await stream.CopyToAsync(content);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"StudentAdmissionEvidenceDownloaded", $"Admission evidence {evidence.Id:N} downloaded from the Student record.",
			applicationId: evidence.ApplicationId), autoSave: true);
		return new StudentDocumentDownloadDto
		{
			FileName = evidence.FileName!, ContentType = evidence.ContentType!, Content = content.ToArray()
		};
	}

	private static StudentAdmissionEvidenceDto ToDto(AdmissionDocumentEvidence evidence,
		List<AdmissionDocumentReview> reviews, bool canDownload) => new()
	{
		Id = evidence.Id,
		ApplicationId = evidence.ApplicationId,
		StageInstanceId = evidence.StageInstanceId,
		RequirementCode = evidence.RequirementCode,
		RequirementLabel = evidence.RequirementLabel,
		DocumentTypeId = evidence.DocumentTypeId,
		VersionNumber = evidence.VersionNumber,
		Kind = evidence.Kind,
		Status = AdmissionDocumentRules.Status(evidence, reviews),
		FileName = evidence.FileName,
		ContentType = evidence.ContentType,
		Size = evidence.Size,
		OriginalSeen = evidence.OriginalSeen,
		OriginalSeenBy = evidence.OriginalSeenBy,
		OriginalSeenAt = evidence.OriginalSeenAt,
		SubmittedBy = evidence.SubmittedBy,
		SubmittedAt = evidence.SubmittedAt,
		Notes = evidence.Notes,
		StudentLinkedAt = evidence.StudentLinkedAt,
		StudentLinkedBy = evidence.StudentLinkedBy,
		CanDownload = canDownload && evidence.Kind != "waiver" && !string.IsNullOrWhiteSpace(evidence.BlobName),
		Reviews = reviews.OrderByDescending(review => review.ReviewedAt).Select(review => new StudentAdmissionEvidenceReviewDto
		{
			Id = review.Id, Decision = review.Decision, Reason = review.Reason,
			ReviewedBy = review.ReviewedBy, ReviewedAt = review.ReviewedAt
		}).ToList()
	};
}
