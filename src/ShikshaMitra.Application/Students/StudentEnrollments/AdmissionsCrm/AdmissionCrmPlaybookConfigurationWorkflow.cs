using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookConfigurationWorkflow(
	IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
	AdmissionCrmPlaybookAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPlaybookDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmPlaybookDto input)
	{
		await access.EnsureConfigurationAsync(academicYearId, nameof(CreateAsync));
		AdmissionCrmPlaybookValidator.NormalizeAndValidate(input);
		var playbook = CreateAggregate(academicYearId, 1, input);
		await playbookRepository.InsertAsync(playbook, autoSave: true);
		return AdmissionCrmPlaybookMapper.ToDto(playbook);
	}

	public async Task<PagedResultDto<AdmissionCrmPlaybookDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmPlaybookListInput input)
	{
		await access.EnsureYearAsync(academicYearId, nameof(GetListAsync));
		var queryable = await playbookRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var filter = input.Filter.Trim();
			query = query.Where(x => x.Name.Contains(filter));
		}
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name).ThenByDescending(x => x.Version)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmPlaybookDto>(totalCount, items.Select(AdmissionCrmPlaybookMapper.ToDto).ToList());
	}

	public async Task<AdmissionCrmPlaybookDto> GetAsync(Guid id) =>
		AdmissionCrmPlaybookMapper.ToDto(await access.GetAsync(id, nameof(GetAsync)));

	public async Task<AdmissionCrmPlaybookDto> UpdateAsync(Guid id, UpdateAdmissionCrmPlaybookDto input)
	{
		var playbook = await access.GetAsync(id, nameof(UpdateAsync));
		await access.EnsureConfigurationAsync(playbook.AcademicYearId, nameof(UpdateAsync));
		AdmissionCrmPlaybookValidator.NormalizeAndValidate(input);
		ApplyUpdate(playbook, input);
		await playbookRepository.UpdateAsync(playbook, autoSave: true);
		return AdmissionCrmPlaybookMapper.ToDto(playbook);
	}

	public async Task<AdmissionCrmPlaybookDto> PublishAsync(Guid id)
	{
		var playbook = await access.GetAsync(id, nameof(PublishAsync));
		await access.EnsureConfigurationAsync(playbook.AcademicYearId, nameof(PublishAsync));
		var validationInput = AdmissionCrmPlaybookMapper.ToUpsert(playbook);
		AdmissionCrmPlaybookValidator.NormalizeAndValidate(validationInput);
		await EnsureNoPublishedConflictAsync(playbook, validationInput.Applicability);
		playbook.Publish(Clock.Now);
		await playbookRepository.UpdateAsync(playbook, autoSave: true);
		return AdmissionCrmPlaybookMapper.ToDto(playbook);
	}

	public async Task<AdmissionCrmPlaybookDto> CreateNewVersionAsync(Guid id)
	{
		var source = await access.GetAsync(id, nameof(CreateNewVersionAsync));
		await access.EnsureConfigurationAsync(source.AcademicYearId, nameof(CreateNewVersionAsync));
		if (source.Status == AdmissionCrmPlaybookStatus.Draft)
			throw new BusinessException("AdmissionCrm:DraftPlaybookAlreadyEditable").WithData("PlaybookId", source.Id);
		var queryable = await playbookRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == source.AcademicYearId && x.Name == source.Name));
		var nextVersion = versions.Count == 0 ? source.Version + 1 : versions.Max(x => x.Version) + 1;
		var copy = new AdmissionCrmPlaybook(GuidGenerator.Create(), CurrentTenant.Id, source.AcademicYearId, source.Name,
			nextVersion, source.IsFallback, source.RegistrationPath, source.ApplicabilityJson, source.StagesJson, source.RoundsJson,
			source.SeatPlansJson, source.ExamDefinitionsJson, source.SlotTemplatesJson, source.AssignmentRulesJson, source.Notes);
		await playbookRepository.InsertAsync(copy, autoSave: true);
		return AdmissionCrmPlaybookMapper.ToDto(copy);
	}

	public async Task<AdmissionCrmPlaybookDto> RetireAsync(Guid id)
	{
		var playbook = await access.GetAsync(id, nameof(RetireAsync));
		await access.EnsureConfigurationAsync(playbook.AcademicYearId, nameof(RetireAsync));
		playbook.Retire(Clock.Now);
		await playbookRepository.UpdateAsync(playbook, autoSave: true);
		return AdmissionCrmPlaybookMapper.ToDto(playbook);
	}

	private async Task EnsureNoPublishedConflictAsync(AdmissionCrmPlaybook playbook, AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		var queryable = await playbookRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == playbook.AcademicYearId && x.Status == AdmissionCrmPlaybookStatus.Published && x.Id != playbook.Id));
		var specificity = playbook.IsFallback ? 0 : AdmissionCrmPlaybookApplicability.Specificity(applicability);
		foreach (var existing in published)
		{
			var existingApplicability = AdmissionCrmPlaybookMapper.Deserialize<AdmissionCrmPlaybookApplicabilityDto>(existing.ApplicabilityJson);
			var existingSpecificity = existing.IsFallback ? 0 : AdmissionCrmPlaybookApplicability.Specificity(existingApplicability);
			if (specificity == existingSpecificity && AdmissionCrmPlaybookApplicability.Overlaps(applicability, existingApplicability))
				throw new BusinessException("AdmissionCrm:ConflictingPublishedPlaybook")
					.WithData("PlaybookId", playbook.Id).WithData("ConflictingPlaybookId", existing.Id);
		}
	}

	private AdmissionCrmPlaybook CreateAggregate(Guid academicYearId, int version, AdmissionCrmPlaybookUpsertDto input) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.Name, version, input.IsFallback, input.RegistrationPath,
			AdmissionCrmPlaybookMapper.Serialize(input.Applicability), AdmissionCrmPlaybookMapper.Serialize(input.Stages),
			AdmissionCrmPlaybookMapper.Serialize(input.Rounds), AdmissionCrmPlaybookMapper.Serialize(input.SeatPlans),
			AdmissionCrmPlaybookMapper.Serialize(input.ExamDefinitions), AdmissionCrmPlaybookMapper.Serialize(input.SlotTemplates),
			AdmissionCrmPlaybookMapper.Serialize(input.AssignmentRules), input.Notes);

	private static void ApplyUpdate(AdmissionCrmPlaybook playbook, AdmissionCrmPlaybookUpsertDto input) => playbook.Update(
		input.Name, input.IsFallback, input.RegistrationPath, AdmissionCrmPlaybookMapper.Serialize(input.Applicability),
		AdmissionCrmPlaybookMapper.Serialize(input.Stages), AdmissionCrmPlaybookMapper.Serialize(input.Rounds),
		AdmissionCrmPlaybookMapper.Serialize(input.SeatPlans), AdmissionCrmPlaybookMapper.Serialize(input.ExamDefinitions),
		AdmissionCrmPlaybookMapper.Serialize(input.SlotTemplates), AdmissionCrmPlaybookMapper.Serialize(input.AssignmentRules), input.Notes);
}
