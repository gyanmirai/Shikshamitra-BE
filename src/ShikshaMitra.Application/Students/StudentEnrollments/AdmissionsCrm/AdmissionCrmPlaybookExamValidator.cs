using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookExamValidator
{
	private static readonly StringComparer CodeComparer = AdmissionCrmPlaybookValidationRules.CodeComparer;
	private static readonly HashSet<string> SupportedTieBreakers = new(["SubmissionTime", "LeadScore", "InquiryDate"], CodeComparer);

	public static void Validate(IReadOnlyList<AdmissionCrmExamDefinitionDto> definitions,
		IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds, IReadOnlyList<AdmissionCrmSlotTemplateDto> templates,
		IReadOnlyList<AdmissionCrmSlotAssignmentRuleDto> rules)
	{
		ValidateDefinitions(definitions, rounds);
		ValidateTemplates(templates, definitions);
		ValidateRules(rules, templates);
	}

	private static void ValidateDefinitions(IReadOnlyList<AdmissionCrmExamDefinitionDto> definitions,
		IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		AdmissionCrmPlaybookValidationRules.Unique(definitions.Select(x => x.Code), "AdmissionCrm:DuplicateExamDefinitionCode");
		var roundCodes = rounds.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var definition in definitions)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(definition.Code, definition.Name);
			if (definition.DurationMinutes <= 0 || definition.Components.Count == 0)
				throw new BusinessException("AdmissionCrm:InvalidExamDefinition").WithData("ExamCode", definition.Code);
			if (!string.IsNullOrWhiteSpace(definition.RoundCode) && !roundCodes.Contains(definition.RoundCode))
				throw new BusinessException("AdmissionCrm:ExamRoundNotFound").WithData("ExamCode", definition.Code);
			AdmissionCrmPlaybookValidationRules.Unique(definition.Components.Select(x => x.Code), "AdmissionCrm:DuplicateExamComponentCode");
			foreach (var component in definition.Components)
			{
				AdmissionCrmPlaybookValidationRules.CodeAndName(component.Code, component.Name);
				if (component.MaximumMarks <= 0 || component.MinimumMarks < 0 || component.MinimumMarks > component.MaximumMarks ||
				    component.WeightPercentage <= 0 || component.ScorePrecision is < 0 or > 4)
					throw new BusinessException("AdmissionCrm:InvalidExamComponent").WithData("ComponentCode", component.Code);
			}
			if (definition.Components.Sum(x => x.WeightPercentage) != 100)
				throw new BusinessException("AdmissionCrm:ExamComponentWeightsMustTotalOneHundred").WithData("ExamCode", definition.Code);
			var rule = definition.ResultRule;
			if (rule.AggregatePassingPercentage is < 0 or > 100 || rule.MeritCapacity < 0 || rule.WaitlistCapacity < 0)
				throw new BusinessException("AdmissionCrm:InvalidExamResultRule").WithData("ExamCode", definition.Code);
			var componentCodes = definition.Components.Select(x => x.Code).ToHashSet(CodeComparer);
			if (rule.TieBreakerCodes.Any(x => !componentCodes.Contains(x) && !SupportedTieBreakers.Contains(x)))
				throw new BusinessException("AdmissionCrm:UnknownExamTieBreaker").WithData("ExamCode", definition.Code);
		}
	}

	private static void ValidateTemplates(IReadOnlyList<AdmissionCrmSlotTemplateDto> templates,
		IReadOnlyList<AdmissionCrmExamDefinitionDto> definitions)
	{
		AdmissionCrmPlaybookValidationRules.Unique(templates.Select(x => x.Code), "AdmissionCrm:DuplicateSlotTemplateCode");
		var examCodes = definitions.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var template in templates)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(template.Code, template.Name);
			if (!examCodes.Contains(template.ExamDefinitionCode))
				throw new BusinessException("AdmissionCrm:SlotTemplateExamNotFound").WithData("SlotTemplateCode", template.Code);
			if (template.Capacity <= 0 || template.RoomNo <= 0 || template.ReportingTime > template.StartTime ||
			    template.StartTime >= template.EndTime || (template.ValidFrom.HasValue && template.ValidTo < template.ValidFrom))
				throw new BusinessException("AdmissionCrm:InvalidSlotTemplate").WithData("SlotTemplateCode", template.Code);
		}
	}

	private static void ValidateRules(IReadOnlyList<AdmissionCrmSlotAssignmentRuleDto> rules,
		IReadOnlyList<AdmissionCrmSlotTemplateDto> templates)
	{
		AdmissionCrmPlaybookValidationRules.Unique(rules.Select(x => x.Code), "AdmissionCrm:DuplicateSlotAssignmentRuleCode");
		AdmissionCrmPlaybookValidationRules.Unique(rules.Select(x => x.Priority.ToString()), "AdmissionCrm:DuplicateSlotAssignmentRulePriority");
		var templateCodes = templates.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var rule in rules)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(rule.Code, rule.Name);
			if (string.IsNullOrWhiteSpace(rule.Trigger) || rule.Priority < 0)
				throw new BusinessException("AdmissionCrm:InvalidSlotAssignmentRule").WithData("RuleCode", rule.Code);
			if (!string.IsNullOrWhiteSpace(rule.SlotTemplateCode) && !templateCodes.Contains(rule.SlotTemplateCode))
				throw new BusinessException("AdmissionCrm:SlotAssignmentTemplateNotFound").WithData("RuleCode", rule.Code);
		}
	}
}
