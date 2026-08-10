using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationReferenceWorkflow(
	AdmissionCrmConfigurationSnapshotReader snapshotReader,
	AdmissionCrmConfigurationYearAccess yearAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmConfigurationReferenceResultDto> GetAsync(Guid yearId,
		AdmissionCrmConfigurationReferenceKind kind, string code)
	{
		var normalized = Normalize(code);
		await yearAccess.EnsureAsync(yearId, "GetReferencesAsync");
		var families = await BuildFamiliesAsync(await snapshotReader.LoadAsync(yearId), kind, normalized);
		return new AdmissionCrmConfigurationReferenceResultDto { AcademicYearId = yearId, Kind = kind, Code = normalized,
			TotalReferenceCount = families.Sum(x => x.ReferenceCount), Families = families };
	}

	internal async Task<IReadOnlyList<AdmissionCrmConfigurationReferenceFamilyDto>> BuildFamiliesAsync(
		AdmissionCrmConfigurationSnapshot snapshot, AdmissionCrmConfigurationReferenceKind kind, string code)
	{
		var families = new List<AdmissionCrmConfigurationReferenceFamilyDto>();
		void Add(string family, int count, string description, IEnumerable<Guid>? ids = null) => families.Add(new()
		{
			Family = family, ReferenceCount = count, Description = description,
			SampleReferenceIds = (ids ?? []).Distinct().OrderBy(x => x).Take(25).ToList()
		});
		var cycleCount = snapshot.Cycle == null ? 0 : AdmissionCrmConfigurationReferenceCounter.Count(code,
			snapshot.Cycle.LeadScoringRulesJson, snapshot.Cycle.LeadRoutingRulesJson, snapshot.Cycle.JourneyRulesJson,
			snapshot.Cycle.ApplicantChecklistJson, snapshot.Cycle.ChannelSettingsJson, snapshot.Cycle.SlaPolicyJson);
		Add("Cycle configuration", cycleCount, "Routing, journey, channel, checklist, scoring, and SLA settings that reference this code.",
			cycleCount > 0 && snapshot.Cycle != null ? [snapshot.Cycle.Id] : []);
		var stageIds = snapshot.PipelineStages.Where(x => kind == AdmissionCrmConfigurationReferenceKind.Stage &&
			string.Equals(x.StageKey, code, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
		Add("Pipeline stage definitions", stageIds.Count + snapshot.PipelineStages.Sum(x =>
			AdmissionCrmConfigurationReferenceCounter.Count(code, x.AllowedNextStagesJson)),
			"Stage definitions and allowed-transition settings that reference this code.", stageIds);
		var sourceIds = snapshot.InquirySources.Where(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
		Add("Inquiry sources", sourceIds.Count, "Inquiry-source definitions with this code.", sourceIds);
		var formMatches = snapshot.InquiryForms.Select(x => new { x.Id, Count = AdmissionCrmConfigurationReferenceCounter.Count(code,
			x.FieldsJson, x.LocalizationsJson) }).Where(x => x.Count > 0).ToList();
		Add("Inquiry forms", formMatches.Sum(x => x.Count),
			"Published or draft inquiry-form fields and localizations that reference this code.", formMatches.Select(x => x.Id));
		var messageMatches = snapshot.MessageTemplates.Select(x => new { x.Id,
			Count = AdmissionCrmConfigurationReferenceCounter.Count(code, x.VariablesJson) +
			AdmissionCrmConfigurationReferenceCounter.CountExact(code, x.Subject) +
			AdmissionCrmConfigurationReferenceCounter.CountExact(code, x.Body) }).Where(x => x.Count > 0).ToList();
		Add("Message templates", messageMatches.Sum(x => x.Count),
			"Message variables or exact template values that reference this code.", messageMatches.Select(x => x.Id));
		AddPlaybook(families, "Playbook stages", "Admission-journey stage definitions that reference this code.", snapshot.Playbooks, x => x.StagesJson, code);
		AddPlaybook(families, "Playbook rounds", "Admission-round definitions that reference this code.", snapshot.Playbooks, x => x.RoundsJson, code);
		AddPlaybook(families, "Playbook exams", "Exam definitions that reference this code.", snapshot.Playbooks, x => x.ExamDefinitionsJson, code);
		AddPlaybook(families, "Playbook seat plans", "Seat-plan definitions that reference this code.", snapshot.Playbooks, x => x.SeatPlansJson, code);
		var playbookRules = snapshot.Playbooks.Select(x => new { x.Id, Count = AdmissionCrmConfigurationReferenceCounter.Count(code,
			x.ApplicabilityJson, x.SlotTemplatesJson, x.AssignmentRulesJson) }).Where(x => x.Count > 0).ToList();
		Add("Playbook routing and assignment rules", playbookRules.Sum(x => x.Count),
			"Applicability, slot-template, and assignment rules that reference this code.", playbookRules.Select(x => x.Id));
		var routeDefinitions = snapshot.RouteDefinitions.Where(x => kind == AdmissionCrmConfigurationReferenceKind.Route &&
			string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)).ToList();
		var routeConfigurations = snapshot.RouteDefinitions.Select(x => new { x.Id,
			Count = AdmissionCrmConfigurationReferenceCounter.Count(code, x.ConfigurationJson) }).Where(x => x.Count > 0).ToList();
		Add("Route definitions", routeDefinitions.Count + routeConfigurations.Sum(x => x.Count),
			"Route definitions and immutable route configuration snapshots that reference this code.",
			routeDefinitions.Select(x => x.Id).Concat(routeConfigurations.Select(x => x.Id)));
		var pools = snapshot.SeatPools.Where(x => (kind == AdmissionCrmConfigurationReferenceKind.SeatPlan &&
			string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) || (kind == AdmissionCrmConfigurationReferenceKind.Round &&
			string.Equals(x.RoundCode, code, StringComparison.OrdinalIgnoreCase))).ToList();
		Add("Seat pools", pools.Count, "Operational seat-pool definitions linked to this seat-plan or round code.", pools.Select(x => x.Id));
		AddJsonFamilies(snapshot, code, Add);
		var routeIds = routeDefinitions.Select(x => x.Id).ToList();
		var applicants = await snapshotReader.GetApplicantReferencesAsync(snapshot.AcademicYear.Id, kind, code, routeIds);
		Add("Applicant records", applicants.Count, "Current-year applicants whose stage or immutable route snapshot references this code.", applicants.SampleIds);
		return families;
	}

	private static void AddJsonFamilies(AdmissionCrmConfigurationSnapshot snapshot, string code,
		Action<string, int, string, IEnumerable<Guid>?> add)
	{
		var registrations = snapshot.RegistrationTemplates.Select(x => new { x.Id,
			Count = AdmissionCrmConfigurationReferenceCounter.Count(code, x.ApplicabilityJson, x.SectionsJson) }).Where(x => x.Count > 0).ToList();
		var series = snapshot.FormNumberSeries.Select(x => new { x.Id,
			Count = AdmissionCrmConfigurationReferenceCounter.Count(code, x.ApplicabilityJson) }).Where(x => x.Count > 0).ToList();
		add("Registration templates and number series", registrations.Sum(x => x.Count) + series.Sum(x => x.Count),
			"Registration sections, applicability rules, and form-number rules that reference this code.",
			registrations.Select(x => x.Id).Concat(series.Select(x => x.Id)));
		AddJson(snapshot.DocumentChecklists, x => x.Id,
			x => AdmissionCrmConfigurationReferenceCounter.Count(code, x.ApplicabilityJson, x.RequirementsJson),
			"Document checklist templates", "Document applicability and requirement rules that reference this code.", add);
		AddJson(snapshot.FeePolicies, x => x.Id,
			x => AdmissionCrmConfigurationReferenceCounter.Count(code, x.ApplicabilityJson, x.ComponentsJson),
			"Fee policies", "Fee-policy applicability and component rules that reference this code.", add);
		AddJson(snapshot.PolicyAdoptions, x => x.Id,
			x => AdmissionCrmConfigurationReferenceCounter.Count(code, x.ApplicabilityJson, x.OverridesJson, x.ImpactPreviewJson),
			"Policy adoptions", "Target-year policy applicability, overrides, and impact snapshots that reference this code.", add);
		AddJson(snapshot.PolicyPacks, x => x.Id,
			x => AdmissionCrmConfigurationReferenceCounter.Count(code, x.SourcesJson, x.RulesJson),
			"Global policy packs", "Shared governed policy packs referenced by this academic year; these are global and are not cloned.", add);
	}

	private static void AddJson<T>(IEnumerable<T> values, Func<T, Guid> id, Func<T, int> count,
		string family, string description, Action<string, int, string, IEnumerable<Guid>?> add)
	{
		var matches = values.Select(x => new { Id = id(x), Count = count(x) }).Where(x => x.Count > 0).ToList();
		add(family, matches.Sum(x => x.Count), description, matches.Select(x => x.Id));
	}

	private static void AddPlaybook(ICollection<AdmissionCrmConfigurationReferenceFamilyDto> families, string family,
		string description, IEnumerable<AdmissionCrmPlaybook> playbooks, Func<AdmissionCrmPlaybook, string> json, string code)
	{
		var matches = playbooks.Select(x => new { x.Id, Count = AdmissionCrmConfigurationReferenceCounter.Count(code, json(x)) })
			.Where(x => x.Count > 0).ToList();
		families.Add(new AdmissionCrmConfigurationReferenceFamilyDto { Family = family, ReferenceCount = matches.Sum(x => x.Count),
			Description = description, SampleReferenceIds = matches.Select(x => x.Id).OrderBy(x => x).Take(25).ToList() });
	}

	public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
