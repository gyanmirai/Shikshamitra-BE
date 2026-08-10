using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationSeatWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository) : ApplicationService, ITransientDependency
{
	public async Task MarkActivatedAsync(Guid leadId)
	{
		var query = await offerRepository.GetQueryableAsync();
		var offer = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId && x.Status == AdmissionCrmSeatOfferStatus.Accepted));
		if (offer == null) return;
		var pool = await poolRepository.GetAsync(offer.SeatPoolId);
		if (pool.TenantId != CurrentTenant.Id)
			throw new AbpAuthorizationException("Seat pool belongs to another tenant.");
		pool.MarkActivated();
		await poolRepository.UpdateAsync(pool, autoSave: true);
	}
}
