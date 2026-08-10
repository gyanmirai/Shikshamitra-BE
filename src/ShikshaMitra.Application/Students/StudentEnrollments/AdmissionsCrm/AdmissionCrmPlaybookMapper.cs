using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionCrmPlaybookDto ToDto(AdmissionCrmPlaybook playbook)
	{
		var seatPlans = Deserialize<List<AdmissionCrmSeatPlanDto>>(playbook.SeatPlansJson);
		foreach (var seatPlan in seatPlans)
		{
			seatPlan.RemainingCapacity = Math.Max(0, seatPlan.ApprovedCapacity - seatPlan.AcceptedSeats);
			seatPlan.IsOverAllocated = seatPlan.OfferedSeats > seatPlan.ApprovedCapacity || seatPlan.AcceptedSeats > seatPlan.ApprovedCapacity;
		}
		return new AdmissionCrmPlaybookDto
		{
			Id = playbook.Id, AcademicYearId = playbook.AcademicYearId, Name = playbook.Name, Version = playbook.Version,
			Status = playbook.Status, IsFallback = playbook.IsFallback, RegistrationPath = playbook.RegistrationPath,
			Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(playbook.ApplicabilityJson),
			Stages = Deserialize<List<AdmissionCrmPlaybookStageDto>>(playbook.StagesJson),
			Rounds = Deserialize<List<AdmissionCrmAdmissionRoundDto>>(playbook.RoundsJson), SeatPlans = seatPlans,
			ExamDefinitions = Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson),
			SlotTemplates = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson),
			AssignmentRules = Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson),
			Notes = playbook.Notes, PublishedAt = playbook.PublishedAt, RetiredAt = playbook.RetiredAt
		};
	}

	public static UpdateAdmissionCrmPlaybookDto ToUpsert(AdmissionCrmPlaybook playbook) => new()
	{
		Name = playbook.Name, IsFallback = playbook.IsFallback, RegistrationPath = playbook.RegistrationPath,
		Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(playbook.ApplicabilityJson),
		Stages = Deserialize<List<AdmissionCrmPlaybookStageDto>>(playbook.StagesJson),
		Rounds = Deserialize<List<AdmissionCrmAdmissionRoundDto>>(playbook.RoundsJson),
		SeatPlans = Deserialize<List<AdmissionCrmSeatPlanDto>>(playbook.SeatPlansJson),
		ExamDefinitions = Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson),
		SlotTemplates = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson),
		AssignmentRules = Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson), Notes = playbook.Notes
	};

	public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
		?? throw new BusinessException("AdmissionCrm:InvalidStoredPlaybookConfiguration");
}
