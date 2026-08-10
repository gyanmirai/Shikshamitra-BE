using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationsDataReader(
	AdmissionCrmOperationsCoreDataReader coreReader,
	AdmissionCrmOperationsGovernanceDataReader governanceReader,
	AdmissionCrmOperationsDecisionDataReader decisionReader) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmOperationsData> LoadAsync(Guid academicYearId, string operation)
	{
		var core = await coreReader.LoadAsync(academicYearId, operation);
		var leadIds = core.Leads.Select(x => x.Id).ToList();
		var governance = await governanceReader.LoadAsync(academicYearId, leadIds);
		var decision = await decisionReader.LoadAsync(academicYearId, leadIds);
		return new AdmissionCrmOperationsData(core.Leads, core.Exams, core.Slots, core.Registrations,
			core.Checklists, core.Documents, governance.Routes, governance.RouteAppeals,
			governance.Activations, governance.ServiceHandoffs, governance.PolicyExceptions,
			governance.Quotations, governance.IdentityProfiles, decision.Decisions,
			decision.Offers, decision.SeatPools, Clock.Now);
	}
}

public class AdmissionCrmOperationsCoreDataReader(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> checklistRepository,
	IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmOperationsCoreData> LoadAsync(Guid yearId, string operation)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, operation);
		var leadQuery = (await leadRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		leadQuery = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leadQuery);
		var leads = await AsyncExecuter.ToListAsync(leadQuery);
		var leadIds = leads.Select(x => x.Id).ToList();
		return new AdmissionCrmOperationsCoreData(
			leads,
			await AsyncExecuter.ToListAsync((await examRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await slotRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId)),
			await AsyncExecuter.ToListAsync((await registrationRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await checklistRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await documentRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))));
	}
}

public class AdmissionCrmOperationsGovernanceDataReader(
	IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
	IRepository<AdmissionCrmRouteAppeal, Guid> routeAppealRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmServiceHandoff, Guid> serviceHandoffRepository,
	IRepository<AdmissionCrmPolicyException, Guid> policyExceptionRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityProfileRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmOperationsGovernanceData> LoadAsync(Guid yearId, List<Guid> leadIds) =>
		new(
			await AsyncExecuter.ToListAsync((await routeRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await routeAppealRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await activationRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await serviceHandoffRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await policyExceptionRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await quotationRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await identityProfileRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))));
}

public class AdmissionCrmOperationsDecisionDataReader(
	IRepository<AdmissionCrmAdmissionDecision, Guid> decisionRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmOperationsDecisionData> LoadAsync(Guid yearId, List<Guid> leadIds) =>
		new(
			await AsyncExecuter.ToListAsync((await decisionRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await offerRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && leadIds.Contains(x.LeadId))),
			await AsyncExecuter.ToListAsync((await seatPoolRepository.GetQueryableAsync()).Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId)));
}

internal sealed record AdmissionCrmOperationsCoreData(
	IReadOnlyList<AdmissionInquiryLead> Leads,
	IReadOnlyList<AdmissionCrmEntranceExamRecord> Exams,
	IReadOnlyList<AdmissionCrmExamSlot> Slots,
	IReadOnlyList<AdmissionCrmRegistrationApplication> Registrations,
	IReadOnlyList<AdmissionCrmDocumentChecklistAssignment> Checklists,
	IReadOnlyList<AdmissionCrmApplicantDocument> Documents);

internal sealed record AdmissionCrmOperationsGovernanceData(
	IReadOnlyList<AdmissionCrmRouteAssignment> Routes,
	IReadOnlyList<AdmissionCrmRouteAppeal> RouteAppeals,
	IReadOnlyList<AdmissionCrmActivation> Activations,
	IReadOnlyList<AdmissionCrmServiceHandoff> ServiceHandoffs,
	IReadOnlyList<AdmissionCrmPolicyException> PolicyExceptions,
	IReadOnlyList<AdmissionCrmAdmissionQuotation> Quotations,
	IReadOnlyList<AdmissionCrmApplicantIdentityProfile> IdentityProfiles);

internal sealed record AdmissionCrmOperationsDecisionData(
	IReadOnlyList<AdmissionCrmAdmissionDecision> Decisions,
	IReadOnlyList<AdmissionCrmSeatOffer> Offers,
	IReadOnlyList<AdmissionCrmSeatPool> SeatPools);

internal sealed record AdmissionCrmOperationsData(
	IReadOnlyList<AdmissionInquiryLead> Leads,
	IReadOnlyList<AdmissionCrmEntranceExamRecord> Exams,
	IReadOnlyList<AdmissionCrmExamSlot> Slots,
	IReadOnlyList<AdmissionCrmRegistrationApplication> Registrations,
	IReadOnlyList<AdmissionCrmDocumentChecklistAssignment> Checklists,
	IReadOnlyList<AdmissionCrmApplicantDocument> Documents,
	IReadOnlyList<AdmissionCrmRouteAssignment> Routes,
	IReadOnlyList<AdmissionCrmRouteAppeal> RouteAppeals,
	IReadOnlyList<AdmissionCrmActivation> Activations,
	IReadOnlyList<AdmissionCrmServiceHandoff> ServiceHandoffs,
	IReadOnlyList<AdmissionCrmPolicyException> PolicyExceptions,
	IReadOnlyList<AdmissionCrmAdmissionQuotation> Quotations,
	IReadOnlyList<AdmissionCrmApplicantIdentityProfile> IdentityProfiles,
	IReadOnlyList<AdmissionCrmAdmissionDecision> Decisions,
	IReadOnlyList<AdmissionCrmSeatOffer> Offers,
	IReadOnlyList<AdmissionCrmSeatPool> SeatPools,
	DateTime Now);