using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicExamWorkflow(
	AdmissionCrmPublicAccess access,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	AdmissionCrmEntranceExamSchedulingWorkflow scheduling) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPublicExamSlotListDto> GetAvailableSlotsAsync(
		Guid tenantId,
		string token,
		string accessSessionToken)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, accessSessionToken);
			if (!await IsSelfSchedulingEnabledAsync(lead))
			{
				return new AdmissionCrmPublicExamSlotListDto { SelfSchedulingEnabled = false };
			}

			var slots = await AsyncExecuter.ToListAsync((await slotRepository.GetQueryableAsync()).Where(slot =>
				slot.TenantId == tenantId && slot.AcademicYearId == lead.AcademicYearId));
			var exams = await AsyncExecuter.ToListAsync((await examRepository.GetQueryableAsync()).Where(exam =>
				exam.TenantId == tenantId && exam.AcademicYearId == lead.AcademicYearId));
			var counts = exams.GroupBy(exam => exam.EntranceExamSlotId).ToDictionary(group => group.Key, group => group.Count());
			var now = DateTimeOffset.UtcNow;
			return new AdmissionCrmPublicExamSlotListDto
			{
				SelfSchedulingEnabled = true,
				Slots = slots
					.Where(slot => slot.StartDate > now && slot.StudentLimit > counts.GetValueOrDefault(slot.Id))
					.OrderBy(slot => slot.StartDate)
					.Select(slot => new AdmissionCrmPublicExamSlotDto
					{
						EntranceExamSlotId = slot.Id,
						SlotName = slot.SlotName ?? "Entrance exam",
						StartTime = slot.StartDate,
						EndTime = slot.EndDate,
						RoomNo = slot.RoomNo,
						AvailableSeats = slot.StudentLimit - counts.GetValueOrDefault(slot.Id)
					})
					.ToList()
			};
		}
	}

	public async Task<AdmissionCrmEntranceExamDto> ScheduleAsync(
		Guid tenantId,
		string token,
		ScheduleAdmissionCrmPublicExamDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			if (!await IsSelfSchedulingEnabledAsync(lead))
			{
				throw new BusinessException("AdmissionCrm:ApplicantSelfSchedulingNotEnabled");
			}

			return await scheduling.ScheduleFromApplicantAsync(lead.Id, input.EntranceExamSlotId);
		}
	}

	private async Task<bool> IsSelfSchedulingEnabledAsync(AdmissionInquiryLead lead)
	{
		var queryable = await playbookRepository.GetQueryableAsync();
		var playbooks = await AsyncExecuter.ToListAsync(queryable.Where(playbook =>
			playbook.TenantId == CurrentTenant.Id &&
			playbook.AcademicYearId == lead.AcademicYearId &&
			playbook.Status == AdmissionCrmPlaybookStatus.Published));
		foreach (var playbook in playbooks.OrderBy(playbook => playbook.IsFallback))
		{
			var scope = JsonSerializer.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(
				playbook.ApplicabilityJson, AdmissionCrmPublicFormat.JsonOptions) ?? new();
			if (!playbook.IsFallback && !MatchesScope(scope, lead))
			{
				continue;
			}

			var rules = JsonSerializer.Deserialize<List<AdmissionCrmSlotAssignmentRuleDto>>(
				playbook.AssignmentRulesJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
			if (rules.Any(rule => rule.IsActive &&
				string.Equals(rule.Trigger, "ApplicantSelfSchedule", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	private static bool MatchesScope(AdmissionCrmPlaybookApplicabilityDto configured, AdmissionInquiryLead lead) =>
		MatchesOptional(configured.Campus, lead.CampusBranchName) &&
		MatchesOptional(configured.DesiredClass, lead.DesiredClassName) &&
		string.IsNullOrWhiteSpace(configured.Board) &&
		string.IsNullOrWhiteSpace(configured.Medium) &&
		string.IsNullOrWhiteSpace(configured.ApplicantCategory) &&
		string.IsNullOrWhiteSpace(configured.AdmissionRound) &&
		string.IsNullOrWhiteSpace(configured.AdmissionRoute);

	private static bool MatchesOptional(string? configured, string? actual) =>
		string.IsNullOrWhiteSpace(configured) ||
		string.Equals(configured.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
}
