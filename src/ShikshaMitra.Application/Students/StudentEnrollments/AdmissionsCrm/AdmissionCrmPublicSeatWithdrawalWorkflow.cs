using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicSeatWithdrawalWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository) : ApplicationService, ITransientDependency
{
	public async Task ReleaseAsync(AdmissionInquiryLead lead, string reason, DateTime at)
	{
		var offers = await AsyncExecuter.ToListAsync((await offerRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id));
		var offer = offers.OrderByDescending(item => item.Version).FirstOrDefault(item => item.Status is
			AdmissionCrmSeatOfferStatus.Draft or AdmissionCrmSeatOfferStatus.ApprovalPending or
			AdmissionCrmSeatOfferStatus.Approved or AdmissionCrmSeatOfferStatus.Issued or
			AdmissionCrmSeatOfferStatus.Opened or AdmissionCrmSeatOfferStatus.Accepted);
		if (offer != null)
		{
			await ReleaseOfferAsync(offer, reason, at);
		}
		await WithdrawWaitlistAsync(lead, reason, at);
	}

	private async Task ReleaseOfferAsync(AdmissionCrmSeatOffer offer, string reason, DateTime at)
	{
		switch (offer.Status)
		{
			case AdmissionCrmSeatOfferStatus.Accepted: offer.CancelAccepted(reason, at); break;
			case AdmissionCrmSeatOfferStatus.Approved:
			case AdmissionCrmSeatOfferStatus.Issued:
			case AdmissionCrmSeatOfferStatus.Opened: offer.Revoke(reason, at); break;
			default: offer.MarkSuperseded(reason, at); break;
		}
		await offerRepository.UpdateAsync(offer, autoSave: true);
		var reservation = await AsyncExecuter.FirstOrDefaultAsync((await reservationRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.SeatOfferId == offer.Id));
		if (reservation == null || reservation.Status is not (
			AdmissionCrmSeatReservationStatus.Active or AdmissionCrmSeatReservationStatus.Committed)) return;
		var pool = await poolRepository.GetAsync(reservation.SeatPoolId);
		if (pool.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:SeatPoolNotFound");
		var wasAccepted = reservation.Status == AdmissionCrmSeatReservationStatus.Committed;
		reservation.Release(reason, at, cancelled: true);
		pool.ReleaseSeat(wasAccepted, cancelled: true);
		await reservationRepository.UpdateAsync(reservation, autoSave: true);
		await poolRepository.UpdateAsync(pool, autoSave: true);
	}

	private async Task WithdrawWaitlistAsync(AdmissionInquiryLead lead, string reason, DateTime at)
	{
		var entries = await AsyncExecuter.ToListAsync((await waitlistRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id));
		foreach (var entry in entries.Where(item => item.Status == AdmissionCrmWaitlistStatus.Active))
		{
			entry.Withdraw(reason, at);
			var pool = await poolRepository.GetAsync(entry.SeatPoolId);
			if (pool.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:SeatPoolNotFound");
			pool.RemoveWaitlistedApplicant();
			await waitlistRepository.UpdateAsync(entry, autoSave: true);
			await poolRepository.UpdateAsync(pool, autoSave: true);
		}
		foreach (var entry in entries.Where(item => item.Status == AdmissionCrmWaitlistStatus.Promoted))
		{
			entry.ClosePromotedOffer(reason, at);
			await waitlistRepository.UpdateAsync(entry, autoSave: true);
		}
	}
}
