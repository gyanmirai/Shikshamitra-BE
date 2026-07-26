using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConfigurationSnapshotReader(
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionCrmInquirySource, Guid> inquirySourceRepository,
	IRepository<AdmissionInquiryFormTemplate, Guid> inquiryFormRepository,
	IRepository<AdmissionCrmMessageTemplate, Guid> messageTemplateRepository,
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	IRepository<AdmissionCrmRegistrationTemplate, Guid> registrationTemplateRepository,
	IRepository<AdmissionCrmFormNumberSeries, Guid> formNumberSeriesRepository,
	IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> documentChecklistRepository,
	IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
	IRepository<AdmissionCrmFeePolicy, Guid> feePolicyRepository,
	IRepository<AdmissionCrmRouteDefinition, Guid> routeDefinitionRepository,
	IRepository<AdmissionCrmPolicyAdoption, Guid> policyAdoptionRepository,
	IRepository<AdmissionCrmPolicyPack, Guid> policyPackRepository,
	IRepository<AdmissionCrmNumberSeries, Guid> numberSeriesRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ICurrentTenant currentTenant) : ITransientDependency
{
	internal async Task<AcademicYear> GetAcademicYearAsync(Guid academicYearId)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearRequired");
		}

		var year = await academicYearRepository.GetAsync(academicYearId);
		if (year.TenantId != currentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearTenantMismatch")
				.WithData("AcademicYearId", academicYearId);
		}

		return year;
	}

	internal async Task<AdmissionCrmConfigurationSnapshot> LoadAsync(Guid academicYearId)
	{
		var academicYear = await GetAcademicYearAsync(academicYearId);
		var tenantId = currentTenant.Id;

		var cycles = await cycleRepository.GetQueryableAsync();
		var stages = await stageRepository.GetQueryableAsync();
		var inquirySources = await inquirySourceRepository.GetQueryableAsync();
		var inquiryForms = await inquiryFormRepository.GetQueryableAsync();
		var messageTemplates = await messageTemplateRepository.GetQueryableAsync();
		var playbooks = await playbookRepository.GetQueryableAsync();
		var registrationTemplates = await registrationTemplateRepository.GetQueryableAsync();
		var formNumberSeries = await formNumberSeriesRepository.GetQueryableAsync();
		var documentChecklists = await documentChecklistRepository.GetQueryableAsync();
		var seatPools = await seatPoolRepository.GetQueryableAsync();
		var feePolicies = await feePolicyRepository.GetQueryableAsync();
		var routeDefinitions = await routeDefinitionRepository.GetQueryableAsync();
		var policyAdoptions = await policyAdoptionRepository.GetQueryableAsync();
		var numberSeries = await numberSeriesRepository.GetQueryableAsync();
		var educationBoards = await educationBoardRepository.GetQueryableAsync();
		var educationMediums = await educationMediumRepository.GetQueryableAsync();
		var educationMediumStages = await educationMediumStageRepository.GetQueryableAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();

		var boardList = await asyncExecuter.ToListAsync(educationBoards.Where(x =>
			x.TenantId == tenantId &&
			x.AcademicYearId == academicYearId));
		var boardIds = boardList.Select(x => x.Id).ToList();
		var mediumList = boardIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync(educationMediums.Where(x =>
				x.TenantId == tenantId &&
				boardIds.Contains(x.EducationBoardId)));
		var mediumIds = mediumList.Select(x => x.Id).ToList();
		var mediumStageList = mediumIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync(educationMediumStages.Where(x =>
				x.TenantId == tenantId &&
				mediumIds.Contains(x.EducationMediumId)));
		var mediumStageIds = mediumStageList.Select(x => x.Id).ToList();
		var standardList = mediumStageIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync(standards.Where(x =>
				x.TenantId == tenantId &&
				mediumStageIds.Contains(x.EducationMediumStageId)));
		var standardIds = standardList.Select(x => x.Id).ToList();
		var divisionList = standardIds.Count == 0
			? []
			: await asyncExecuter.ToListAsync(divisions.Where(x =>
				x.TenantId == tenantId &&
				standardIds.Contains(x.StandardId)));

		var cycle = await asyncExecuter.FirstOrDefaultAsync(cycles.Where(x =>
			x.TenantId == tenantId &&
			x.AcademicYearId == academicYearId));
		var snapshot = new AdmissionCrmConfigurationSnapshot(
			academicYear,
			cycle,
			await asyncExecuter.ToListAsync(stages.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(inquirySources.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(inquiryForms.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(messageTemplates.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(playbooks.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(registrationTemplates.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(formNumberSeries.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(documentChecklists.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(seatPools.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(feePolicies.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(routeDefinitions.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(policyAdoptions.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			await asyncExecuter.ToListAsync(numberSeries.Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId)),
			boardList,
			mediumList,
			mediumStageList,
			standardList,
			divisionList);

		var referencedPolicyPackIds = snapshot.PolicyAdoptions
			.Select(x => x.PolicyPackId)
			.Distinct()
			.ToList();
		if (referencedPolicyPackIds.Count > 0)
		{
			var policyPacks = await policyPackRepository.GetQueryableAsync();
			snapshot.PolicyPacks.AddRange(await asyncExecuter.ToListAsync(policyPacks.Where(x =>
				x.TenantId == tenantId &&
				referencedPolicyPackIds.Contains(x.Id))));
		}

		return snapshot;
	}

	internal async Task<int> CountApplicantsAsync(Guid academicYearId)
	{
		var query = await leadRepository.GetQueryableAsync();
		return await asyncExecuter.CountAsync(query.Where(x =>
			x.TenantId == currentTenant.Id &&
			x.AcademicYearId == academicYearId));
	}

	internal async Task<(int Count, IReadOnlyList<Guid> SampleIds)> GetApplicantReferencesAsync(
		Guid academicYearId,
		AdmissionCrmConfigurationReferenceKind kind,
		string code,
		IReadOnlyCollection<Guid> routeDefinitionIds)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == currentTenant.Id &&
			x.AcademicYearId == academicYearId);

		if (kind == AdmissionCrmConfigurationReferenceKind.Route)
		{
			query = routeDefinitionIds.Count == 0
				? query.Where(_ => false)
				: query.Where(x =>
					x.IntendedRouteDefinitionId.HasValue &&
					routeDefinitionIds.Contains(x.IntendedRouteDefinitionId.Value));
		}
		else if (kind == AdmissionCrmConfigurationReferenceKind.Stage &&
		         Enum.TryParse<AdmissionInquiryStage>(code, ignoreCase: true, out var stage))
		{
			query = query.Where(x => x.Stage == stage);
		}
		else
		{
			query = query.Where(_ => false);
		}

		var count = await asyncExecuter.CountAsync(query);
		var sampleIds = await asyncExecuter.ToListAsync(
			query.OrderBy(x => x.Id).Select(x => x.Id).Take(25));
		return (count, sampleIds);
	}
}

internal sealed class AdmissionCrmConfigurationSnapshot(
	AcademicYear academicYear,
	AdmissionCrmCycleConfiguration? cycle,
	List<AdmissionCrmPipelineStage> pipelineStages,
	List<AdmissionCrmInquirySource> inquirySources,
	List<AdmissionInquiryFormTemplate> inquiryForms,
	List<AdmissionCrmMessageTemplate> messageTemplates,
	List<AdmissionCrmPlaybook> playbooks,
	List<AdmissionCrmRegistrationTemplate> registrationTemplates,
	List<AdmissionCrmFormNumberSeries> formNumberSeries,
	List<AdmissionCrmDocumentChecklistTemplate> documentChecklists,
	List<AdmissionCrmSeatPool> seatPools,
	List<AdmissionCrmFeePolicy> feePolicies,
	List<AdmissionCrmRouteDefinition> routeDefinitions,
	List<AdmissionCrmPolicyAdoption> policyAdoptions,
	List<AdmissionCrmNumberSeries> numberSeries,
	List<EducationBoard> educationBoards,
	List<EducationMedium> educationMediums,
	List<EducationMediumStage> educationMediumStages,
	List<Standard> standards,
	List<Division> divisions)
{
	internal AcademicYear AcademicYear { get; } = academicYear;
	internal AdmissionCrmCycleConfiguration? Cycle { get; } = cycle;
	internal List<AdmissionCrmPipelineStage> PipelineStages { get; } = pipelineStages;
	internal List<AdmissionCrmInquirySource> InquirySources { get; } = inquirySources;
	internal List<AdmissionInquiryFormTemplate> InquiryForms { get; } = inquiryForms;
	internal List<AdmissionCrmMessageTemplate> MessageTemplates { get; } = messageTemplates;
	internal List<AdmissionCrmPlaybook> Playbooks { get; } = playbooks;
	internal List<AdmissionCrmRegistrationTemplate> RegistrationTemplates { get; } = registrationTemplates;
	internal List<AdmissionCrmFormNumberSeries> FormNumberSeries { get; } = formNumberSeries;
	internal List<AdmissionCrmDocumentChecklistTemplate> DocumentChecklists { get; } = documentChecklists;
	internal List<AdmissionCrmSeatPool> SeatPools { get; } = seatPools;
	internal List<AdmissionCrmFeePolicy> FeePolicies { get; } = feePolicies;
	internal List<AdmissionCrmRouteDefinition> RouteDefinitions { get; } = routeDefinitions;
	internal List<AdmissionCrmPolicyAdoption> PolicyAdoptions { get; } = policyAdoptions;
	internal List<AdmissionCrmNumberSeries> NumberSeries { get; } = numberSeries;
	internal List<AdmissionCrmPolicyPack> PolicyPacks { get; } = [];
	internal List<EducationBoard> EducationBoards { get; } = educationBoards;
	internal List<EducationMedium> EducationMediums { get; } = educationMediums;
	internal List<EducationMediumStage> EducationMediumStages { get; } = educationMediumStages;
	internal List<Standard> Standards { get; } = standards;
	internal List<Division> Divisions { get; } = divisions;

	internal int ConfigurationEntityCount =>
		(Cycle == null ? 0 : 1) +
		PipelineStages.Count +
		InquirySources.Count +
		InquiryForms.Count +
		MessageTemplates.Count +
		Playbooks.Count +
		RegistrationTemplates.Count +
		FormNumberSeries.Count +
		DocumentChecklists.Count +
		SeatPools.Count +
		FeePolicies.Count +
		RouteDefinitions.Count +
		PolicyAdoptions.Count +
		NumberSeries.Count;
}
