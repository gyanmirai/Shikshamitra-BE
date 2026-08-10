using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamResultWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	AdmissionCrmEntranceExamAccess access,
	AdmissionCrmEntranceExamTimeline timeline,
	AdmissionCrmEntranceExamJourney journey) : ApplicationService, ITransientDependency
{
	private const int MaximumCorrectionReasonLength = AdmissionCrmConsts.MaxTimelineDetailsLength - 250;

	public Task<AdmissionCrmEntranceExamDto> UploadAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input) =>
		UploadCoreAsync(examRecordId, input, input.CorrectionReason);

	public Task<AdmissionCrmEntranceExamDto> UploadFromBulkAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input,
		string? correctionReason) => UploadCoreAsync(examRecordId, input, correctionReason);

	private async Task<AdmissionCrmEntranceExamDto> UploadCoreAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input,
		string? correctionReason)
	{
		var exam = await access.GetExamAsync(examRecordId, "UploadResultAsync");
		var lead = await access.GetLeadAsync(exam.LeadId, "UploadResultAsync");
		var isCorrection = exam.ResultRecordedAt.HasValue;
		if (isCorrection && IsSameResult(exam, input)) return AdmissionCrmEntranceExamMapper.ToDto(exam, lead);

		var normalizedReason = string.IsNullOrWhiteSpace(correctionReason) ? null : correctionReason.Trim();
		if (normalizedReason?.Length > MaximumCorrectionReasonLength)
			throw new BusinessException("AdmissionCrm:ExamResultCorrectionReasonTooLong")
				.WithData("MaximumLength", MaximumCorrectionReasonLength);
		if (isCorrection)
		{
			if (normalizedReason == null)
				throw new BusinessException("AdmissionCrm:ExamResultCorrectionReasonRequired")
					.WithData("ExamRecordId", exam.Id);
			if (!await AuthorizationService.IsGrantedAsync(
				    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult))
				throw new AbpAuthorizationException("Exam result override permission is required.");
		}

		var previousEvidence = isCorrection
			? FormatEvidence(exam.TotalMarks, exam.PassingMarks, exam.ObtainedMarks, exam.MeritRank)
			: null;
		var cycle = await access.FindCycleAsync(lead.AcademicYearId);
		var previousStage = lead.Stage;
		exam.RecordResult(input.TotalMarks, input.PassingMarks, input.ObtainedMarks, input.MeritRank, Clock.Now);
		if (!input.MeritRank.HasValue)
			await RecalculateMeritRanksAsync(lead, exam,
				cycle?.MeritRankingMode ?? AdmissionCrmMeritRankingMode.MarksThenInquiryDate);

		lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(
			lead, AdmissionCrmLeadScoreCalculator.DeserializeRules(cycle?.LeadScoringRulesJson), exam));
		AdmissionCrmEntranceExamMapper.ApplyLeadStage(lead, exam);
		await examRepository.UpdateAsync(exam, autoSave: false);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ExamResultRecorded,
			isCorrection ? "Exam result corrected" : "Exam result recorded",
			isCorrection
				? $"Previous: {previousEvidence}. New: {FormatEvidence(exam.TotalMarks, exam.PassingMarks, exam.ObtainedMarks, exam.MeritRank)}. Correction reason: {normalizedReason}"
				: FormatEvidence(exam.TotalMarks, exam.PassingMarks, exam.ObtainedMarks, exam.MeritRank), exam.Id);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await journey.RunAsync(lead, AdmissionCrmJourneyTrigger.ExamCompleted);
		await journey.RunStageReachedIfChangedAsync(lead, previousStage);
		return AdmissionCrmEntranceExamMapper.ToDto(exam, lead);
	}

	private async Task RecalculateMeritRanksAsync(
		AdmissionInquiryLead targetLead,
		AdmissionCrmEntranceExamRecord currentExam,
		AdmissionCrmMeritRankingMode mode)
	{
		var leadQuery = await leadRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(leadQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == targetLead.AcademicYearId && x.MergedIntoLeadId == null));
		var sameClass = candidates.Where(x => IsSameMeritClass(targetLead, x)).ToList();
		var leadIds = sameClass.Select(x => x.Id).ToList();
		if (leadIds.Count == 0) return;
		var examQuery = await examRepository.GetQueryableAsync();
		var exams = await AsyncExecuter.ToListAsync(examQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == targetLead.AcademicYearId &&
			leadIds.Contains(x.LeadId) && x.ResultRecordedAt.HasValue && x.ObtainedMarks.HasValue));
		if (exams.All(x => x.Id != currentExam.Id)) exams.Add(currentExam);
		var byId = sameClass.ToDictionary(x => x.Id);
		var ranked = exams.Where(x => x.ObtainedMarks.HasValue && byId.ContainsKey(x.LeadId))
			.Select(x => new { Exam = x, Lead = byId[x.LeadId] }).ToList();
		var ordered = mode switch
		{
			AdmissionCrmMeritRankingMode.MarksThenLeadScore => ranked.OrderByDescending(x => x.Exam.ObtainedMarks)
				.ThenByDescending(x => x.Lead.LeadScore).ThenBy(x => x.Lead.CreationTime).ThenBy(x => x.Lead.Id),
			AdmissionCrmMeritRankingMode.LeadScoreThenMarks => ranked.OrderByDescending(x => x.Lead.LeadScore)
				.ThenByDescending(x => x.Exam.ObtainedMarks).ThenBy(x => x.Lead.CreationTime).ThenBy(x => x.Lead.Id),
			_ => ranked.OrderByDescending(x => x.Exam.ObtainedMarks).ThenBy(x => x.Lead.CreationTime).ThenBy(x => x.Lead.Id)
		};
		var rank = 1;
		var changed = new List<AdmissionCrmEntranceExamRecord>();
		foreach (var candidate in ordered)
		{
			candidate.Exam.SetMeritRank(rank++);
			if (candidate.Exam.Id != currentExam.Id) changed.Add(candidate.Exam);
		}
		if (changed.Count > 0) await examRepository.UpdateManyAsync(changed, autoSave: false);
	}

	private static bool IsSameResult(AdmissionCrmEntranceExamRecord exam, UploadAdmissionCrmEntranceExamResultDto input) =>
		exam.TotalMarks == input.TotalMarks && exam.PassingMarks == input.PassingMarks &&
		exam.ObtainedMarks == input.ObtainedMarks && (!input.MeritRank.HasValue || exam.MeritRank == input.MeritRank);

	private static string FormatEvidence(double? total, double? passing, double? obtained, int? rank) =>
		$"obtained {obtained:0.##}/{total:0.##}, passing {passing:0.##}, merit rank {(rank.HasValue ? rank.Value.ToString() : "automatic")}";

	private static bool IsSameMeritClass(AdmissionInquiryLead target, AdmissionInquiryLead candidate)
	{
		if (target.TargetStandardId.HasValue) return candidate.TargetStandardId == target.TargetStandardId;
		var targetClass = NormalizeClass(target.DesiredClassName);
		return !string.IsNullOrWhiteSpace(targetClass)
			? NormalizeClass(candidate.DesiredClassName) == targetClass
			: candidate.Id == target.Id;
	}

	private static string? NormalizeClass(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
