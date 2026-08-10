using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFinalAdmissionAccess(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var lead = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id))
			?? throw new BusinessException("AdmissionCrm:LeadNotFound");
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	public async Task<AdmissionCrmSeatPool> GetPoolAsync(Guid id, string operation)
	{
		var queryable = await poolRepository.GetQueryableAsync();
		var pool = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id))
			?? throw new BusinessException("AdmissionCrm:SeatPoolNotFound");
		await scopeAccess.EnsureCanAccessAcademicYearAsync(pool.AcademicYearId, operation);
		return pool;
	}

	public async Task<AdmissionCrmSeatOffer> GetOfferAsync(Guid id, string operation)
	{
		var queryable = await offerRepository.GetQueryableAsync();
		var offer = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id))
			?? throw new BusinessException("AdmissionCrm:SeatOfferNotFound");
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(await leadRepository.GetAsync(offer.LeadId), operation);
		return offer;
	}

	public async Task<AdmissionCrmSeatReservation> GetReservationAsync(Guid offerId)
	{
		var queryable = await reservationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.SeatOfferId == offerId))
			?? throw new BusinessException("AdmissionCrm:SeatReservationNotFound");
	}

	public async Task<AdmissionCrmWaitlistEntry> GetWaitlistEntryAsync(Guid id, string operation)
	{
		var queryable = await waitlistRepository.GetQueryableAsync();
		var entry = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == id && x.TenantId == CurrentTenant.Id))
			?? throw new BusinessException("AdmissionCrm:WaitlistEntryNotFound");
		await scopeAccess.EnsureCanAccessAdmissionLeadAsync(await leadRepository.GetAsync(entry.LeadId), operation);
		return entry;
	}
}
