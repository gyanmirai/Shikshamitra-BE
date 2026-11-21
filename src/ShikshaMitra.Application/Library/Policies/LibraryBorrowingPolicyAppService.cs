using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Policies;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View)]
public partial class LibraryBorrowingPolicyAppService(IRepository<LibraryBorrowingPolicyVersion, Guid> policies,
	ILibraryAccessService access, ILibraryInventoryLock inventoryLock, LibraryPolicyTargetAccess targetAccess,
	LibraryPolicyContextReader contextReader, LibraryBorrowingPolicyResolver resolver,
	LibraryBorrowingPolicyPublisher publisher, LibraryCalendarService calendar) : ApplicationService, ILibraryBorrowingPolicyAppService
{
	public async Task<PaginatedResponse<LibraryBorrowingPolicyVersionDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 20)
	{
		var context = await access.GetContextAsync(academicYearId);
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var query = await targetAccess.ApplyReadScopeAsync(await policies.GetQueryableAsync(), context);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		return new()
		{
			TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
			Items = ObjectMapper.Map<List<LibraryBorrowingPolicyVersion>, List<LibraryBorrowingPolicyVersionDto>>(rows)
		};
	}

	public async Task<LibraryBorrowingPolicyVersionDto> GetAsync(Guid versionId)
	{
		var context = await access.GetContextAsync();
		var policy = await FindAsync(versionId);
		await targetAccess.EnsureReadableAsync(policy, context);
		return ObjectMapper.Map<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryBorrowingPolicyVersionDto> CreateAsync(Guid academicYearId, LibraryBorrowingPolicyDraftRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var definition = await targetAccess.ValidateAsync(academicYearId,
			ObjectMapper.Map<LibraryBorrowingPolicyDraftRequest, LibraryBorrowingPolicyDefinition>(input));
		var policyId = input.PolicyId ?? GuidGenerator.Create();
		var family = await policies.GetListAsync(x => x.PolicyId == policyId);
		if (input.PolicyId.HasValue && family.Count == 0) throw new BusinessException(LibraryErrorCodes.PolicyNotFound);
		if (family.Count > 0)
		{
			var latest = family.MaxBy(x => x.Version)!;
			await targetAccess.ValidateAsync(latest.OriginAcademicYearId, Definition(latest));
		}
		if (family.Any(x => x.Role != definition.Role)) throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "Versions of a policy must retain the same borrower role.");
		var version = family.Count == 0 ? 1 : checked(family.Max(x => x.Version) + 1);
		var policy = new LibraryBorrowingPolicyVersion(GuidGenerator.Create(), CurrentTenant.Id, academicYearId, policyId, version, definition);
		await policies.InsertAsync(policy, true);
		return ObjectMapper.Map<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add)]
	public async Task<LibraryBorrowingPolicyVersionDto> UpdateAsync(Guid versionId, LibraryBorrowingPolicyDraftRequest input)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var policy = await FindAsync(versionId);
		await targetAccess.ValidateAsync(policy.OriginAcademicYearId, Definition(policy));
		if (input.PolicyId.HasValue && input.PolicyId != policy.PolicyId) throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		var definition = await targetAccess.ValidateAsync(policy.OriginAcademicYearId,
			ObjectMapper.Map<LibraryBorrowingPolicyDraftRequest, LibraryBorrowingPolicyDefinition>(input));
		if (await policies.AnyAsync(x => x.PolicyId == policy.PolicyId && x.Id != policy.Id && x.Role != definition.Role))
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		policy.UpdateDraft(definition, input.ConcurrencyStamp);
		await policies.UpdateAsync(policy, true);
		return ObjectMapper.Map<LibraryBorrowingPolicyVersion, LibraryBorrowingPolicyVersionDto>(policy);
	}

	public async Task<LibraryPolicyOptionsDto> GetOptionsAsync(Guid academicYearId)
	{
		var result = await targetAccess.GetOptionsAsync(academicYearId);
		result.CanManageGlobalPolicies = (await access.GetContextAsync(academicYearId)).UserScope.HasTenantWideScope() &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add);
		return result;
	}

	private async Task<LibraryBorrowingPolicyVersion> FindAsync(Guid versionId) =>
		await policies.FindAsync(versionId) ?? throw new BusinessException(LibraryErrorCodes.PolicyNotFound);
}
