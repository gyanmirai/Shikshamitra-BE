using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsDecisionReader(
	IRepository<AdmissionCrmSeatPool, Guid> poolRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
	IRepository<AdmissionCrmWaitlistEntry, Guid> waitlistRepository,
	IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmConcessionRequest, Guid> concessionRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmAnalyticsDecisionSnapshot> LoadAsync(Guid yearId, List<Guid> leadIds)
	{
		var pools = await AsyncExecuter.ToListAsync((await poolRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		var offers = await AsyncExecuter.ToListAsync((await offerRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var reservations = await AsyncExecuter.ToListAsync((await reservationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var waitlist = await AsyncExecuter.ToListAsync((await waitlistRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var decisions = await AsyncExecuter.ToListAsync((await decisionRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var quotations = await AsyncExecuter.ToListAsync((await quotationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		var concessions = await AsyncExecuter.ToListAsync((await concessionRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId)));
		return new AdmissionCrmAnalyticsDecisionSnapshot(pools, offers, reservations, waitlist, decisions, quotations, concessions);
	}
}
