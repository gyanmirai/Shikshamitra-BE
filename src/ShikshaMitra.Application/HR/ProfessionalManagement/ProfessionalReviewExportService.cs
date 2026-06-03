using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.ProfessionalManagement;

[RemoteService(false)]
public class ProfessionalReviewExportService(
	ProfessionalManagementReviewReader reviewReader)
	: ApplicationService, ITransientDependency
{
	public async Task<byte[]> GetReviewPdfAsync(Guid id)
	{
		var review = await reviewReader.GetReviewAsync(id);
		var observations = await reviewReader.GetObservationsAsync(id);
		var evidence = await reviewReader.GetEvidenceAsync(id);
		var feedback = await reviewReader.GetFeedbackAsync(id);
		var audit = await reviewReader.GetAuditTrailAsync(id);
		return ProfessionalReviewPdfComposer.Compose(review, observations, evidence, feedback, audit, Clock.Now);
	}
}
