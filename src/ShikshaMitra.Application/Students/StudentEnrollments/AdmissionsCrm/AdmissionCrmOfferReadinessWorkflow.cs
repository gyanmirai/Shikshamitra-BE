using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOfferReadinessWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository) : ApplicationService, ITransientDependency
{
	public async Task EnsureOfferAsync(AdmissionInquiryLead lead, Guid? registrationId)
	{
		var queryable = await registrationRepository.GetQueryableAsync();
		if (registrationId.HasValue)
		{
			var registration = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.Id == registrationId.Value &&
				x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
			if (registration == null || registration.Status != AdmissionCrmRegistrationStatus.AdmissionReady)
				throw new BusinessException("AdmissionCrm:AdmissionReadyRegistrationRequired");
			return;
		}
		var hasRegistration = await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var ready = await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id &&
			x.Status == AdmissionCrmRegistrationStatus.AdmissionReady));
		if ((hasRegistration && !ready) || (!hasRegistration && lead.Stage is not (AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Waitlisted)))
			throw new BusinessException("AdmissionCrm:ApplicantNotReadyForSeatOffer");
	}

	public async Task EnsurePaymentAsync(Guid leadId, AdmissionCrmPaymentGateMilestone milestone)
	{
		var queryable = await quotationRepository.GetQueryableAsync();
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId && x.Status == AdmissionCrmQuotationStatus.Issued).OrderByDescending(x => x.Version));
		if (quotation == null || quotation.GateMilestone > milestone || quotation.IsGateSatisfiedAt(Clock.Now)) return;
		throw new BusinessException("AdmissionCrm:PaymentGateNotSatisfied").WithData("QuotationId", quotation.Id)
			.WithData("GateType", quotation.GateType).WithData("GateMilestone", quotation.GateMilestone)
			.WithData("FinanceState", quotation.FinanceState).WithData("RequiredAmount", quotation.RequiredAmount)
			.WithData("ConfirmedAmount", quotation.ConfirmedAmount);
	}
}
