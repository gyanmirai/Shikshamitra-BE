using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationRouteReadiness(
	IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmRouteDefinition, Guid> definitionRepository) : ApplicationService, ITransientDependency
{
	public async Task<(bool Ready, string Detail)> EvaluateAsync(AdmissionInquiryLead lead)
	{
		var query = await assignmentRepository.GetQueryableAsync();
		var history = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var assignment = history.FirstOrDefault(x => x.IsPrimary);
		var definition = assignment == null ? null : await definitionRepository.FindAsync(assignment.RouteDefinitionId);
		return Evaluate(lead, assignment, history, definition);
	}

	private static (bool Ready, string Detail) Evaluate(
		AdmissionInquiryLead lead,
		AdmissionCrmRouteAssignment? assignment,
		IReadOnlyCollection<AdmissionCrmRouteAssignment> history,
		AdmissionCrmRouteDefinition? definition)
	{
		var governedIntent = lead.IntendedRouteType.HasValue && lead.IntendedRouteType.Value != AdmissionCrmRouteType.Fresh;
		if (assignment == null)
			return governedIntent
				? (false, $"The selected {lead.IntendedRouteNameSnapshot ?? lead.IntendedRouteType!.Value.ToString()} pathway requires an evaluated route assignment.")
				: (true, lead.IntendedRouteType == AdmissionCrmRouteType.Fresh
					? "Fresh admission is an ordinary intake intent and does not require a governed route assignment."
					: "No governed admission route applies.");
		if (governedIntent && !CorrespondsToIntent(lead, assignment, history))
			return (false, "The evaluated route assignment does not correspond to the selected pathway.");
		if (definition == null || definition.TenantId != lead.TenantId ||
		    definition.AcademicYearId != assignment.AcademicYearId || definition.Version != assignment.RouteDefinitionVersion ||
		    definition.RouteType != assignment.RouteType)
			return (false, "The assigned admission route definition snapshot is unavailable or inconsistent.");
		var evidence = AdmissionCrmRouteEvidenceValidator.Validate(definition.ConfigurationJson, assignment.EvidenceJson);
		if (!evidence.IsValid)
			return (false, evidence.MissingCodes.Count > 0
				? $"Required route evidence is missing: {string.Join(", ", evidence.MissingCodes)}."
				: "The assigned admission route contains invalid evidence references.");
		var eligible = assignment.EvaluatedAt.HasValue && assignment.Status is
			AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved;
		return eligible
			? (true, governedIntent ? "The selected pathway has an evaluated eligible route assignment." : "The assigned admission route is eligible.")
			: (false, "The assigned admission route is not evaluated as eligible.");
	}

	private static bool CorrespondsToIntent(
		AdmissionInquiryLead lead,
		AdmissionCrmRouteAssignment current,
		IReadOnlyCollection<AdmissionCrmRouteAssignment> history)
	{
		var byId = history.ToDictionary(x => x.Id);
		var visited = new HashSet<Guid>();
		AdmissionCrmRouteAssignment? candidate = current;
		while (candidate != null && visited.Add(candidate.Id))
		{
			if (candidate.Id != current.Id && (candidate.IsPrimary || candidate.Status != AdmissionCrmRouteEligibilityStatus.Superseded))
				return false;
			var matches = lead.IntendedRouteDefinitionId.HasValue
				? candidate.RouteDefinitionId == lead.IntendedRouteDefinitionId.Value &&
				  candidate.RouteDefinitionVersion == lead.IntendedRouteDefinitionVersion
				: candidate.RouteType == lead.IntendedRouteType;
			if (matches) return true;
			candidate = candidate.PreviousAssignmentId.HasValue && byId.TryGetValue(candidate.PreviousAssignmentId.Value, out var previous)
				? previous : null;
		}
		return false;
	}
}
