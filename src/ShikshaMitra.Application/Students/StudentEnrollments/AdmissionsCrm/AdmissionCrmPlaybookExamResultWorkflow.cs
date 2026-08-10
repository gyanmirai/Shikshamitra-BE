using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookExamResultWorkflow(AdmissionCrmPlaybookAccess access) : ITransientDependency
{
	private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;

	public async Task<AdmissionCrmExamResultPreviewDto> PreviewAsync(Guid id, PreviewAdmissionCrmExamResultDto input)
	{
		var playbook = await access.GetAsync(id, "PreviewExamResultAsync");
		var definition = AdmissionCrmPlaybookMapper.Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson)
			.FirstOrDefault(x => CodeComparer.Equals(x.Code, input.ExamDefinitionCode))
			?? throw new BusinessException("AdmissionCrm:ExamDefinitionNotFound").WithData("ExamDefinitionCode", input.ExamDefinitionCode);
		var scores = new Dictionary<string, decimal>(input.ComponentScores, CodeComparer);
		var results = new List<AdmissionCrmExamComponentResultPreviewDto>();
		var reasons = new List<string>();
		foreach (var component in definition.Components)
		{
			if (!scores.TryGetValue(component.Code, out var score))
			{
				if (component.IsMandatory)
					throw new BusinessException("AdmissionCrm:MandatoryExamComponentScoreRequired").WithData("ComponentCode", component.Code);
				score = 0;
			}
			if (score < 0 || score > component.MaximumMarks)
				throw new BusinessException("AdmissionCrm:InvalidExamComponentScore")
					.WithData("ComponentCode", component.Code).WithData("Score", score);
			var percentage = decimal.Round(score / component.MaximumMarks * 100, 4);
			var contribution = decimal.Round(percentage * component.WeightPercentage / 100, 4);
			var passedMinimum = !component.MinimumMarks.HasValue || score >= component.MinimumMarks.Value;
			if (component.IsMandatory && !passedMinimum) reasons.Add($"{component.Name} did not meet its mandatory minimum.");
			results.Add(new AdmissionCrmExamComponentResultPreviewDto
			{
				ComponentCode = component.Code, Score = score, MaximumMarks = component.MaximumMarks,
				Percentage = percentage, WeightedContribution = contribution, PassedMinimum = passedMinimum
			});
		}
		var weightedPercentage = decimal.Round(results.Sum(x => x.WeightedContribution), 2);
		var mandatoryPassed = results.All(result =>
		{
			var component = definition.Components.First(x => CodeComparer.Equals(x.Code, result.ComponentCode));
			return !component.IsMandatory || result.PassedMinimum;
		});
		var passed = weightedPercentage >= definition.ResultRule.AggregatePassingPercentage &&
		             (!definition.ResultRule.RequireMandatoryComponentPass || mandatoryPassed);
		if (weightedPercentage < definition.ResultRule.AggregatePassingPercentage)
			reasons.Add($"Weighted score {weightedPercentage:0.##}% is below the {definition.ResultRule.AggregatePassingPercentage:0.##}% threshold.");
		if (passed) reasons.Add("The configured pass criteria are satisfied.");
		return new AdmissionCrmExamResultPreviewDto
		{
			ExamDefinitionCode = definition.Code, WeightedPercentage = weightedPercentage, MandatoryComponentsPassed = mandatoryPassed,
			Passed = passed, Outcome = passed ? "Pass" : "Fail", DecisionReasons = reasons,
			TieBreakerCodes = definition.ResultRule.TieBreakerCodes, Components = results
		};
	}
}
