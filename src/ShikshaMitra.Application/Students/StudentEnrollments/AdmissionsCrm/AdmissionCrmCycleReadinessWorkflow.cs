using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCycleReadinessWorkflow(
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	StudentScopeAccessService scopeAccess,
	AdmissionCrmCycleConfigurationWorkflow configurationWorkflow,
	AdmissionCrmApplicantCountReader applicantCountReader,
	AdmissionCrmSetupReadinessReader readinessReader,
	AdmissionCrmPresetAdoptionReader adoptionReader,
	AdmissionCrmSettingsAccessWorkflow settingsAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmSetupStatusDto> GetStatusAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(academicYearId, nameof(GetStatusAsync));
		var configuration = await configurationWorkflow.GetAsync(academicYearId);
		var policies = await settingsAccess.GetAccessPolicyAsync();
		var readiness = await readinessReader.LoadAsync(academicYearId);
		var presetAdoption = await adoptionReader.LoadAsync(academicYearId);
		var adoption = presetAdoption.Adoption;
		var admissionYearReady = readiness.PublishedAdmissionYear is not null;
		var pipelineReady = readiness.HasActiveSource && readiness.HasActiveType;
		var examReady = admissionYearReady && (!readiness.PublishedAdmissionYear!.EntranceExamRequired || readiness.HasPublishedExamPolicy);
		var registrationReady = readiness.HasPublishedRegistration && readiness.HasPublishedDocuments;
		var communicationReady = readiness.HasApprovedMessage;
		var allReady = admissionYearReady && pipelineReady && examReady && registrationReady && communicationReady;
		var overall = allReady ? adoption is null ? AdmissionCrmSetupReadiness.Ready : AdmissionCrmSetupReadiness.Managed : AdmissionCrmSetupReadiness.NeedsAttention;
		var policyByKey = policies.ToDictionary(x => x.SettingsKey, StringComparer.OrdinalIgnoreCase);
		var definitions = AdmissionCrmSetupDefinition.All(admissionYearReady, pipelineReady, examReady, registrationReady, communicationReady);
		return new AdmissionCrmSetupStatusDto
		{
			AcademicYearId = academicYearId,
			CycleName = configuration.CycleName,
			Status = overall,
			AppliedPresetName = presetAdoption.Preset?.Name,
			AppliedPresetVersion = adoption?.PresetVersion,
			LastUpdatedAt = readiness.PublishedAdmissionYear?.LastModificationTime ?? readiness.PublishedAdmissionYear?.CreationTime ?? adoption?.AppliedAt,
			Items = definitions.Select(item =>
			{
				var mode = policyByKey.GetValueOrDefault(item.PolicyKey)?.AccessMode ?? AdmissionCrmSettingsAccessMode.Editable;
				var status = mode == AdmissionCrmSettingsAccessMode.Hidden ? AdmissionCrmSetupReadiness.Locked :
					item.Ready ? adoption is null ? AdmissionCrmSetupReadiness.Ready : AdmissionCrmSetupReadiness.Managed : AdmissionCrmSetupReadiness.NeedsAttention;
				return new AdmissionCrmSetupItemDto
				{
					Key = item.Key,
					Title = item.Title,
					Path = item.Path,
					AccessMode = mode,
					Status = status,
					BlockerCodes = status == AdmissionCrmSetupReadiness.NeedsAttention ? [item.BlockerCode] : []
				};
			}).ToList()
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<PagedResultDto<AdmissionCrmCycleListItemDto>> GetCyclesAsync(PagedResultRequestDto input)
	{
		var yearQuery = (await academicYearRepository.GetQueryableAsync())
			.Where(year => year.TenantId == CurrentTenant.Id)
			.OrderByDescending(year => year.StartDate);
		var total = await AsyncExecuter.CountAsync(yearQuery);
		var years = await AsyncExecuter.ToListAsync(yearQuery
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));
		foreach (var year in years)
		{
			await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(year.Id, nameof(GetCyclesAsync));
		}

		var yearIds = years.Select(year => year.Id).ToList();
		var configurations = yearIds.Count == 0
			? []
			: await AsyncExecuter.ToListAsync((await cycleRepository.GetQueryableAsync())
				.Where(configuration =>
					configuration.TenantId == CurrentTenant.Id &&
					yearIds.Contains(configuration.AcademicYearId)));
		var applicantCounts = await applicantCountReader.GetAsync(yearIds);
		var rows = years
			.Select(year => AdmissionCrmCycleMapper.Build(
				year,
				configurations.FirstOrDefault(configuration => configuration.AcademicYearId == year.Id),
				applicantCounts.GetValueOrDefault(year.Id),
				null))
			.ToList();
		return new PagedResultDto<AdmissionCrmCycleListItemDto>(total, rows);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmCycleSummaryDto> GetCycleSummaryAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(GetCycleSummaryAsync));
		var year = await GetAcademicYearAsync(academicYearId);
		var readiness = await GetStatusAsync(academicYearId);
		var configuration = await configurationWorkflow.GetAsync(academicYearId);
		var journeyRules = configuration.JourneyRules;
		var applicantCounts = await applicantCountReader.GetAsync([academicYearId]);
		var cycle = AdmissionCrmCycleMapper.Build(
			year,
			await cycleRepository.FindAsync(configuration.Id),
			applicantCounts.GetValueOrDefault(academicYearId),
			readiness);

		return new AdmissionCrmCycleSummaryDto
		{
			Cycle = cycle,
			Readiness = readiness,
			Communications = new AdmissionCrmCycleCommunicationSummaryDto
			{
				JourneyRuleCount = journeyRules.Count,
				ActiveMessageRuleCount = journeyRules.Count(x => x.IsEnabled && x.SendMessage),
				TemplateBackedRuleCount = journeyRules.Count(x => x.IsEnabled && x.SendMessage && x.MessageTemplateId.HasValue)
			},
			ApplicationStagePolicy = AdmissionCrmApplicationStagePolicyMapper.Build(
				await cycleRepository.GetAsync(configuration.Id),
				applicantCounts.GetValueOrDefault(academicYearId)),
			Policy = new AdmissionCrmCyclePolicySummaryDto
			{
				IsActive = configuration.IsActive,
				RegistrationFormRequiredBeforeExam = configuration.RegistrationFormRequiredBeforeExam,
				EntranceExamRequired = configuration.EntranceExamRequired,
				JourneyLocked = configuration.JourneyLocked,
				DefaultLeadSlaHours = configuration.DefaultLeadSlaHours,
				StaleAfterDays = configuration.StaleAfterDays,
				ArchiveAfterDays = configuration.ArchiveAfterDays,
				DefaultExamPassingPercentage = configuration.DefaultExamPassingPercentage,
				MeritRankingMode = configuration.MeritRankingMode
			}
		};
	}

	private async Task<AcademicYear> GetAcademicYearAsync(Guid academicYearId)
	{
		var year = await academicYearRepository.FindAsync(academicYearId);
		if (year is null || year.TenantId != CurrentTenant.Id)
			throw new Volo.Abp.BusinessException("AdmissionCrm:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		return year;
	}
}

internal sealed record AdmissionCrmSetupDefinition(string Key, string Title, string Path, string PolicyKey, bool Ready, string BlockerCode)
{
	internal static IReadOnlyList<AdmissionCrmSetupDefinition> All(bool admissionYear, bool pipeline, bool exam, bool registration, bool communication) =>
	[
		new("admission-year", "Admission Year Setup", "/student/enrollment/settings/admission-year", "journey", admissionYear, "EnrollmentSetup:PublishedAdmissionYearRequired"),
		new("pipeline", "Pipeline Settings", "/student/enrollment/settings/pipeline", "inquiry-capture", pipeline, "EnrollmentSetup:InquirySourceAndTypeRequired"),
		new("entrance-exam", "Entrance Exam Settings", "/student/enrollment/settings/entrance-exam", "entrance-exams", exam, "EnrollmentSetup:PublishedExamPolicyRequired"),
		new("registration", "Registration Settings", "/student/enrollment/settings/registration", "registration-documents", registration, "EnrollmentSetup:PublishedRegistrationAndDocumentsRequired"),
		new("finance-handoff", "Finance Handoff Settings", "/student/enrollment/settings/finance-handoff", "journey", admissionYear, "EnrollmentSetup:PublishedAdmissionYearRequired"),
		new("class-placement", "Class Placement Settings", "/student/enrollment/settings/class-placement", "offers-seats", admissionYear, "EnrollmentSetup:PublishedAdmissionYearRequired"),
		new("activation", "Activation Settings", "/student/enrollment/settings/activation", "activation", admissionYear, "EnrollmentSetup:PublishedAdmissionYearRequired"),
		new("communication", "Communication Settings", "/student/enrollment/settings/communication", "communications", communication, "EnrollmentSetup:ApprovedMessageTemplateRequired")
	];
}