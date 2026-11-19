using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentRecords;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.View)]
public class StudentRecordsSettingsAppService(IRepository<StudentRecordsConfiguration, Guid> configurations,
	IRepository<StudentRecordsPolicyVersion, Guid> versions, IRepository<AcademicYear, Guid> years,
	IIdentityRoleRepository roles, StudentRecordsPolicyResolver resolver, StudentRecordsActorService actor,
	StudentScopeAccessService studentScope)
	: ApplicationService, IStudentRecordsSettingsAppService
{
	public async Task<StudentRecordsRoleNamesDto> GetRoleNamesAsync()
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(StudentRecordsErrorCodes.TenantRequired);
		await actor.EnsureActiveAsync();
		var scope = await studentScope.GetCurrentUserAcademicYearScopeAsync();
		if (!scope.HasAllAcademicYears && scope.AcademicYearIds.Count == 0)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return new() { Items = (await roles.GetListAsync()).Where(x => x.TenantId == CurrentTenant.Id)
			.OrderBy(x => x.Name).Select(x => new StudentRecordsRoleNameDto { Name = x.Name }).ToList() };
	}

	public async Task<StudentRecordsSettingsDto> GetAsync(Guid? academicYearId)
	{
		await EnsureYearAsync(academicYearId);
		var configuration = await FindAsync(academicYearId);
		var effective = await resolver.ResolveAsync(academicYearId);
		return new()
		{
			Id = configuration?.Id, AcademicYearId = academicYearId, ConcurrencyStamp = configuration?.ConcurrencyStamp,
			DraftPolicy = configuration == null ? StudentRecordsPolicy.FromJson(effective.Policy.ToJson()) : StudentRecordsPolicy.FromJson(configuration.DraftPolicyJson),
			EffectivePolicy = effective.Policy, PublishedVersionId = effective.VersionId,
			PublishedVersionNumber = effective.VersionNumber, IsStarter = effective.IsStarter,
			EffectiveScopeAcademicYearId = effective.ScopeAcademicYearId
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	[UnitOfWork(true)]
	public async Task<StudentRecordsSettingsDto> SaveDraftAsync(SaveStudentRecordsSettingsDto input)
	{
		await ValidateAsync(input.Policy, input.AcademicYearId);
		var configuration = await FindAsync(input.AcademicYearId);
		EnsureConcurrency(configuration, input.ConcurrencyStamp);
		if (configuration == null)
			await configurations.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, input.AcademicYearId, input.Policy.ToJson()), autoSave: true);
		else
		{
			configuration.SaveDraft(input.Policy.ToJson());
			await configurations.UpdateAsync(configuration, autoSave: true);
		}
		return await GetAsync(input.AcademicYearId);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add)]
	public async Task<StudentRecordsSettingsPreviewDto> PreviewAsync(SaveStudentRecordsSettingsDto input)
	{
		await ValidateAsync(input.Policy, input.AcademicYearId);
		var policy = input.Policy;
		return new()
		{
			Policy = policy,
			Summary = [$"Provisioning access: {policy.Provisioning.Mode}.",
				$"{policy.Changes.Count(x => x.Value.Mode == "Approval")} change actions require approval; requesters cannot approve their own requests.",
				$"{policy.Access.Masks.Count} sensitive field groups are restricted.",
				$"Batch exceptions: {policy.Transitions.BatchExceptionMode}. Annual renewal: {policy.Transitions.AnnualRenewalMode}.",
				"Publication affects new requests; submitted requests retain their original policy version."]
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Publish)]
	[UnitOfWork(true)]
	public async Task<StudentRecordsSettingsDto> PublishAsync(PublishStudentRecordsSettingsDto input)
	{
		await EnsureYearAsync(input.AcademicYearId, writing: true);
		var configuration = await FindAsync(input.AcademicYearId)
			?? throw new BusinessException(StudentRecordsErrorCodes.DraftRequired);
		EnsureConcurrency(configuration, input.ConcurrencyStamp);
		await ValidateAsync(StudentRecordsPolicy.FromJson(configuration.DraftPolicyJson), input.AcademicYearId);
		var version = new StudentRecordsPolicyVersion(GuidGenerator.Create(), CurrentTenant.Id!.Value, configuration.Id,
			configuration.AcademicYearId, configuration.PublishedVersionNumber + 1, configuration.DraftPolicyJson);
		await versions.InsertAsync(version);
		configuration.Publish(version.Id);
		await configurations.UpdateAsync(configuration, autoSave: true);
		return await GetAsync(input.AcademicYearId);
	}

	private Task<StudentRecordsConfiguration?> FindAsync(Guid? yearId) => configurations.FindAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
	private async Task EnsureYearAsync(Guid? yearId, bool writing = false)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(StudentRecordsErrorCodes.TenantRequired);
		await actor.EnsureActiveAsync();
		if (yearId.HasValue && !await years.AnyAsync(x => x.Id == yearId && x.TenantId == CurrentTenant.Id))
			throw new BusinessException(StudentRecordsErrorCodes.InvalidAcademicYear);
		await studentScope.EnsureSettingsScopeAsync(yearId, writing, "StudentRecordsSettings");
	}
	private async Task ValidateAsync(StudentRecordsPolicy policy, Guid? yearId)
	{
		await EnsureYearAsync(yearId, writing: true);
		StudentRecordsPolicyValidator.Validate(policy);
		var available = (await roles.GetListAsync()).Where(x => x.TenantId == CurrentTenant.Id).Select(x => x.Name).ToList();
		var unknown = StudentRecordsPolicyValidator.RoleNames(policy).Where(x => !available.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
		if (unknown.Length != 0) throw new BusinessException(StudentRecordsErrorCodes.InvalidApproverRole).WithData("Roles", string.Join(", ", unknown));
	}
	private static void EnsureConcurrency(StudentRecordsConfiguration? configuration, string? stamp)
	{
		if ((configuration != null && (string.IsNullOrWhiteSpace(stamp) || configuration.ConcurrencyStamp != stamp)) ||
			(configuration == null && stamp != null)) throw new BusinessException(StudentRecordsErrorCodes.ConfigurationChanged);
	}
}
