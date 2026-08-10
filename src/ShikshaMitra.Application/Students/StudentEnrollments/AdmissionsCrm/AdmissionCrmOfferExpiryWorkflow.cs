using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOfferExpiryWorkflow(
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmOfferLifecycleWorkflow lifecycle) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmOfferExpiryRunResultDto> RunAsync(Guid yearId, bool enforceUserScope)
	{
		var queryable = await offerRepository.GetQueryableAsync();
		var overdueQuery = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
			(x.Status == AdmissionCrmSeatOfferStatus.Issued || x.Status == AdmissionCrmSeatOfferStatus.Opened) && x.ValidUntil <= Clock.Now);
		if (enforceUserScope)
		{
			var leads = await leadRepository.GetQueryableAsync();
			var scoped = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leads.Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
			overdueQuery = overdueQuery.Where(x => scoped.Select(lead => lead.Id).Contains(x.LeadId));
		}
		var overdue = await AsyncExecuter.ToListAsync(overdueQuery);
		var failed = new List<Guid>();
		var expired = 0;
		foreach (var offer in overdue)
		{
			try { await lifecycle.ExpireAsync(offer); expired++; }
			catch (Exception exception) when (exception is BusinessException or AbpAuthorizationException)
			{
				failed.Add(offer.Id);
				Logger.LogError(exception, "Failed to expire admission offer {OfferId}.", offer.Id);
			}
		}
		return new AdmissionCrmOfferExpiryRunResultDto { AcademicYearId = yearId, EvaluatedCount = overdue.Count,
			ExpiredCount = expired, FailedCount = failed.Count, FailedOfferIds = failed };
	}
}
