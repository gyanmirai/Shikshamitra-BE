using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Reads finalized evidence without treating waivers or physical originals as uploaded files.</summary>
[RemoteService(false)]
public class AdmissionStudentEvidenceReader(
	IRepository<AdmissionDocumentEvidence, Guid> evidenceRepository,
	IRepository<AdmissionDocumentReview, Guid> reviewRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionDocumentEvidence>> GetLinkedAsync(Guid studentId)
	{
		var evidence = await evidenceRepository.GetQueryableAsync();
		var applications = await applicationRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(evidence.Where(item => item.StudentId == studentId &&
			applications.Any(application => application.Id == item.ApplicationId &&
				application.StudentId == studentId && application.Status == AdmissionApplicationStatus.AdmissionFinalized)));
	}

	public async Task<List<AdmissionDocumentReview>> GetReviewsAsync(IReadOnlyCollection<AdmissionDocumentEvidence> evidence)
	{
		if (evidence.Count == 0) return [];
		var ids = evidence.Select(item => item.Id).ToList();
		return await reviewRepository.GetListAsync(review => ids.Contains(review.EvidenceId));
	}

	public async Task<HashSet<Guid>> GetAcceptedUploadDocumentTypeIdsAsync(Guid studentId)
	{
		var evidence = await GetLinkedAsync(studentId);
		var reviews = await GetReviewsAsync(evidence);
		var acceptedEvidence = reviews.GroupBy(item => item.EvidenceId)
			.Where(group => group.OrderByDescending(item => item.ReviewedAt).First().Decision == "accepted")
			.Select(group => group.Key).ToHashSet();
		return evidence.GroupBy(item => new { item.ApplicationId, item.StageInstanceId, item.RequirementCode })
			.Select(group => group.OrderByDescending(item => item.VersionNumber).First())
			.Where(item => item.Kind != "waiver" && item.DocumentTypeId.HasValue &&
				!string.IsNullOrWhiteSpace(item.BlobName) && item.Size > 0 && acceptedEvidence.Contains(item.Id) &&
				AdmissionDocumentRules.HasCollectionEvidence(item, item.CollectionMode))
			.Select(item => item.DocumentTypeId!.Value).ToHashSet();
	}
}
