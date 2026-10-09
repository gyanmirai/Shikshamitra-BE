using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class OnboardingConfigurationManager(
	IRepository<StudentOnboardingSettings, Guid> settingsRepository,
	IRepository<EducationBoard, Guid> boardsRepository,
	IRepository<EducationMedium, Guid> mediumsRepository,
	IRepository<EducationMediumStage, Guid> stagesRepository,
	IRepository<Standard, Guid> standardsRepository,
	IRepository<Division, Guid> divisionsRepository,
	IRepository<AdmissionCampus, Guid> campusesRepository,
	IRepository<AdmissionOffering, Guid> offeringsRepository,
	IRepository<AdmissionCycle, Guid> cyclesRepository) : DomainService
{
	public async Task<OnboardingRuleConfiguration?> GetAsync(Guid yearId)
	{
		var records = await settingsRepository.GetListAsync(s => s.AcademicYearId == yearId || s.AcademicYearId == null);
		var record = records.OrderByDescending(s => s.AcademicYearId.HasValue).FirstOrDefault();
		return record == null ? null : new OnboardingRuleConfiguration(record.ConfigurationJson);
	}

	public async Task ValidateAsync(string json, Guid? yearId)
	{
		var config = new OnboardingRuleConfiguration(json);
		var offeringIds = config.Profiles.SelectMany(p => OnboardingRuleConfiguration.Ids(p, "offeringIds")).ToArray();
		if (offeringIds.Distinct().Count() != offeringIds.Length) throw OnboardingRuleConfiguration.Invalid("An Offering can belong to only one readiness rule.");
		if (offeringIds.Length > 0 && await offeringsRepository.CountAsync(o => Enumerable.Contains(offeringIds, o.Id)) != offeringIds.Length)
			throw OnboardingRuleConfiguration.Invalid("One or more selected Offerings are unavailable.");
		if (yearId.HasValue && offeringIds.Length > 0)
		{
			var cycleIds = (await offeringsRepository.GetListAsync(o => Enumerable.Contains(offeringIds, o.Id))).Select(o => o.AdmissionCycleId).Distinct().ToArray();
			if (await cyclesRepository.CountAsync(c => Enumerable.Contains(cycleIds, c.Id) && c.AcademicYearId == yearId.Value) != cycleIds.Length)
				throw OnboardingRuleConfiguration.Invalid("Selected Offerings must belong to the selected academic year.");
		}
		var boards = await boardsRepository.GetListAsync();
		var mediums = await mediumsRepository.GetListAsync();
		var stages = await stagesRepository.GetListAsync();
		var standards = await standardsRepository.GetListAsync();
		var divisions = await divisionsRepository.GetListAsync();
		var activeCampusIds = (await campusesRepository.GetListAsync(c => c.IsActive))
			.Select(c => c.Id).ToHashSet();
		var paths = new[]
		{
			boards.Where(b => !yearId.HasValue || b.AcademicYearId == yearId).ToDictionary(b => b.Id, _ => (Guid?)null),
			mediums.ToDictionary(m => m.Id, m => (Guid?)m.EducationBoardId),
			stages.ToDictionary(s => s.Id, s => (Guid?)s.EducationMediumId),
			standards.ToDictionary(s => s.Id, s => (Guid?)s.EducationMediumStageId),
			divisions.ToDictionary(d => d.Id, d => (Guid?)d.StandardId)
		};
		var availableSchoolLevelNames = config.AvailableSchoolLevelNames;
		if (availableSchoolLevelNames != null)
		{
			var mediumById = mediums.ToDictionary(m => m.Id);
			var boardById = boards.ToDictionary(b => b.Id);
			var levelById = stages.Where(stage => mediumById.ContainsKey(stage.EducationMediumId) &&
				boardById.ContainsKey(mediumById[stage.EducationMediumId].EducationBoardId))
				.Where(stage => !yearId.HasValue ||
					boardById[mediumById[stage.EducationMediumId].EducationBoardId].AcademicYearId == yearId)
				.ToDictionary(stage => stage.Id, stage =>
				{
					var medium = mediumById[stage.EducationMediumId];
					var board = boardById[medium.EducationBoardId];
					var prefix = $"{board.Type} - {medium.Name} - ";
					return stage.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
						? stage.Name[prefix.Length..].Trim() : stage.Name.Trim();
				});
			var selectedNames = availableSchoolLevelNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (selectedNames.Any(name => !levelById.Values.Contains(name, StringComparer.OrdinalIgnoreCase)))
				throw OnboardingRuleConfiguration.Invalid("Selected onboarding school levels are not configured for this scope.");
			foreach (var rule in config.Applicabilities.Where(rule =>
				OnboardingRuleConfiguration.Text(rule, "stageMode") == "selected"))
				if (OnboardingRuleConfiguration.Ids(rule, "academicMediumStageIds")
					.Any(id => levelById.TryGetValue(id, out var name) && !selectedNames.Contains(name)))
					throw OnboardingRuleConfiguration.Invalid("Existing rules target a hidden onboarding school level.");
		}
		foreach (var rule in config.Applicabilities)
		{
			var campusMode = OnboardingRuleConfiguration.Text(rule, "campusMode", "all");
			var campusIds = OnboardingRuleConfiguration.Ids(rule, "campusIds");
			if (campusMode is not ("all" or "selected") ||
				campusMode == "selected" && campusIds.Length == 0 ||
				campusIds.Distinct().Count() != campusIds.Length ||
				campusIds.Any(id => !activeCampusIds.Contains(id)))
				throw OnboardingRuleConfiguration.Invalid("Select valid active campus coverage.");
			HashSet<Guid>? parents = null;
			for (var i = 0; i < OnboardingRuleConfiguration.Levels.Length; i++)
			{
				var (modeKey, idsKey, _) = OnboardingRuleConfiguration.Levels[i];
				var mode = OnboardingRuleConfiguration.Text(rule, modeKey);
				var ids = OnboardingRuleConfiguration.Ids(rule, idsKey);
				if (mode is not ("all" or "selected") || mode == "selected" && ids.Length == 0 || ids.Distinct().Count() != ids.Length)
					throw OnboardingRuleConfiguration.Invalid($"Select valid {idsKey} coverage.");
				var available = paths[i].Where(pair => parents == null || pair.Value.HasValue && parents.Contains(pair.Value.Value))
					.Select(pair => pair.Key).ToHashSet();
				if (ids.Any(id => !available.Contains(id)))
					throw OnboardingRuleConfiguration.Invalid($"The selected {idsKey} do not belong to the selected academic hierarchy/year.");
				parents = mode == "all" ? available : ids.ToHashSet();
			}
		}
		ValidateConflicts(config.Profiles.Where(p => OnboardingRuleConfiguration.Ids(p, "offeringIds").Length == 0).ToList(), true);
		foreach (var section in new[] { "grNumber", "rollNumber", "divisionChange" })
		{
			var rules = config.Rules(section).ToList();
			if (rules.Select(r => OnboardingRuleConfiguration.Text(r, "id")).Distinct().Count() != rules.Count ||
				rules.Any(r => string.IsNullOrWhiteSpace(OnboardingRuleConfiguration.Text(r, "id"))))
				throw OnboardingRuleConfiguration.Invalid($"Duplicate or missing {section} rule IDs.");
			ValidateConflicts(rules.Where(OnboardingRuleConfiguration.IsPublished).ToList(), false);
			foreach (var rule in rules)
			{
				var date = OnboardingRuleConfiguration.Text(rule, section == "divisionChange" ? "effectiveDate" : "effectiveFrom");
				if (!string.IsNullOrWhiteSpace(date) && !DateTime.TryParseExact(date, "yyyy-MM-dd",
					System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
					throw OnboardingRuleConfiguration.Invalid("Effective dates must be valid yyyy-MM-dd dates.");
				var max = OnboardingRuleConfiguration.Number(rule, "maximumNumber");
				if (max < 0 || max > 0 && max < OnboardingRuleConfiguration.Number(rule, "startingNumber"))
					throw OnboardingRuleConfiguration.Invalid("Maximum number must not be below the starting number.");
			}
			if (section is "grNumber" or "rollNumber")
				foreach (var rule in rules)
					if (OnboardingRuleConfiguration.Number(rule, "startingNumber") < 1 ||
						section == "grNumber" && OnboardingRuleConfiguration.Number(rule, "padding") is < 1 or > 20)
						throw OnboardingRuleConfiguration.Invalid("Number series require a positive starting number and valid padding.");
		}
	}

	private static void ValidateConflicts(IReadOnlyList<JsonElement> rules, bool profiles)
	{
		for (var i = 0; i < rules.Count; i++)
		for (var j = i + 1; j < rules.Count; j++)
		{
			if (profiles && !OnboardingRuleConfiguration.Strings(rules[i], "admissionRoutes")
				.Intersect(OnboardingRuleConfiguration.Strings(rules[j], "admissionRoutes"),
					StringComparer.OrdinalIgnoreCase).Any()) continue;
			var a = profiles ? rules[i].GetProperty("applicability") : rules[i];
			var b = profiles ? rules[j].GetProperty("applicability") : rules[j];
			if (OnboardingRuleConfiguration.Score(a) != OnboardingRuleConfiguration.Score(b)) continue;
			if (OnboardingRuleConfiguration.Text(a, "campusMode", "all") == "selected" &&
				OnboardingRuleConfiguration.Text(b, "campusMode", "all") == "selected" &&
				!OnboardingRuleConfiguration.Ids(a, "campusIds").Intersect(OnboardingRuleConfiguration.Ids(b, "campusIds")).Any())
				continue;
			if (OnboardingRuleConfiguration.Levels.All(level =>
				OnboardingRuleConfiguration.Text(a, level.Mode) == "all" || OnboardingRuleConfiguration.Text(b, level.Mode) == "all" ||
				OnboardingRuleConfiguration.Ids(a, level.Ids).Intersect(OnboardingRuleConfiguration.Ids(b, level.Ids)).Any()))
				throw OnboardingRuleConfiguration.Invalid("Rules with the same priority cannot overlap. Narrow their academic coverage.");
		}
	}
}
