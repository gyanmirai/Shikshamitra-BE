using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPlaybookAppService : ApplicationService, IAdmissionCrmPlaybookAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;
	private static readonly HashSet<string> SupportedTieBreakers = new(
		["SubmissionTime", "LeadScore", "InquiryDate"],
		CodeComparer);

	private readonly IRepository<AdmissionCrmPlaybook, Guid> _playbookRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<StudentAdditionalDetail, Guid> _studentAdditionalDetailRepository;
	private readonly AdmissionCrmExamSlotManager _slotManager;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmPlaybookAppService(
		IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
		AdmissionCrmExamSlotManager slotManager,
		StudentScopeAccessService studentScopeAccessService)
	{
		_playbookRepository = playbookRepository;
		_slotRepository = slotRepository;
		_examRepository = examRepository;
		_studentAdditionalDetailRepository = studentAdditionalDetailRepository;
		_slotManager = slotManager;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> CreateAsync(
		Guid academicYearId,
		CreateAdmissionCrmPlaybookDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateAsync));
		NormalizeAndValidate(input);

		var playbook = CreateAggregate(academicYearId, 1, input);
		await _playbookRepository.InsertAsync(playbook, autoSave: true);
		return ToDto(playbook);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmPlaybookDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmPlaybookListInput input)
	{
		await EnsureAcademicYearAccessAsync(academicYearId, nameof(GetListAsync));
		var queryable = await _playbookRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var filter = input.Filter.Trim();
			query = query.Where(x => x.Name.Contains(filter));
		}

		if (input.Status.HasValue)
		{
			query = query.Where(x => x.Status == input.Status.Value);
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query
			.OrderBy(x => x.Name)
			.ThenByDescending(x => x.Version)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCrmPlaybookDto>(
			totalCount,
			items.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmPlaybookDto> GetAsync(Guid id)
	{
		return ToDto(await GetForCurrentTenantAsync(id, nameof(GetAsync)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> UpdateAsync(
		Guid id,
		UpdateAdmissionCrmPlaybookDto input)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(UpdateAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			playbook.AcademicYearId,
			nameof(UpdateAsync));
		NormalizeAndValidate(input);
		ApplyUpdate(playbook, input);
		await _playbookRepository.UpdateAsync(playbook, autoSave: true);
		return ToDto(playbook);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> PublishAsync(Guid id)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(PublishAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			playbook.AcademicYearId,
			nameof(PublishAsync));
		var validationInput = ToUpsertDto(playbook);
		NormalizeAndValidate(validationInput);
		await EnsureNoPublishedConflictAsync(playbook, validationInput.Applicability);
		playbook.Publish(Clock.Now);
		await _playbookRepository.UpdateAsync(playbook, autoSave: true);
		return ToDto(playbook);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> CreateNewVersionAsync(Guid id)
	{
		var source = await GetForCurrentTenantAsync(id, nameof(CreateNewVersionAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			source.AcademicYearId,
			nameof(CreateNewVersionAsync));
		if (source.Status == AdmissionCrmPlaybookStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:DraftPlaybookAlreadyEditable")
				.WithData("PlaybookId", source.Id);
		}

		var queryable = await _playbookRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == source.AcademicYearId &&
			x.Name == source.Name));
		var nextVersion = versions.Count == 0 ? source.Version + 1 : versions.Max(x => x.Version) + 1;
		var copy = new AdmissionCrmPlaybook(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			source.Name,
			nextVersion,
			source.IsFallback,
			source.RegistrationPath,
			source.ApplicabilityJson,
			source.StagesJson,
			source.RoundsJson,
			source.SeatPlansJson,
			source.ExamDefinitionsJson,
			source.SlotTemplatesJson,
			source.AssignmentRulesJson,
			source.Notes);

		await _playbookRepository.InsertAsync(copy, autoSave: true);
		return ToDto(copy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishPlaybooks)]
	public async Task<AdmissionCrmPlaybookDto> RetireAsync(Guid id)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(RetireAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			playbook.AcademicYearId,
			nameof(RetireAsync));
		playbook.Retire(Clock.Now);
		await _playbookRepository.UpdateAsync(playbook, autoSave: true);
		return ToDto(playbook);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmPlaybookResolutionDto> ResolveAsync(
		Guid academicYearId,
		ResolveAdmissionCrmPlaybookDto input)
	{
		await EnsureAcademicYearAccessAsync(academicYearId, nameof(ResolveAsync));
		NormalizeApplicability(input);
		var queryable = await _playbookRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.Status == AdmissionCrmPlaybookStatus.Published));

		var matches = published
			.Select(x => new
			{
				Playbook = x,
				Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(x.ApplicabilityJson)
			})
			.Where(x => Matches(x.Applicability, input))
			.Select(x => new
			{
				x.Playbook,
				Score = x.Playbook.IsFallback ? 0 : GetSpecificity(x.Applicability)
			})
			.OrderByDescending(x => x.Score)
			.ThenByDescending(x => x.Playbook.PublishedAt)
			.ToList();

		if (matches.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:NoApplicablePlaybook")
				.WithData("AcademicYearId", academicYearId);
		}

		if (matches.Count > 1 && matches[0].Score == matches[1].Score)
		{
			throw new BusinessException("AdmissionCrm:AmbiguousPlaybookResolution")
				.WithData("AcademicYearId", academicYearId)
				.WithData("SpecificityScore", matches[0].Score);
		}

		return new AdmissionCrmPlaybookResolutionDto
		{
			Playbook = ToDto(matches[0].Playbook),
			SpecificityScore = matches[0].Score,
			Explanation = matches[0].Playbook.IsFallback
				? "Resolved to the published fallback playbook."
				: $"Resolved to the most specific published playbook using {matches[0].Score} matching dimensions."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public async Task<AdmissionCrmSlotGenerationResultDto> GenerateSlotsAsync(
		Guid id,
		GenerateAdmissionCrmPlaybookSlotsDto input)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(GenerateSlotsAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			playbook.AcademicYearId,
			nameof(GenerateSlotsAsync));
		EnsurePublished(playbook);
		var fromDate = input.FromDate.Date;
		var toDate = input.ToDate.Date;
		if (toDate < fromDate || (toDate - fromDate).TotalDays > 92)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlotGenerationDateRange");
		}

		if (input.UtcOffsetMinutes is < -840 or > 840)
		{
			throw new BusinessException("AdmissionCrm:InvalidSlotGenerationUtcOffset");
		}

		var template = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson)
			.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, input.SlotTemplateCode))
			?? throw new BusinessException("AdmissionCrm:SlotTemplateNotFound")
				.WithData("SlotTemplateCode", input.SlotTemplateCode);
		var effectiveFrom = template.ValidFrom.HasValue && template.ValidFrom.Value.Date > fromDate
			? template.ValidFrom.Value.Date
			: fromDate;
		var effectiveTo = template.ValidTo.HasValue && template.ValidTo.Value.Date < toDate
			? template.ValidTo.Value.Date
			: toDate;
		var allowedDays = template.DaysOfWeek.ToHashSet();
		var blackoutDates = template.BlackoutDates.Select(x => x.Date).ToHashSet();
		var offset = TimeSpan.FromMinutes(input.UtcOffsetMinutes);
		var existingQueryable = await _slotRepository.GetQueryableAsync();
		var existingSlots = await AsyncExecuter.ToListAsync(existingQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId));
		var createdSlots = new List<AdmissionCrmExamSlot>();
		var results = new List<AdmissionCrmGeneratedSlotDto>();

		for (var date = effectiveFrom; date <= effectiveTo; date = date.AddDays(1))
		{
			if ((allowedDays.Count > 0 && !allowedDays.Contains(date.DayOfWeek)) || blackoutDates.Contains(date))
			{
				continue;
			}

			var start = ToUtcDateTimeOffset(date, template.StartTime, offset);
			var end = ToUtcDateTimeOffset(date, template.EndTime, offset);
			var existing = existingSlots.FirstOrDefault(x =>
				x.RoomNo == template.RoomNo &&
				x.StartDate == start &&
				x.EndDate == end);
			if (existing != null)
			{
				results.Add(ToGeneratedSlotDto(existing, template, wasCreated: false));
				continue;
			}

			var slot = await _slotManager.CreateAsync(
				playbook.AcademicYearId,
				playbook.Id,
				template.Code,
				template.ExamDefinitionCode,
				$"{template.Name} - {date:yyyy-MM-dd}",
				template.Campus,
				template.RoomOrHall,
				start,
				end,
				template.Capacity,
				template.RoomNo);
			createdSlots.Add(slot);
			existingSlots.Add(slot);
			results.Add(ToGeneratedSlotDto(slot, template, wasCreated: true));
		}

		if (createdSlots.Count > 0)
		{
			await _slotRepository.InsertManyAsync(createdSlots, autoSave: true);
		}

		return new AdmissionCrmSlotGenerationResultDto
		{
			CreatedCount = createdSlots.Count,
			ExistingCount = results.Count - createdSlots.Count,
			Slots = results
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmSlotRecommendationDto> RecommendSlotAsync(
		Guid id,
		RecommendAdmissionCrmPlaybookSlotDto input)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(RecommendSlotAsync));
		EnsurePublished(playbook);
		var rules = Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson);
		var rule = rules.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, input.AssignmentRuleCode))
		           ?? throw new BusinessException("AdmissionCrm:SlotAssignmentRuleNotFound")
			           .WithData("AssignmentRuleCode", input.AssignmentRuleCode);
		if (rule.Strategy == AdmissionCrmSlotAssignmentStrategy.Manual)
		{
			throw new BusinessException("AdmissionCrm:ManualAssignmentRuleCannotRecommendSlot");
		}

		var templates = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson);
		var template = !string.IsNullOrWhiteSpace(rule.SlotTemplateCode)
			? templates.FirstOrDefault(x => x.IsActive && CodeComparer.Equals(x.Code, rule.SlotTemplateCode))
			: null;
		if (!string.IsNullOrWhiteSpace(rule.SlotTemplateCode) && template == null)
		{
			throw new BusinessException("AdmissionCrm:SlotTemplateNotFound")
				.WithData("SlotTemplateCode", rule.SlotTemplateCode);
		}

		var slotQueryable = await _slotRepository.GetQueryableAsync();
		var scopedSlots = await AsyncExecuter.ToListAsync(slotQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId));
		var now = DateTimeOffset.UtcNow;
		var futureSlots = scopedSlots.Where(x => x.StartDate > now).ToList();
		if (template != null)
		{
			futureSlots = futureSlots
				.Where(x => x.SlotName != null && x.SlotName.StartsWith(template.Name + " - ", StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		var slotIds = futureSlots.Select(x => x.Id).ToList();
		var studentQueryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		var legacyAssignments = await AsyncExecuter.ToListAsync(studentQueryable.Where(x =>
			x.EntranceExamSlotId.HasValue && slotIds.Contains(x.EntranceExamSlotId.Value)));
		var examQueryable = await _examRepository.GetQueryableAsync();
		var crmAssignments = await AsyncExecuter.ToListAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && slotIds.Contains(x.EntranceExamSlotId)));
		var candidates = futureSlots
			.Select(slot =>
			{
				var scheduled = legacyAssignments.Count(x => x.EntranceExamSlotId == slot.Id) +
				                crmAssignments.Count(x => x.EntranceExamSlotId == slot.Id);
				return new { Slot = slot, Scheduled = scheduled, Available = slot.StudentLimit - scheduled };
			})
			.Where(x => x.Available > 0)
			.ToList();
		if (candidates.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:NoAvailableConfiguredExamSlot");
		}

		var selected = rule.Strategy switch
		{
			AdmissionCrmSlotAssignmentStrategy.BalancedCapacity or
			AdmissionCrmSlotAssignmentStrategy.RoundRobin => candidates
				.OrderBy(x => (decimal)x.Scheduled / x.Slot.StudentLimit)
				.ThenBy(x => x.Slot.StartDate)
				.First(),
			AdmissionCrmSlotAssignmentStrategy.PreferredDate when input.PreferredDate.HasValue => candidates
				.OrderBy(x => Math.Abs((x.Slot.StartDate.Date - input.PreferredDate.Value.Date).TotalDays))
				.ThenBy(x => x.Slot.StartDate)
				.First(),
			_ => candidates.OrderBy(x => x.Slot.StartDate).First()
		};

		return new AdmissionCrmSlotRecommendationDto
		{
			EntranceExamSlotId = selected.Slot.Id,
			SlotName = selected.Slot.SlotName ?? "Entrance exam slot",
			StartTime = selected.Slot.StartDate,
			EndTime = selected.Slot.EndDate,
			RoomNo = selected.Slot.RoomNo,
			Capacity = selected.Slot.StudentLimit,
			ScheduledCount = selected.Scheduled,
			AvailableSeats = selected.Available,
			Strategy = rule.Strategy,
			Explanation = $"Rule {rule.Code} selected this slot using {rule.Strategy} with {selected.Available} seats available."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmExamResultPreviewDto> PreviewExamResultAsync(
		Guid id,
		PreviewAdmissionCrmExamResultDto input)
	{
		var playbook = await GetForCurrentTenantAsync(id, nameof(PreviewExamResultAsync));
		var definition = Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson)
			.FirstOrDefault(x => CodeComparer.Equals(x.Code, input.ExamDefinitionCode))
			?? throw new BusinessException("AdmissionCrm:ExamDefinitionNotFound")
				.WithData("ExamDefinitionCode", input.ExamDefinitionCode);
		var scores = new Dictionary<string, decimal>(input.ComponentScores, CodeComparer);
		var results = new List<AdmissionCrmExamComponentResultPreviewDto>();
		var reasons = new List<string>();
		foreach (var component in definition.Components)
		{
			if (!scores.TryGetValue(component.Code, out var score))
			{
				if (component.IsMandatory)
				{
					throw new BusinessException("AdmissionCrm:MandatoryExamComponentScoreRequired")
						.WithData("ComponentCode", component.Code);
				}
				score = 0;
			}

			if (score < 0 || score > component.MaximumMarks)
			{
				throw new BusinessException("AdmissionCrm:InvalidExamComponentScore")
					.WithData("ComponentCode", component.Code)
					.WithData("Score", score);
			}

			var percentage = decimal.Round(score / component.MaximumMarks * 100, 4);
			var contribution = decimal.Round(percentage * component.WeightPercentage / 100, 4);
			var passedMinimum = !component.MinimumMarks.HasValue || score >= component.MinimumMarks.Value;
			if (component.IsMandatory && !passedMinimum)
			{
				reasons.Add($"{component.Name} did not meet its mandatory minimum.");
			}

			results.Add(new AdmissionCrmExamComponentResultPreviewDto
			{
				ComponentCode = component.Code,
				Score = score,
				MaximumMarks = component.MaximumMarks,
				Percentage = percentage,
				WeightedContribution = contribution,
				PassedMinimum = passedMinimum
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
		{
			reasons.Add($"Weighted score {weightedPercentage:0.##}% is below the {definition.ResultRule.AggregatePassingPercentage:0.##}% threshold.");
		}
		if (passed)
		{
			reasons.Add("The configured pass criteria are satisfied.");
		}

		return new AdmissionCrmExamResultPreviewDto
		{
			ExamDefinitionCode = definition.Code,
			WeightedPercentage = weightedPercentage,
			MandatoryComponentsPassed = mandatoryPassed,
			Passed = passed,
			Outcome = passed ? "Pass" : "Fail",
			DecisionReasons = reasons,
			TieBreakerCodes = definition.ResultRule.TieBreakerCodes,
			Components = results
		};
	}

	private AdmissionCrmPlaybook CreateAggregate(
		Guid academicYearId,
		int version,
		AdmissionCrmPlaybookUpsertDto input)
	{
		return new AdmissionCrmPlaybook(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.Name,
			version,
			input.IsFallback,
			input.RegistrationPath,
			Serialize(input.Applicability),
			Serialize(input.Stages),
			Serialize(input.Rounds),
			Serialize(input.SeatPlans),
			Serialize(input.ExamDefinitions),
			Serialize(input.SlotTemplates),
			Serialize(input.AssignmentRules),
			input.Notes);
	}

	private static void ApplyUpdate(AdmissionCrmPlaybook playbook, AdmissionCrmPlaybookUpsertDto input)
	{
		playbook.Update(
			input.Name,
			input.IsFallback,
			input.RegistrationPath,
			Serialize(input.Applicability),
			Serialize(input.Stages),
			Serialize(input.Rounds),
			Serialize(input.SeatPlans),
			Serialize(input.ExamDefinitions),
			Serialize(input.SlotTemplates),
			Serialize(input.AssignmentRules),
			input.Notes);
	}

	private async Task<AdmissionCrmPlaybook> GetForCurrentTenantAsync(Guid id, string operation)
	{
		var playbook = await _playbookRepository.GetAsync(id);
		if (playbook.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:PlaybookNotFound")
				.WithData("PlaybookId", id);
		}

		await EnsureAcademicYearAccessAsync(playbook.AcademicYearId, operation);
		return playbook;
	}

	private async Task EnsureAcademicYearAccessAsync(Guid academicYearId, string operation)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
	}

	private async Task EnsureNoPublishedConflictAsync(
		AdmissionCrmPlaybook playbook,
		AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		var queryable = await _playbookRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId &&
			x.Status == AdmissionCrmPlaybookStatus.Published &&
			x.Id != playbook.Id));
		var specificity = playbook.IsFallback ? 0 : GetSpecificity(applicability);

		foreach (var existing in published)
		{
			var existingApplicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(existing.ApplicabilityJson);
			var existingSpecificity = existing.IsFallback ? 0 : GetSpecificity(existingApplicability);
			if (specificity == existingSpecificity && Overlaps(applicability, existingApplicability))
			{
				throw new BusinessException("AdmissionCrm:ConflictingPublishedPlaybook")
					.WithData("PlaybookId", playbook.Id)
					.WithData("ConflictingPlaybookId", existing.Id);
			}
		}
	}

	private static void NormalizeAndValidate(AdmissionCrmPlaybookUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(
			input.Name,
			nameof(input.Name),
			AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		NormalizeApplicability(input.Applicability);
		if (input.IsFallback && GetSpecificity(input.Applicability) != 0)
		{
			throw new BusinessException("AdmissionCrm:FallbackPlaybookCannotBeScoped");
		}

		if (!input.IsFallback && GetSpecificity(input.Applicability) == 0)
		{
			throw new BusinessException("AdmissionCrm:ScopedPlaybookApplicabilityRequired");
		}

		ValidateStages(input.Stages);
		ValidateRounds(input.Rounds);
		ValidateSeatPlans(input.SeatPlans, input.Rounds);
		ValidateExamDefinitions(input.ExamDefinitions, input.Rounds);
		ValidateSlotTemplates(input.SlotTemplates, input.ExamDefinitions);
		ValidateAssignmentRules(input.AssignmentRules, input.SlotTemplates);
	}

	private static void ValidateStages(IReadOnlyList<AdmissionCrmPlaybookStageDto> stages)
	{
		if (stages.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:PlaybookStagesRequired");
		}

		EnsureUnique(stages.Select(x => x.Code), "AdmissionCrm:DuplicatePlaybookStageCode");
		EnsureUnique(stages.Select(x => x.Order.ToString()), "AdmissionCrm:DuplicatePlaybookStageOrder");
		var codes = stages.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var stage in stages)
		{
			ValidateCodeAndName(stage.Code, stage.DisplayName);
			if (stage.Order < 0 || stage.SlaHours is <= 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidPlaybookStage").WithData("StageCode", stage.Code);
			}

			if (stage.AllowedNextStageCodes.Any(x => !codes.Contains(x)))
			{
				throw new BusinessException("AdmissionCrm:UnknownNextPlaybookStage").WithData("StageCode", stage.Code);
			}
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
			if (visiting.Contains(code))
			{
				throw new BusinessException("AdmissionCrm:CircularPlaybookStages").WithData("StageCode", code);
			}

			if (!visited.Add(code))
			{
				return;
			}

			visiting.Add(code);
			foreach (var next in byCode[code].AllowedNextStageCodes)
			{
				Visit(next);
			}
			visiting.Remove(code);
		}

		foreach (var stage in stages)
		{
			Visit(stage.Code);
		}

		var reachable = new HashSet<string>(CodeComparer);
		var queue = new Queue<string>();
		queue.Enqueue(stages.OrderBy(x => x.Order).First().Code);
		while (queue.TryDequeue(out var code))
		{
			if (!reachable.Add(code))
			{
				continue;
			}

			foreach (var next in byCode[code].AllowedNextStageCodes)
			{
				queue.Enqueue(next);
			}
		}

		if (reachable.Count != stages.Count)
		{
			throw new BusinessException("AdmissionCrm:UnreachablePlaybookStage");
		}
	}

	private static void ValidateRounds(IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		EnsureUnique(rounds.Select(x => x.Code), "AdmissionCrm:DuplicateAdmissionRoundCode");
		foreach (var round in rounds)
		{
			ValidateCodeAndName(round.Code, round.Name);
			if (round.ClosingDate < round.OpeningDate ||
			    (round.ExamWindowStart.HasValue != round.ExamWindowEnd.HasValue) ||
			    (round.ExamWindowStart.HasValue && round.ExamWindowEnd < round.ExamWindowStart) ||
			    (round.RegistrationDeadline.HasValue && round.RegistrationDeadline < round.OpeningDate) ||
			    (round.DecisionDeadline.HasValue && round.DecisionDeadline < round.OpeningDate))
			{
				throw new BusinessException("AdmissionCrm:InvalidAdmissionRoundDates").WithData("RoundCode", round.Code);
			}
		}
	}

	private static void ValidateSeatPlans(
		IReadOnlyList<AdmissionCrmSeatPlanDto> seatPlans,
		IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		EnsureUnique(seatPlans.Select(x => x.Code), "AdmissionCrm:DuplicateSeatPlanCode");
		var roundCodes = rounds.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var plan in seatPlans)
		{
			ValidateCodeAndName(plan.Code, plan.Code);
			if (!string.IsNullOrWhiteSpace(plan.RoundCode) && !roundCodes.Contains(plan.RoundCode))
			{
				throw new BusinessException("AdmissionCrm:SeatPlanRoundNotFound").WithData("SeatPlanCode", plan.Code);
			}

			if (new[]
			    {
				    plan.PlannedSeats,
				    plan.ApprovedCapacity,
				    plan.OfferedSeats,
				    plan.AcceptedSeats,
				    plan.WaitlistedApplicants,
				    plan.WithdrawnApplicants
			    }.Any(x => x < 0))
			{
				throw new BusinessException("AdmissionCrm:InvalidSeatPlanCounts").WithData("SeatPlanCode", plan.Code);
			}
		}
	}

	private static void ValidateExamDefinitions(
		IReadOnlyList<AdmissionCrmExamDefinitionDto> definitions,
		IReadOnlyList<AdmissionCrmAdmissionRoundDto> rounds)
	{
		EnsureUnique(definitions.Select(x => x.Code), "AdmissionCrm:DuplicateExamDefinitionCode");
		var roundCodes = rounds.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var definition in definitions)
		{
			ValidateCodeAndName(definition.Code, definition.Name);
			if (definition.DurationMinutes <= 0 || definition.Components.Count == 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidExamDefinition").WithData("ExamCode", definition.Code);
			}

			if (!string.IsNullOrWhiteSpace(definition.RoundCode) && !roundCodes.Contains(definition.RoundCode))
			{
				throw new BusinessException("AdmissionCrm:ExamRoundNotFound").WithData("ExamCode", definition.Code);
			}

			EnsureUnique(definition.Components.Select(x => x.Code), "AdmissionCrm:DuplicateExamComponentCode");
			foreach (var component in definition.Components)
			{
				ValidateCodeAndName(component.Code, component.Name);
				if (component.MaximumMarks <= 0 ||
				    component.MinimumMarks < 0 ||
				    component.MinimumMarks > component.MaximumMarks ||
				    component.WeightPercentage <= 0 ||
				    component.ScorePrecision is < 0 or > 4)
				{
					throw new BusinessException("AdmissionCrm:InvalidExamComponent").WithData("ComponentCode", component.Code);
				}
			}

			if (definition.Components.Sum(x => x.WeightPercentage) != 100)
			{
				throw new BusinessException("AdmissionCrm:ExamComponentWeightsMustTotalOneHundred")
					.WithData("ExamCode", definition.Code);
			}

			var rule = definition.ResultRule;
			if (rule.AggregatePassingPercentage is < 0 or > 100 ||
			    rule.MeritCapacity < 0 ||
			    rule.WaitlistCapacity < 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidExamResultRule").WithData("ExamCode", definition.Code);
			}

			var componentCodes = definition.Components.Select(x => x.Code).ToHashSet(CodeComparer);
			if (rule.TieBreakerCodes.Any(x => !componentCodes.Contains(x) && !SupportedTieBreakers.Contains(x)))
			{
				throw new BusinessException("AdmissionCrm:UnknownExamTieBreaker").WithData("ExamCode", definition.Code);
			}
		}
	}

	private static void ValidateSlotTemplates(
		IReadOnlyList<AdmissionCrmSlotTemplateDto> templates,
		IReadOnlyList<AdmissionCrmExamDefinitionDto> definitions)
	{
		EnsureUnique(templates.Select(x => x.Code), "AdmissionCrm:DuplicateSlotTemplateCode");
		var examCodes = definitions.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var template in templates)
		{
			ValidateCodeAndName(template.Code, template.Name);
			if (!examCodes.Contains(template.ExamDefinitionCode))
			{
				throw new BusinessException("AdmissionCrm:SlotTemplateExamNotFound").WithData("SlotTemplateCode", template.Code);
			}

			if (template.Capacity <= 0 ||
			    template.RoomNo <= 0 ||
			    template.ReportingTime > template.StartTime ||
			    template.StartTime >= template.EndTime ||
			    (template.ValidFrom.HasValue && template.ValidTo < template.ValidFrom))
			{
				throw new BusinessException("AdmissionCrm:InvalidSlotTemplate").WithData("SlotTemplateCode", template.Code);
			}
		}
	}

	private static void ValidateAssignmentRules(
		IReadOnlyList<AdmissionCrmSlotAssignmentRuleDto> rules,
		IReadOnlyList<AdmissionCrmSlotTemplateDto> templates)
	{
		EnsureUnique(rules.Select(x => x.Code), "AdmissionCrm:DuplicateSlotAssignmentRuleCode");
		EnsureUnique(rules.Select(x => x.Priority.ToString()), "AdmissionCrm:DuplicateSlotAssignmentRulePriority");
		var templateCodes = templates.Select(x => x.Code).ToHashSet(CodeComparer);
		foreach (var rule in rules)
		{
			ValidateCodeAndName(rule.Code, rule.Name);
			if (string.IsNullOrWhiteSpace(rule.Trigger) || rule.Priority < 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidSlotAssignmentRule").WithData("RuleCode", rule.Code);
			}

			if (!string.IsNullOrWhiteSpace(rule.SlotTemplateCode) && !templateCodes.Contains(rule.SlotTemplateCode))
			{
				throw new BusinessException("AdmissionCrm:SlotAssignmentTemplateNotFound").WithData("RuleCode", rule.Code);
			}
		}
	}

	private static void ValidateCodeAndName(string code, string name)
	{
		Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPlaybookCodeLength);
		Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxTemplateNameLength);
	}

	private static void EnsureUnique(IEnumerable<string> values, string errorCode)
	{
		var seen = new HashSet<string>(CodeComparer);
		foreach (var value in values)
		{
			if (string.IsNullOrWhiteSpace(value) || !seen.Add(value.Trim()))
			{
				throw new BusinessException(errorCode).WithData("Value", value);
			}
		}
	}

	private static void NormalizeApplicability(AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		applicability.Campus = Normalize(applicability.Campus);
		applicability.DesiredClass = Normalize(applicability.DesiredClass);
		applicability.Board = Normalize(applicability.Board);
		applicability.Medium = Normalize(applicability.Medium);
		applicability.ApplicantCategory = Normalize(applicability.ApplicantCategory);
		applicability.AdmissionRound = Normalize(applicability.AdmissionRound);
		applicability.AdmissionRoute = Normalize(applicability.AdmissionRoute);
	}

	private static string? Normalize(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private static int GetSpecificity(AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		return GetValues(applicability).Count(x => !string.IsNullOrWhiteSpace(x));
	}

	private static bool Matches(
		AdmissionCrmPlaybookApplicabilityDto configured,
		AdmissionCrmPlaybookApplicabilityDto requested)
	{
		var configuredValues = GetValues(configured);
		var requestedValues = GetValues(requested);
		return configuredValues.Zip(requestedValues).All(x =>
			string.IsNullOrWhiteSpace(x.First) || Same(x.First, x.Second));
	}

	private static bool Overlaps(
		AdmissionCrmPlaybookApplicabilityDto left,
		AdmissionCrmPlaybookApplicabilityDto right)
	{
		return GetValues(left).Zip(GetValues(right)).All(x =>
			string.IsNullOrWhiteSpace(x.First) ||
			string.IsNullOrWhiteSpace(x.Second) ||
			Same(x.First, x.Second));
	}

	private static IReadOnlyList<string?> GetValues(AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		return
		[
			applicability.Campus,
			applicability.DesiredClass,
			applicability.Board,
			applicability.Medium,
			applicability.ApplicantCategory,
			applicability.AdmissionRound,
			applicability.AdmissionRoute
		];
	}

	private static bool Same(string? left, string? right)
	{
		return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static void EnsurePublished(AdmissionCrmPlaybook playbook)
	{
		if (playbook.Status != AdmissionCrmPlaybookStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:PublishedPlaybookRequired")
				.WithData("PlaybookId", playbook.Id);
		}
	}

	private static DateTimeOffset ToUtcDateTimeOffset(DateTime date, TimeSpan time, TimeSpan offset)
	{
		var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
		return new DateTimeOffset(local, offset).ToUniversalTime();
	}

	private static AdmissionCrmGeneratedSlotDto ToGeneratedSlotDto(
		AdmissionCrmExamSlot slot,
		AdmissionCrmSlotTemplateDto template,
		bool wasCreated)
	{
		return new AdmissionCrmGeneratedSlotDto
		{
			SlotId = slot.Id,
			SlotTemplateCode = template.Code,
			ExamDefinitionCode = template.ExamDefinitionCode,
			SlotName = slot.SlotName ?? template.Name,
			StartTime = slot.StartDate,
			EndTime = slot.EndDate,
			Capacity = slot.StudentLimit,
			RoomNo = slot.RoomNo,
			WasCreated = wasCreated
		};
	}

	private static AdmissionCrmPlaybookDto ToDto(AdmissionCrmPlaybook playbook)
	{
		var seatPlans = Deserialize<List<AdmissionCrmSeatPlanDto>>(playbook.SeatPlansJson);
		foreach (var seatPlan in seatPlans)
		{
			seatPlan.RemainingCapacity = Math.Max(0, seatPlan.ApprovedCapacity - seatPlan.AcceptedSeats);
			seatPlan.IsOverAllocated = seatPlan.OfferedSeats > seatPlan.ApprovedCapacity ||
			                           seatPlan.AcceptedSeats > seatPlan.ApprovedCapacity;
		}

		return new AdmissionCrmPlaybookDto
		{
			Id = playbook.Id,
			AcademicYearId = playbook.AcademicYearId,
			Name = playbook.Name,
			Version = playbook.Version,
			Status = playbook.Status,
			IsFallback = playbook.IsFallback,
			RegistrationPath = playbook.RegistrationPath,
			Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(playbook.ApplicabilityJson),
			Stages = Deserialize<List<AdmissionCrmPlaybookStageDto>>(playbook.StagesJson),
			Rounds = Deserialize<List<AdmissionCrmAdmissionRoundDto>>(playbook.RoundsJson),
			SeatPlans = seatPlans,
			ExamDefinitions = Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson),
			SlotTemplates = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson),
			AssignmentRules = Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson),
			Notes = playbook.Notes,
			PublishedAt = playbook.PublishedAt,
			RetiredAt = playbook.RetiredAt
		};
	}

	private static UpdateAdmissionCrmPlaybookDto ToUpsertDto(AdmissionCrmPlaybook playbook)
	{
		return new UpdateAdmissionCrmPlaybookDto
		{
			Name = playbook.Name,
			IsFallback = playbook.IsFallback,
			RegistrationPath = playbook.RegistrationPath,
			Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(playbook.ApplicabilityJson),
			Stages = Deserialize<List<AdmissionCrmPlaybookStageDto>>(playbook.StagesJson),
			Rounds = Deserialize<List<AdmissionCrmAdmissionRoundDto>>(playbook.RoundsJson),
			SeatPlans = Deserialize<List<AdmissionCrmSeatPlanDto>>(playbook.SeatPlansJson),
			ExamDefinitions = Deserialize<List<AdmissionCrmExamDefinitionDto>>(playbook.ExamDefinitionsJson),
			SlotTemplates = Deserialize<List<AdmissionCrmSlotTemplateDto>>(playbook.SlotTemplatesJson),
			AssignmentRules = Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(playbook.AssignmentRulesJson),
			Notes = playbook.Notes
		};
	}

	private static string Serialize<T>(T value)
	{
		return JsonSerializer.Serialize(value, JsonOptions);
	}

	private static T Deserialize<T>(string json)
	{
		return JsonSerializer.Deserialize<T>(json, JsonOptions)
		       ?? throw new BusinessException("AdmissionCrm:InvalidStoredPlaybookConfiguration");
	}
}
