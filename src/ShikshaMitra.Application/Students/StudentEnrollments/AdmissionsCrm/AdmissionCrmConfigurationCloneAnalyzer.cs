using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationCloneAnalyzer
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmConfigurationClonePreviewDto BuildPreview(AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmConfigurationSnapshot target, int targetApplicantCount)
	{
		var conflicts = new List<string>();
		if (source.Cycle == null) conflicts.Add("The source academic year has no Admission Year settings to clone.");
		if (targetApplicantCount > 0) conflicts.Add(
			$"The target academic year has {targetApplicantCount} applicant record(s); configuration cloning requires no applicant activity.");
		if (target.ConfigurationEntityCount > 0) conflicts.Add(
			$"The target academic year already has {target.ConfigurationEntityCount} admission configuration record(s).");
		var mapping = AdmissionCrmConfigurationHierarchyMapper.Build(source, target);
		conflicts.AddRange(mapping.Conflicts);
		var families = BuildFamilies(source, target, mapping);
		var warnings = BuildWarnings(source, mapping);
		var fingerprint = AdmissionCrmConfigurationImpactWorkflow.Compute(JsonSerializer.Serialize(new
		{
			Type = "AdmissionCrmConfigurationClone", SourceAcademicYearId = source.AcademicYear.Id,
			TargetAcademicYearId = target.AcademicYear.Id,
			SourceSnapshot = AdmissionCrmConfigurationSnapshotSignature.Build(source),
			TargetSnapshot = AdmissionCrmConfigurationSnapshotSignature.Build(target),
			TargetApplicantCount = targetApplicantCount, Families = families, Conflicts = conflicts
		}, JsonOptions));
		return new AdmissionCrmConfigurationClonePreviewDto
		{
			SourceAcademicYearId = source.AcademicYear.Id, TargetAcademicYearId = target.AcademicYear.Id,
			CanConfirm = conflicts.Count == 0, TargetApplicantActivityCount = targetApplicantCount,
			Families = families, Conflicts = conflicts, Warnings = warnings, Fingerprint = fingerprint
		};
	}

	public static IReadOnlyList<AdmissionCrmConfigurationCloneFamilyDto> BuildFamilies(
		AdmissionCrmConfigurationSnapshot source, AdmissionCrmConfigurationSnapshot target,
		AdmissionCrmHierarchyMapping mapping)
	{
		var cloneableSeries = source.NumberSeries.Count(x =>
			AdmissionCrmConfigurationHierarchyMapper.CanMapNumberSeries(x, source, mapping.IdMap));
		return
		[
			Family("Cycle configuration", source.Cycle == null ? 0 : 1, source.Cycle == null ? 0 : 1,
				target.Cycle == null ? 0 : 1, "Settings are copied unlocked with known configuration IDs remapped."),
			Family("Pipeline stages", source.PipelineStages.Count, source.PipelineStages.Count,
				target.PipelineStages.Count, "System and custom stage definitions are copied."),
			Family("Inquiry sources", source.InquirySources.Count, source.InquirySources.Count,
				target.InquirySources.Count, "Codes, allowed channels, routing eligibility, and active state are copied."),
			Family("Inquiry forms", source.InquiryForms.Count, source.InquiryForms.Count,
				target.InquiryForms.Count, "Fields, localizations, privacy metadata, and active state are copied with new IDs."),
			Family("Message templates", source.MessageTemplates.Count, source.MessageTemplates.Count,
				target.MessageTemplates.Count, "Templates are recreated as drafts and known IDs are remapped."),
			Family("Admission playbooks", source.Playbooks.Count, source.Playbooks.Count,
				target.Playbooks.Count, "Journey, round, exam, seat-plan, slot, and assignment configuration is recreated as drafts."),
			Family("Registration templates", source.RegistrationTemplates.Count, source.RegistrationTemplates.Count,
				target.RegistrationTemplates.Count, "Registration templates are recreated as drafts."),
			Family("Registration form number series", source.FormNumberSeries.Count, source.FormNumberSeries.Count,
				target.FormNumberSeries.Count, "Patterns are copied and counters restart at one."),
			Family("Document checklist templates", source.DocumentChecklists.Count, source.DocumentChecklists.Count,
				target.DocumentChecklists.Count, "Document checklists are recreated as drafts."),
			Family("Seat pools", source.SeatPools.Count, source.SeatPools.Count,
				target.SeatPools.Count, "Definitions and capacities are copied; applicant and occupancy counters reset."),
			Family("Fee policies", source.FeePolicies.Count, source.FeePolicies.Count,
				target.FeePolicies.Count, "Fee policies are recreated as drafts with target-year-shifted effective dates."),
			Family("Route definitions", source.RouteDefinitions.Count, source.RouteDefinitions.Count,
				target.RouteDefinitions.Count, "Route definitions are recreated as drafts with target-year-shifted effective dates."),
			Family("Policy adoptions", source.PolicyAdoptions.Count, source.PolicyAdoptions.Count,
				target.PolicyAdoptions.Count, "Year-bound adoptions are recreated by the confirming user; global policy packs are reused."),
			Family("Identifier number series", source.NumberSeries.Count, cloneableSeries,
				target.NumberSeries.Count, "Series are recreated as drafts, hierarchy IDs are mapped to the target year, and counters reset."),
			Family("Global policy packs", source.PolicyPacks.Count, 0,
				target.PolicyPacks.Count, "Shared policy packs are referenced and are never duplicated.")
		];
	}

	public static IReadOnlyList<string> BuildWarnings(AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmHierarchyMapping mapping)
	{
		var warnings = new List<string>
		{
			"Journey locks and approval history are not copied; approval-controlled playbooks, templates, routes, fee policies, and number series are recreated as drafts.",
			"Known inquiry-source, inquiry-form, message-template, playbook, route, and configuration IDs are remapped inside cloned JSON.",
			"Registration-form counters, identifier counters, and seat-pool applicant or occupancy counters are reset for the target year.",
			"Effective dates are shifted by the difference between source and target academic-year start dates.",
			"Global policy packs are reused rather than duplicated; year-bound policy adoptions are recreated by the confirming user."
		};
		if (mapping.ReferencedSourceIds.Count > 0 && mapping.Conflicts.Count == 0)
			warnings.Add($"{mapping.ReferencedSourceIds.Count} source-year academic hierarchy identifier(s) will be mapped to equivalent target-year records.");
		if (ContainsUnmappedGuidValues(source)) warnings.Add(
			"Staff, campus, or other non-hierarchy identifiers that are not known admission configuration IDs are copied verbatim; review owner and external-reference mappings before publishing.");
		return warnings;
	}

	private static AdmissionCrmConfigurationCloneFamilyDto Family(string family, int sourceCount,
		int cloneableCount, int targetCount, string behavior) => new()
	{
		Family = family, SourceCount = sourceCount, CloneableCount = cloneableCount,
		TargetCount = targetCount, CloneBehavior = behavior
	};

	private static bool ContainsUnmappedGuidValues(AdmissionCrmConfigurationSnapshot snapshot)
	{
		var known = new HashSet<Guid>();
		if (snapshot.Cycle != null) known.Add(snapshot.Cycle.Id);
		foreach (var id in snapshot.PipelineStages.Select(x => x.Id).Concat(snapshot.InquirySources.Select(x => x.Id))
			.Concat(snapshot.InquiryForms.Select(x => x.Id)).Concat(snapshot.MessageTemplates.Select(x => x.Id))
			.Concat(snapshot.Playbooks.Select(x => x.Id)).Concat(snapshot.RegistrationTemplates.Select(x => x.Id))
			.Concat(snapshot.FormNumberSeries.Select(x => x.Id)).Concat(snapshot.DocumentChecklists.Select(x => x.Id))
			.Concat(snapshot.SeatPools.Select(x => x.Id)).Concat(snapshot.FeePolicies.Select(x => x.Id))
			.Concat(snapshot.RouteDefinitions.Select(x => x.Id)).Concat(snapshot.PolicyAdoptions.Select(x => x.Id))
			.Concat(snapshot.NumberSeries.Select(x => x.Id))) known.Add(id);
		foreach (var id in snapshot.EducationBoards.Select(x => x.Id).Concat(snapshot.EducationMediums.Select(x => x.Id))
			.Concat(snapshot.EducationMediumStages.Select(x => x.Id)).Concat(snapshot.Standards.Select(x => x.Id))
			.Concat(snapshot.Divisions.Select(x => x.Id))) known.Add(id);
		return AdmissionCrmConfigurationCloneJson.Values(snapshot).SelectMany(AdmissionCrmConfigurationCloneJson.Guids)
			.Any(id => !known.Contains(id));
	}
}
