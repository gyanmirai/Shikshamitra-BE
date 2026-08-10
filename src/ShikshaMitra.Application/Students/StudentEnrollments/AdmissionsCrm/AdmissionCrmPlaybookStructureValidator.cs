using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookStructureValidator
{
	private static readonly StringComparer CodeComparer = AdmissionCrmPlaybookValidationRules.CodeComparer;

	public static void Validate(IReadOnlyList<AdmissionCrmPlaybookStageDto> stages,
		IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds, IReadOnlyList<AdmissionCrmSeatPlanDto> seatPlans)
	{
		ValidateStages(stages);
		ValidateRounds(rounds);
		ValidateSeatPlans(seatPlans, rounds);
	}

	private static void ValidateStages(IReadOnlyList<AdmissionCrmPlaybookStageDto> stages)
	{
		if (stages.Count == 0) throw new BusinessException("AdmissionCrm:PlaybookStagesRequired");
		AdmissionCrmPlaybookValidationRules.Unique(stages.Select(x => x.Code), "AdmissionCrm:DuplicatePlaybookStageCode");
		AdmissionCrmPlaybookValidationRules.Unique(stages.Select(x => x.Order.ToString()), "AdmissionCrm:DuplicatePlaybookStageOrder");
		var codes = stages.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var stage in stages)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(stage.Code, stage.DisplayName);
			if (stage.Order < 0 || stage.SlaHours is <= 0)
				throw new BusinessException("AdmissionCrm:InvalidPlaybookStage").WithData("StageCode", stage.Code);
			if (stage.AllowedNextStageCodes.Any(x => !codes.Contains(x)))
				throw new BusinessException("AdmissionCrm:UnknownNextPlaybookStage").WithData("StageCode", stage.Code);
		}
		EnsureAcyclicAndReachable(stages);
	}

	private static void EnsureAcyclicAndReachable(IReadOnlyList<AdmissionCrmPlaybookStageDto> stages)
	{
		var byCode = stages.ToDictionary(x => x.Code, CodeComparer);
		var visiting = new HashSet<string>(CodeComparer);
		var visited = new HashSet<string>(CodeComparer);
		void Visit(string code)
		{
			if (visiting.Contains(code)) throw new BusinessException("AdmissionCrm:CircularPlaybookStages").WithData("StageCode", code);
			if (!visited.Add(code)) return;
			visiting.Add(code);
			foreach (var next in byCode[code].AllowedNextStageCodes) Visit(next);
			visiting.Remove(code);
		}
		foreach (var stage in stages) Visit(stage.Code);
		var reachable = new HashSet<string>(CodeComparer);
		var queue = new Queue<string>();
		queue.Enqueue(stages.OrderBy(x => x.Order).First().Code);
		while (queue.TryDequeue(out var code))
		{
			if (!reachable.Add(code)) continue;
			foreach (var next in byCode[code].AllowedNextStageCodes) queue.Enqueue(next);
		}
		if (reachable.Count != stages.Count) throw new BusinessException("AdmissionCrm:UnreachablePlaybookStage");
	}

	private static void ValidateRounds(IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		AdmissionCrmPlaybookValidationRules.Unique(rounds.Select(x => x.Code), "AdmissionCrm:DuplicateAdmissionRoundCode");
		foreach (var round in rounds)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(round.Code, round.Name);
			if (round.ClosingDate < round.OpeningDate || (round.ExamWindowStart.HasValue != round.ExamWindowEnd.HasValue) ||
			    (round.ExamWindowStart.HasValue && round.ExamWindowEnd < round.ExamWindowStart) ||
			    (round.RegistrationDeadline.HasValue && round.RegistrationDeadline < round.OpeningDate) ||
			    (round.DecisionDeadline.HasValue && round.DecisionDeadline < round.OpeningDate))
				throw new BusinessException("AdmissionCrm:InvalidAdmissionRoundDates").WithData("RoundCode", round.Code);
		}
	}

	private static void ValidateSeatPlans(IReadOnlyList<AdmissionCrmSeatPlanDto> seatPlans, IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		AdmissionCrmPlaybookValidationRules.Unique(seatPlans.Select(x => x.Code), "AdmissionCrm:DuplicateSeatPlanCode");
		var roundCodes = rounds.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var plan in seatPlans)
		{
			AdmissionCrmPlaybookValidationRules.CodeAndName(plan.Code, plan.Code);
			if (!string.IsNullOrWhiteSpace(plan.RoundCode) && !roundCodes.Contains(plan.RoundCode))
				throw new BusinessException("AdmissionCrm:SeatPlanRoundNotFound").WithData("SeatPlanCode", plan.Code);
			if (new[] { plan.PlannedSeats, plan.ApprovedCapacity, plan.OfferedSeats, plan.AcceptedSeats,
				plan.WaitlistedApplicants, plan.WithdrawnApplicants }.Any(x => x < 0))
				throw new BusinessException("AdmissionCrm:InvalidSeatPlanCounts").WithData("SeatPlanCode", plan.Code);
		}
	}
}
