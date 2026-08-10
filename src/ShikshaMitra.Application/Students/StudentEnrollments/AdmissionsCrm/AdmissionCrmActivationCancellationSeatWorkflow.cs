using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationCancellationSeatWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository) : ApplicationService, ITransientDependency
{
	public async Task CancelAsync(AdmissionCrmActivation activation, Guid userId)
	{
		var query = await offerRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == activation.LeadId));
		var offer = offers.Where(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted)
			.OrderByDescending(x => x.RespondedAt).FirstOrDefault();
		if (offer != null)
		{
			offer.CancelAccepted(activation.CancellationReason!, Clock.Now);
			await offerRepository.UpdateAsync(offer);
			if (offer.ReservationId.HasValue)
			{
				var reservation = await reservationRepository.FindAsync(offer.ReservationId.Value);
				if (reservation?.TenantId == CurrentTenant.Id)
				{
					reservation.Release(activation.CancellationReason!, Clock.Now, cancelled: true);
					await reservationRepository.UpdateAsync(reservation);
				}
			}
			var pool = await poolRepository.GetAsync(offer.SeatPoolId);
			if (pool.TenantId != CurrentTenant.Id)
				throw new AbpAuthorizationException("Seat pool belongs to another tenant.");
			pool.CancelCommittedSeat();
			await poolRepository.UpdateAsync(pool);
		}
		var decision = new AdmissionCrmAdmissionDecision(GuidGenerator.Create(), CurrentTenant.Id,
			activation.AcademicYearId, activation.LeadId);
		decision.Decide(AdmissionCrmAdmissionDecisionStatus.Cancelled, offer?.SeatPoolId, offer?.Id,
			activation.CancellationReason!, false, null, userId, Clock.Now);
		await decisionRepository.InsertAsync(decision);
	}
}
