using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookSlotWorkflow(
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
	AdmissionCrmExamSlotManager slotManager,
	AdmissionCrmPlaybookAccess access)
	: ApplicationService, ITransientDependency
{
	private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;

	public async Task<AdmissionCrmSlotGenerationResultDto> GenerateAsync(Guid id, GenerateAdmissionCrmPlaybookSlotsDto input)
	{
		var playbook = await access.GetAsync(id, "GenerateSlotsAsync");
		await access.EnsureConfigurationAsync(playbook.AcademicYearId, "GenerateSlotsAsync");
		AdmissionCrmPlaybookAccess.EnsurePublished(playbook);
		var fromDate = input.FromDate.Date;
		var toDate = input.ToDate.Date;
		if (toDate < fromDate || (toDate - fromDate).TotalDays > 92)
			throw new BusinessException("AdmissionCrm:InvalidSlotGenerationDateRange");
		if (input.UtcOffsetMinutes is < -840 or > 840)
			throw new BusinessException("AdmissionCrm:InvalidSlotGenerationUtcOffset");
		var template = AdmissionCrmPlaybookMapper.Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson)
			.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, input.SlotTemplateCode))
			?? throw new BusinessException("AdmissionCrm:SlotTemplateNotFound").WithData("SlotTemplateCode", input.SlotTemplateCode);
		var effectiveFrom = template.ValidFrom.HasValue && template.ValidFrom.Value.Date > fromDate ? template.ValidFrom.Value.Date : fromDate;
		var effectiveTo = template.ValidTo.HasValue && template.ValidTo.Value.Date < toDate ? template.ValidTo.Value.Date : toDate;
		var allowedDays = template.DaysOfWeek.ToHashSet();
		var blackoutDates = template.BlackoutDates.Select(x => x.Date).ToHashSet();
		var offset = TimeSpan.FromMinutes(input.UtcOffsetMinutes);
		var existingQueryable = await slotRepository.GetQueryableAsync();
		var existingSlots = await AsyncExecuter.ToListAsync(existingQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId));
		var createdSlots = new List<AdmissionCrmExamSlot>();
		var results = new List<AdmissionCrmGeneratedSlotDto>();
		for (var date = effectiveFrom; date <= effectiveTo; date = date.AddDays(1))
		{
			if ((allowedDays.Count > 0 && !allowedDays.Contains(date.DayOfWeek)) || blackoutDates.Contains(date)) continue;
			var start = ToUtc(date, template.StartTime, offset);
			var end = ToUtc(date, template.EndTime, offset);
			var existing = existingSlots.FirstOrDefault(x => x.RoomNo == template.RoomNo && x.StartDate == start && x.EndDate == end);
			if (existing != null)
			{
				results.Add(ToDto(existing, template, false));
				continue;
			}
			var slot = await slotManager.CreateAsync(playbook.AcademicYearId, playbook.Id, template.Code,
				template.ExamDefinitionCode, $"{template.Name} - {date:yyyy-MM-dd}", template.Campus, template.RoomOrHall,
				start, end, template.Capacity, template.RoomNo);
			createdSlots.Add(slot); existingSlots.Add(slot); results.Add(ToDto(slot, template, true));
		}
		if (createdSlots.Count > 0) await slotRepository.InsertManyAsync(createdSlots, autoSave: true);
		return new AdmissionCrmSlotGenerationResultDto
		{
			CreatedCount = createdSlots.Count, ExistingCount = results.Count - createdSlots.Count, Slots = results
		};
	}

	public async Task<AdmissionCrmSlotRecommendationDto> RecommendAsync(Guid id, RecommendAdmissionCrmPlaybookSlotDto input)
	{
		var playbook = await access.GetAsync(id, "RecommendSlotAsync");
		AdmissionCrmPlaybookAccess.EnsurePublished(playbook);
		var rules = AdmissionCrmPlaybookMapper.Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson);
		var rule = rules.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, input.AssignmentRuleCode))
			?? throw new BusinessException("AdmissionCrm:SlotAssignmentRuleNotFound").WithData("AssignmentRuleCode", input.AssignmentRuleCode);
		if (rule.Strategy == AdmissionCrmSlotAssignmentStrategy.Manual)
			throw new BusinessException("AdmissionCrm:ManualAssignmentRuleCannotRecommendSlot");
		var templates = AdmissionCrmPlaybookMapper.Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson);
		var template = !string.IsNullOrWhiteSpace(rule.SlotTemplateCode)
			? templates.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, rule.SlotTemplateCode)) : null;
		if (!string.IsNullOrWhiteSpace(rule.SlotTemplateCode) && template == null)
			throw new BusinessException("AdmissionCrm:SlotTemplateNotFound").WithData("SlotTemplateCode", rule.SlotTemplateCode);
		var slotQueryable = await slotRepository.GetQueryableAsync();
		var scopedSlots = await AsyncExecuter.ToListAsync(slotQueryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId));
		var futureSlots = scopedSlots.Where(x => x.StartDate > DateTimeOffset.UtcNow).ToList();
		if (template != null)
			futureSlots = futureSlots.Where(x => x.SlotName != null && x.SlotName.StartsWith(template.Name + " - ", StringComparison.OrdinalIgnoreCase)).ToList();
		var slotIds = futureSlots.Select(x => x.Id).ToList();
		var studentQueryable = await studentAdditionalDetailRepository.GetQueryableAsync();
		var legacy = await AsyncExecuter.ToListAsync(studentQueryable.Where(x => x.EntranceExamSlotId.HasValue && slotIds.Contains(x.EntranceExamSlotId.Value)));
		var examQueryable = await examRepository.GetQueryableAsync();
		var crm = await AsyncExecuter.ToListAsync(examQueryable.Where(x => x.TenantId == CurrentTenant.Id && slotIds.Contains(x.EntranceExamSlotId)));
		var candidates = futureSlots.Select(slot =>
		{
			var scheduled = legacy.Count(x => x.EntranceExamSlotId == slot.Id) + crm.Count(x => x.EntranceExamSlotId == slot.Id);
			return new { Slot = slot, Scheduled = scheduled, Available = slot.StudentLimit - scheduled };
		}).Where(x => x.Available > 0).ToList();
		if (candidates.Count == 0) throw new BusinessException("AdmissionCrm:NoAvailableConfiguredExamSlot");
		var selected = rule.Strategy switch
		{
			AdmissionCrmSlotAssignmentStrategy.BalancedCapacity or AdmissionCrmSlotAssignmentStrategy.RoundRobin =>
				candidates.OrderBy(x => (decimal)x.Scheduled / x.Slot.StudentLimit).ThenBy(x => x.Slot.StartDate).First(),
			AdmissionCrmSlotAssignmentStrategy.PreferredDate when input.PreferredDate.HasValue =>
				candidates.OrderBy(x => Math.Abs((x.Slot.StartDate.Date - input.PreferredDate.Value.Date).TotalDays)).ThenBy(x => x.Slot.StartDate).First(),
			_ => candidates.OrderBy(x => x.Slot.StartDate).First()
		};
		return new AdmissionCrmSlotRecommendationDto
		{
			EntranceExamSlotId = selected.Slot.Id, SlotName = selected.Slot.SlotName ?? "Entrance exam slot",
			StartTime = selected.Slot.StartDate, EndTime = selected.Slot.EndDate, RoomNo = selected.Slot.RoomNo,
			Capacity = selected.Slot.StudentLimit, ScheduledCount = selected.Scheduled, AvailableSeats = selected.Available,
			Strategy = rule.Strategy, Explanation = $"Rule {rule.Code} selected this slot using {rule.Strategy} with {selected.Available} seats available."
		};
	}

	private static DateTimeOffset ToUtc(DateTime date, TimeSpan time, TimeSpan offset) =>
		new DateTimeOffset(DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified), offset).ToUniversalTime();
	private static AdmissionCrmGeneratedSlotDto ToDto(AdmissionCrmExamSlot slot, AdmissionCrmSlotTemplateDto template, bool created) => new()
	{
		SlotId = slot.Id, SlotTemplateCode = template.Code, ExamDefinitionCode = template.ExamDefinitionCode,
		SlotName = slot.SlotName ?? template.Name, StartTime = slot.StartDate, EndTime = slot.EndDate,
		Capacity = slot.StudentLimit, RoomNo = slot.RoomNo, WasCreated = created
	};
}
