using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteImpactReader(
	IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> documentRepository,
	AdmissionCrmRouteDefinitionWorkflow definitionWorkflow,
	AdmissionCrmRouteDataValidator routeDataValidator,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmRouteAssignmentWorkflow assignmentWorkflow) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRouteImpactPreviewDto> GetAsync(
		Guid leadId,
		AssignAdmissionCrmSpecialRouteDto input,
		string operation)
	{
		var lead = await leadAccessReader.GetAsync(leadId, operation);
		var definition = await definitionWorkflow.GetAsync(input.RouteDefinitionId);
		var assignmentQuery = await assignmentRepository.GetQueryableAsync();
		var current = await AsyncExecuter.FirstOrDefaultAsync(assignmentQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId && entity.IsPrimary));
		assignmentWorkflow.EnsureCanAssign(definition, lead);
		await routeDataValidator.ValidateAsync(definition.RouteType, input.RouteData, lead);
		AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, input.EvidenceReferences);

		var impacts = new List<AdmissionCrmRouteImpactItemDto>();
		void Impact(string area, string detail, bool blocking = false) => impacts.Add(new()
			{ Area = area, Impact = detail, RequiresResolution = blocking });

		if (current != null && current.RouteDefinitionId != definition.Id)
			Impact("Route", $"{current.RouteType} version {current.RouteDefinitionVersion} will be superseded by {definition.RouteType} version {definition.Version}.");

		var offerQuery = await offerRepository.GetQueryableAsync();
		var openOffers = await AsyncExecuter.ToListAsync(offerQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId &&
			entity.Status != AdmissionCrmSeatOfferStatus.Declined &&
			entity.Status != AdmissionCrmSeatOfferStatus.Expired &&
			entity.Status != AdmissionCrmSeatOfferStatus.Revoked &&
			entity.Status != AdmissionCrmSeatOfferStatus.Cancelled &&
			entity.Status != AdmissionCrmSeatOfferStatus.Superseded));
		if (openOffers.Count > 0)
			Impact("Seat offer", $"{openOffers.Count} active offer(s) require review before the route change is finalized.", true);

		var quoteQuery = await quotationRepository.GetQueryableAsync();
		var activeQuotes = await AsyncExecuter.ToListAsync(quoteQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId && entity.Status == AdmissionCrmQuotationStatus.Issued));
		if (activeQuotes.Count > 0)
			Impact("Fees", "The active admission quotation must be reviewed against the target route fee policy.", true);

		var documentQuery = await documentRepository.GetQueryableAsync();
		var checklistCount = await AsyncExecuter.CountAsync(documentQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId));
		if (checklistCount > 0)
			Impact("Documents", "The assigned document checklist must be recalculated for the target route.");

		var configuration = AdmissionCrmRouteMapper.Deserialize<AdmissionCrmRouteConfigurationDto>(definition.ConfigurationJson) ?? new();
		if (configuration.RequiresPolicyEvaluation)
			Impact("Policy", "Route eligibility and applicable policy rules must be evaluated for the target route.", true);
		if (!string.IsNullOrWhiteSpace(configuration.SeatPoolCode))
			Impact("Seat pool", $"Target route expects seat pool {configuration.SeatPoolCode}.");
		if (configuration.DecisionSlaHours.HasValue)
			Impact("Deadline", $"The route decision SLA will be {configuration.DecisionSlaHours} hour(s).");
		if (impacts.Count == 0)
			Impact("Route", "No downstream admission artifacts require recalculation.");

		return new AdmissionCrmRouteImpactPreviewDto
		{
			LeadId = leadId,
			CurrentRoute = current == null ? null : AdmissionCrmRouteMapper.Map(current),
			TargetRoute = AdmissionCrmRouteMapper.Map(definition),
			Impacts = impacts,
			HasBlockingImpact = impacts.Any(item => item.RequiresResolution)
		};
	}
}
