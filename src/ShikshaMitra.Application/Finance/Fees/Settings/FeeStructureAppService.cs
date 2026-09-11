using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp.Data;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public partial class FeeStructureAppService(
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IStandardRepository standardRepository,
	IAcademicYearRepository academicYearRepository,
	IRepository<PaymentTerm, Guid> paymentTermRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<ConcessionPolicy, Guid> concessionPolicyRepository,
	IRepository<RefundPolicy, Guid> refundPolicyRepository,
	IRepository<CollectionPolicy, Guid> collectionPolicyRepository,
	IRepository<ProrationPolicy, Guid> prorationPolicyRepository,
	IRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<StudentFeeStructure, Guid> studentStructureRepository,
	IDataFilter dataFilter,
	IUserScopeAccessService scopeAccess,
	FeeHeadAccountingManager accountingManager,
	PaymentTermScheduleResolver scheduleResolver
) : ApplicationService, IFeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task<FeeStructureCreateResponse> CreateAsync(Guid feeStructureMetadataId, StandardFeeStructureSaveRequest input)
	{
		var candidate = JsonSerializer.Deserialize<StandardFeeStructureRequest>(JsonSerializer.Serialize(input.StandardFeeStructures))
			?? throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
		var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
		if (input.RequestId.HasValue)
		{
			if (input.RequestId == Guid.Empty) throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
			var existing = await feeStructureRepository.FindAsync(input.RequestId.Value);
			if (existing != null)
			{
				await EnsureStandardScopeAsync(existing.StandardId);
				if (existing.FeeStructureMetadataId != feeStructureMetadataId || existing.GetProperty<string>("CreationRequestHash") != requestHash)
					throw new BusinessException(FeeStructureErrorCodes.ConcurrencyConflict);
				return await MapAsync(existing);
			}
		}
		ThrowIfInvalid(await ValidateCandidateAsync(feeStructureMetadataId, input.StandardId, candidate, null, !candidate.SaveAsDraft));
		await CheckAttachmentsAsync(candidate, null);
		await EnsureUniqueNameAsync(input.StandardId, candidate.Name, null, null);
		var entity = new FeeStructure(input.RequestId ?? GuidGenerator.Create(), CurrentTenant.Id, feeStructureMetadataId, input.StandardId, candidate.Name.Trim());
		entity.SetProperty("CreationRequestHash", requestHash);
		ApplyCandidate(entity, candidate);
		await feeStructureRepository.InsertAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task<FeeStructureCreateResponse> UpdateAsync(Guid id, StandardFeeStructureRequest input)
	{
		var entity = await GetScopedAsync(id);
		entity.EnsureNotLocked();
		await EnsureUnusedAsync(entity.Id);
		EnsureStamp(entity, input.ConcurrencyStamp);
		ThrowIfInvalid(await ValidateCandidateAsync(entity.FeeStructureMetadataId, entity.StandardId, input, entity, !input.SaveAsDraft));
		await CheckAttachmentsAsync(input, entity);
		await EnsureUniqueNameAsync(entity.StandardId, input.Name, entity.Id, entity.RevisionRootId);
		ApplyCandidate(entity, input);
		await feeStructureRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await GetScopedAsync(id);
		entity.EnsureNotLocked();
		await EnsureUnusedAsync(entity.Id);
		await feeStructureRepository.DeleteAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<StandardFeeStructureResponse> GetByStandardAsync(Guid standardId)
	{
		var standard = await standardRepository.GetAsync(standardId);
		await EnsureStandardScopeAsync(standardId);
		var entities = await feeStructureRepository.GetListAsync(x => x.StandardId == standardId);
		var result = new StandardFeeStructureResponse { StandardId = standard.Id, StandardName = standard.Name, StandardFeeStructures = [] };
		foreach (var entity in entities.OrderBy(x => x.CreationTime)) result.StandardFeeStructures.Add(await MapAsync(entity));
		return result;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task LockAsync(Guid id)
	{
		await GetScopedAsync(id);
		throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration)
			.WithData("Errors", new[] { "Review this structure and use Publish with its concurrency stamp." });
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task<FeeStructureCreateResponse> PublishAsync(Guid id, FeeStructurePublishRequest input)
	{
		var entity = await GetScopedAsync(id);
		if (entity.IsLocked) return await MapAsync(entity);
		EnsureStamp(entity, input.ConcurrencyStamp);
		await EnsureUnusedAsync(entity.Id);
		var candidate = ToRequest(entity);
		var review = await ValidateCandidateAsync(entity.FeeStructureMetadataId, entity.StandardId, candidate, entity, true);
		ThrowIfInvalid(review);
		await CheckAttachmentsAsync(candidate, entity);
		ApplyCandidate(entity, candidate);
		entity.SetProperty("PublicationReview", JsonSerializer.Serialize(review));
		entity.Lock();
		await feeStructureRepository.UpdateAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task<FeeStructureCreateResponse> CreateRevisionAsync(Guid id, FeeStructureRevisionRequest input)
	{
		// Referencing a published parent must not make EF's relationship fixup mark its aggregate modified.
		using var untrackedHistory = feeStructureRepository.DisableTracking();
		var previous = await GetScopedAsync(id);
		EnsureStamp(previous, input.ConcurrencyStamp);
		if (!previous.IsLocked) throw new BusinessException(FeeStructureErrorCodes.RevisionRequiresPublication);
		List<FeeStructure> siblings;
		using (dataFilter.Disable<ISoftDelete>())
			siblings = await feeStructureRepository.GetListAsync(x => x.RevisionRootId == previous.RevisionRootId);
		var draft = siblings.SingleOrDefault(x => !x.IsLocked && !x.IsDeleted);
		if (draft != null) return await MapAsync(draft);
		if (siblings.Where(x => x.IsLocked).Max(x => x.RevisionNumber) != previous.RevisionNumber)
			throw new BusinessException(FeeStructureErrorCodes.ConcurrencyConflict);
		var candidate = ToRequest(previous);
		await CheckAttachmentsAsync(candidate, null);
		var entity = new FeeStructure(GuidGenerator.Create(), CurrentTenant.Id, previous.FeeStructureMetadataId, previous.StandardId, previous.Name);
		entity.SetRevision(previous, siblings.Max(x => x.RevisionNumber) + 1);
		foreach (var line in candidate.FeeHeads) line.Id = null;
		ApplyCandidate(entity, candidate);
		await feeStructureRepository.InsertAsync(entity, autoSave: true);
		return await MapAsync(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<FeeStructurePreviewDto> PreviewAsync(FeeStructurePreviewRequest input)
	{
		var current = input.StructureId.HasValue ? await GetScopedAsync(input.StructureId.Value) : null;
		if (current != null && (current.StandardId != input.StandardId || current.FeeStructureMetadataId != input.FeeStructureMetadataId))
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		return await ValidateCandidateAsync(input.FeeStructureMetadataId, input.StandardId, input.StandardFeeStructures, current, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<List<AcademicYearMonthItem>> GetMonthsByAcademicYearAsync(Guid academicYearId)
	{
		var year = await academicYearRepository.GetAsync(academicYearId);
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		scopeAccess.EnsureHasAnyAccess(scope);
		if (!scope.IsAdmin && !scope.HasWildcardAcademicYear && !scope.AcademicYearIds.Contains(academicYearId))
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		var result = new List<AcademicYearMonthItem>();
		for (var cursor = new DateTime(year.StartDate.Year, year.StartDate.Month, 1); cursor <= year.EndDate; cursor = cursor.AddMonths(1))
			result.Add(new AcademicYearMonthItem { Month = (MonthEnum)(cursor.Month - 1), Year = cursor.Year });
		return result;
	}

	private static void EnsureStamp(FeeStructure entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || stamp != entity.ConcurrencyStamp)
			throw new BusinessException(FeeStructureErrorCodes.ConcurrencyConflict);
	}
	private async Task EnsureUnusedAsync(Guid id)
	{
		using var historicalReferences = dataFilter.Disable<ISoftDelete>();
		if (await studentStructureRepository.AnyAsync(x => x.FeeStructureStandardId == id))
			throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration)
				.WithData("Errors", new[] { "This structure is already referenced by student fees. Its configuration must remain unchanged." });
	}
	private static void ThrowIfInvalid(FeeStructurePreviewDto preview)
	{
		if (!preview.IsReady) throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration).WithData("Errors", preview.Errors);
	}
}
