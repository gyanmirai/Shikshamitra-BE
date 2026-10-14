using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize]
public partial class StudentDirectoryAppService(
	IStudentRepository students,
	StudentRecordsAccessService access,
	IRepository<StudentRecordLegacyOverride, Guid> overrides,
	StudentRecordsPolicyResolver policies, StudentRecordsEnrollmentScopeService enrollmentScope,
	StudentDirectoryOptionsService options, StudentDirectoryQueryService directory) : ApplicationService, IStudentDirectoryAppService
{
	public async Task<StudentDirectoryOptionsDto> GetOptionsAsync(Guid? academicYearId = null)
	{
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View))
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		return await options.GetAsync(academicYearId);
	}
	public async Task<PagedResultDto<StudentDirectoryItemDto>> GetListAsync(StudentDirectoryRequest input)
	{
		if (input.IncludeActiveStudentList)
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		else if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View))
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View);
		return await GetDirectoryAsync(input, false);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request)]
	public async Task<PagedResultDto<StudentDirectoryItemDto>> GetLegacyCandidatesAsync(StudentDirectoryRequest input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request);
		return await GetDirectoryAsync(input, true);
	}

	private Task<PagedResultDto<StudentDirectoryItemDto>> GetDirectoryAsync(StudentDirectoryRequest input, bool legacyCandidates) =>
		directory.GetAsync(input, legacyCandidates);

	private void EnsureTenant()
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
	}
}
