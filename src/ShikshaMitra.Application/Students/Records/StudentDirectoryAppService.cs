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
	StudentScopeAccessService scope,
	StudentRecordsAccessService access,
	IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentRecordLegacyOverride, Guid> overrides,
	StudentRecordsPolicyResolver policies, StudentRecordsEnrollmentScopeService enrollmentScope,
	IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid> divisions, StudentDirectoryOptionsService options) : ApplicationService, IStudentDirectoryAppService
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

	private async Task<PagedResultDto<StudentDirectoryItemDto>> GetDirectoryAsync(StudentDirectoryRequest input, bool legacyCandidates)
	{
		EnsureTenant();
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Population is not ("All" or "Active" or "Former"))
			throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		if (input.BoardIds.Count > 100 || input.MediumIds.Count > 100 || input.StandardIds.Count > 100 || input.DivisionIds.Count > 100 ||
			input.BoardIds.Contains(Guid.Empty) || input.MediumIds.Contains(Guid.Empty) || input.StandardIds.Contains(Guid.Empty) || input.DivisionIds.Contains(Guid.Empty))
			throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		var query = await enrollmentScope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		if (input.StudentId.HasValue) query = query.Where(s => s.Id == input.StudentId);
		var caseQuery = await cases.GetQueryableAsync();
        var onboardingEligibleIds = await access.GetOnboardingEligibleStudentIdsAsync();
		var overrideQuery = await overrides.GetQueryableAsync();
		query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled || s.Status == StudentLifeCycleStatusEnum.InActive || s.Status == StudentLifeCycleStatusEnum.Left);
		if (legacyCandidates)
		{
			var enrollmentQuery = await enrollments.GetQueryableAsync();
			query = query.Where(s => (enrollmentQuery.Any(e => e.TenantId == CurrentTenant.Id && e.StudentId == s.Id) || s.Status == StudentLifeCycleStatusEnum.Left && s.AcademicYearId.HasValue) &&
				!caseQuery.Any(c => c.TenantId == CurrentTenant.Id && c.StudentId == s.Id && (c.Status == StudentOnboardingStatus.Completed || !c.IsHistoricalBackfill)) &&
				!overrideQuery.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == s.Id && (o.Status == "Pending" || o.Status == "Approved")));
		}
		else query = query.Where(s => input.IncludeActiveStudentList && s.Status == StudentLifeCycleStatusEnum.Enrolled || onboardingEligibleIds.Contains(s.Id) ||
			 overrideQuery.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == s.Id && o.Status == "Approved"));
		if (input.Population == "Active") query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled);
		if (input.Population == "Former") query = query.Where(s => s.Status == StudentLifeCycleStatusEnum.InActive || s.Status == StudentLifeCycleStatusEnum.Left);
		if (input.AcademicYearId.HasValue && input.IncludeActiveStudentList && input.Population == "Active")
		{
			query = query.Where(s => s.AcademicYearId == input.AcademicYearId &&
				(!input.BoardId.HasValue || s.AcademicBoardId == input.BoardId) && (!input.MediumId.HasValue || s.AcademicMediumId == input.MediumId) &&
				(!input.AcademicMediumStageId.HasValue || s.AcademicMediumStageId == input.AcademicMediumStageId) &&
				(!input.StandardId.HasValue || s.StandardId == input.StandardId) && (!input.DivisionId.HasValue || s.DivisionId == input.DivisionId));
		}
		else if (input.AcademicYearId.HasValue)
		{
			var enrollmentQuery = await enrollmentScope.FilterHierarchyAsync(await enrollmentScope.GetQueryableAsync(), input.BoardId, input.MediumId, input.AcademicMediumStageId);
			var allEnrollmentQuery = await enrollments.GetQueryableAsync();
			query = query.Where(s => enrollmentQuery.Any(e => e.TenantId == CurrentTenant.Id && e.StudentId == s.Id && e.AcademicYearId == input.AcademicYearId &&
				(!input.StandardId.HasValue || e.StandardId == input.StandardId) && (!input.DivisionId.HasValue || e.DivisionId == input.DivisionId)) ||
				((input.IncludeActiveStudentList && s.Status == StudentLifeCycleStatusEnum.Enrolled) ||
				 !allEnrollmentQuery.Any(e => e.TenantId == CurrentTenant.Id && e.StudentId == s.Id) && s.Status == StudentLifeCycleStatusEnum.Left) && s.AcademicYearId == input.AcademicYearId &&
				(!input.BoardId.HasValue || s.AcademicBoardId == input.BoardId) && (!input.MediumId.HasValue || s.AcademicMediumId == input.MediumId) &&
				(!input.AcademicMediumStageId.HasValue || s.AcademicMediumStageId == input.AcademicMediumStageId) &&
				(!input.StandardId.HasValue || s.StandardId == input.StandardId) && (!input.DivisionId.HasValue || s.DivisionId == input.DivisionId));
		}
		else
		{
			if (input.AcademicMediumStageId.HasValue) query = query.Where(s => s.AcademicMediumStageId == input.AcademicMediumStageId);
			if (input.StandardId.HasValue) query = query.Where(s => s.StandardId == input.StandardId);
			if (input.DivisionId.HasValue) query = query.Where(s => s.DivisionId == input.DivisionId);
		}
		if (!input.AcademicYearId.HasValue)
		{
			if (input.BoardId.HasValue) query = query.Where(s => s.AcademicBoardId == input.BoardId);
			if (input.MediumId.HasValue) query = query.Where(s => s.AcademicMediumId == input.MediumId);
		}
		if (input.BoardIds.Count > 0) query = query.Where(s => s.AcademicBoardId.HasValue && input.BoardIds.Contains(s.AcademicBoardId.Value));
		if (input.MediumIds.Count > 0) query = query.Where(s => input.MediumIds.Contains(s.AcademicMediumId));
		if (input.StandardIds.Count > 0) query = query.Where(s => s.StandardId.HasValue && input.StandardIds.Contains(s.StandardId.Value));
		if (input.DivisionIds.Count > 0) query = query.Where(s => s.DivisionId.HasValue && input.DivisionIds.Contains(s.DivisionId.Value));
		var roles = await access.GetDatabaseRoleNamesAsync();
		var years = await AsyncExecuter.ToListAsync(query.Select(s => s.AcademicYearId).Distinct());
		var yearPolicies = new Dictionary<Guid, StudentRecordsPolicy>();
		var defaultPolicy = (await policies.ResolveAsync(null)).Policy;
		foreach (var year in years.Where(y => y.HasValue)) yearPolicies[year!.Value] = (await policies.ResolveAsync(year.Value)).Policy;
		if (input.AcademicYearId.HasValue && !yearPolicies.ContainsKey(input.AcademicYearId.Value))
			yearPolicies[input.AcademicYearId.Value] = (await policies.ResolveAsync(input.AcademicYearId.Value)).Policy;
		var visibleIdentityYears = yearPolicies.Where(p => !p.Value.IsMasked("Identity", roles)).Select(p => p.Key).ToArray();
		var showNullYear = !defaultPolicy.IsMasked("Identity", roles);
		var selectedYearVisible = !input.AcademicYearId.HasValue || !yearPolicies[input.AcademicYearId.Value].IsMasked("Identity", roles);
		var search = input.Search?.Trim();
		if (search?.Length > 150) throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		if (!string.IsNullOrEmpty(search))
		{
			var term = search.ToLower();
			query = query.Where(s => (input.AcademicYearId.HasValue ? selectedYearVisible :
				((s.AcademicYearId.HasValue && Enumerable.Contains(visibleIdentityYears, s.AcademicYearId.Value)) || (!s.AcademicYearId.HasValue && showNullYear))) &&
				((s.FirstName != null && s.FirstName.ToLower().Contains(term)) || (s.LastName != null && s.LastName.ToLower().Contains(term)) ||
				 (s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) || (s.GrNumber != null && s.GrNumber.ToLower().Contains(term)) ||
				 (s.GrNo.HasValue && s.GrNo.Value.ToString().Contains(term))));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var sorting = input.Sorting?.Trim().ToLowerInvariant();
		if (!selectedYearVisible || (!input.AcademicYearId.HasValue && (visibleIdentityYears.Length < yearPolicies.Count || (!showNullYear && years.Any(y => !y.HasValue)))))
		{
			// Sorting on hidden names/register numbers would still disclose their relative order.
			if (sorting is null or "" or "name asc" or "name desc" or "grnumber asc" or "grnumber desc") sorting = "identity masked";
		}
		query = sorting switch
		{
			null or "" or "name asc" => query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ThenBy(s => s.Id),
			"name desc" => query.OrderByDescending(s => s.FirstName).ThenByDescending(s => s.LastName).ThenBy(s => s.Id),
			"grnumber asc" => query.OrderBy(s => s.GrNumber).ThenBy(s => s.Id),
			"grnumber desc" => query.OrderByDescending(s => s.GrNumber).ThenBy(s => s.Id),
			"creationtime asc" => query.OrderBy(s => s.CreationTime).ThenBy(s => s.Id),
			"creationtime desc" => query.OrderByDescending(s => s.CreationTime).ThenBy(s => s.Id),
			"identity masked" => query.OrderBy(s => s.Id),
			_ => throw new BusinessException("Students:Records:InvalidDirectorySort")
		};
		var rows = await AsyncExecuter.ToListAsync(query.Skip(input.SkipCount).Take(input.MaxResultCount).Select(s => new StudentDirectoryItemDto
		{
			Id = s.Id, StudentId = s.Id, FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName,
			GrNumber = s.GrNumber, GrNo = s.GrNo, RollNo = s.RollNo, Status = s.Status, AcademicYearId = s.AcademicYearId,
			StandardId = s.StandardId, StandardName = s.Standard != null ? s.Standard.Name : null,
			DivisionId = s.DivisionId, DivisionName = s.Division != null ? s.Division.Name : null,
			AcademicMediumId = s.AcademicMediumId, AcademicMediumName = s.AcademicMedium != null ? s.AcademicMedium.Name : null,
			HasPhoto = s.PhotoBlobName != null,
			EntrySource = caseQuery.Any(c => c.TenantId == CurrentTenant.Id && c.StudentId == s.Id && c.Status == StudentOnboardingStatus.Completed) ? "CompletedOnboarding" : onboardingEligibleIds.Contains(s.Id) ? "ActivatedOnboarding" :
				overrideQuery.Any(o => o.TenantId == CurrentTenant.Id && o.StudentId == s.Id && o.Status == "Approved") ? "LegacyOverride" : "ActiveStudentList"
		}));
		if (input.AcademicYearId.HasValue && rows.Count > 0 && !(input.IncludeActiveStudentList && input.Population == "Active"))
		{
			var studentIds = rows.Select(r => r.StudentId).ToArray();
			var enrollmentQuery = await enrollmentScope.GetDirectoryProjectionAsync();
			var divisionQuery = await divisions.GetQueryableAsync();
			var placements = await AsyncExecuter.ToListAsync(from p in enrollmentQuery
				where p.Enrollment.AcademicYearId == input.AcademicYearId && Enumerable.Contains(studentIds, p.Enrollment.StudentId)
				join d in divisionQuery.Where(d => d.TenantId == CurrentTenant.Id) on p.Enrollment.DivisionId equals (Guid?)d.Id into divisionRows
				from d in divisionRows.DefaultIfEmpty()
				select new { p.Enrollment, p.StandardName, p.MediumId, p.MediumName, DivisionName = d != null ? d.Name : null });
			foreach (var row in rows)
			{
				var placement = placements.FirstOrDefault(p => p.Enrollment.StudentId == row.StudentId);
				if (placement == null) continue;
				row.AcademicYearId = placement.Enrollment.AcademicYearId;
				row.StandardId = placement.Enrollment.StandardId; row.StandardName = placement.StandardName;
				row.DivisionId = placement.Enrollment.DivisionId; row.DivisionName = placement.DivisionName;
				row.RollNo = placement.Enrollment.RollNumber; row.AcademicMediumId = placement.MediumId; row.AcademicMediumName = placement.MediumName;
			}
		}
		foreach (var row in rows)
		{
			if (legacyCandidates) row.EntrySource = "LegacyNeedsApproval";
			var policy = row.AcademicYearId.HasValue && yearPolicies.TryGetValue(row.AcademicYearId.Value, out var rowPolicy) ? rowPolicy :
				(await policies.ResolveAsync(row.AcademicYearId)).Policy;
			if (!policy.IsMasked("Identity", roles)) continue;
			row.FirstName = row.MiddleName = row.LastName = row.GrNumber = null;
			row.GrNo = null; row.HasPhoto = false; row.MaskedFields.Add("Identity");
		}
		return new(total, rows);
	}

	private void EnsureTenant()
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired");
	}
}
