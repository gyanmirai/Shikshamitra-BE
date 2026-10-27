using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteIdentityReportData
{
	private async Task<IQueryable<IdentityRow>> QueryAsync(Guid user)
	{
		var tenant = CurrentTenant.Id ?? throw new BusinessException("InstituteReports:TenantRequired");
		var scope = await scopes.GetUserScopeAsync(user);
		var yearIds = scope.WildcardBoardAcademicYearIds.ToArray();
		var boardIds = scope.BoardIds.ToArray(); var allMediums = scope.WildcardMediumBoardIds.ToArray();
		var mediumIds = scope.MediumIds.ToArray(); var allStages = scope.WildcardMediumStageMediumIds.ToArray();
		var stageIds = scope.MediumStageIds.ToArray(); var allStandards = scope.WildcardStandardMediumStageIds.ToArray();
		var pq = (await profiles.GetQueryableAsync()).Where(p => p.TenantId == tenant && (!p.EducationBoardId.HasValue || scope.IsAdmin || scope.HasWildcardAcademicYear
			|| (p.AcademicYearId.HasValue && Enumerable.Contains(yearIds, p.AcademicYearId.Value))
			|| Enumerable.Contains(allMediums, p.EducationBoardId.Value)
			|| (p.EducationMediumId.HasValue && Enumerable.Contains(allStages, p.EducationMediumId.Value))
			|| (p.OrganizationId.HasValue && (Enumerable.Contains(allStandards, p.OrganizationId.Value) || Enumerable.Contains(stageIds, p.OrganizationId.Value)))
			|| (!p.OrganizationId.HasValue && ((!p.EducationMediumId.HasValue && Enumerable.Contains(boardIds, p.EducationBoardId.Value))
				|| (p.EducationMediumId.HasValue && Enumerable.Contains(mediumIds, p.EducationMediumId.Value))))));
		var yq = (await years.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var bq = (await boards.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var mq = (await mediums.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		var oq = (await organizations.GetQueryableAsync()).Where(x => x.TenantId == tenant);
		return from profile in pq
			join year in yq on profile.AcademicYearId equals (Guid?)year.Id into yg from y in yg.DefaultIfEmpty()
			join board in bq on profile.EducationBoardId equals (Guid?)board.Id into bg from b in bg.DefaultIfEmpty()
			join medium in mq on profile.EducationMediumId equals (Guid?)medium.Id into mg from m in mg.DefaultIfEmpty()
			join organization in oq on profile.OrganizationId equals (Guid?)organization.Id into og from o in og.DefaultIfEmpty()
			where (!profile.EducationBoardId.HasValue || (b != null && b.AcademicYearId == profile.AcademicYearId))
				&& (!profile.EducationMediumId.HasValue || (m != null && m.EducationBoardId == profile.EducationBoardId))
				&& (!profile.OrganizationId.HasValue || (o != null && o.EducationMediumId == profile.EducationMediumId))
			select new IdentityRow { Profile = profile, Year = y == null ? "" : y.Name,
				Board = b == null ? "" : b.Type == EducationBoardType.CBSE ? "CBSE" : "GSEB", Medium = m == null ? "" : m.Name, Organization = o == null ? "" : o.Name };
	}
	public async Task<string?> FilterNameAsync(Guid user, string field, Guid id)
	{
		await access.EnsureAsync(user, "school-identity-register");
		var q = await QueryAsync(user);
		var names = field switch {
			nameof(InstituteReportFilters.AcademicYearId) => q.Where(x => x.Profile.AcademicYearId == id).Select(x => x.Year),
			nameof(InstituteReportFilters.BoardId) => q.Where(x => x.Profile.EducationBoardId == id).Select(x => x.Board),
			nameof(InstituteReportFilters.MediumId) => q.Where(x => x.Profile.EducationMediumId == id).Select(x => x.Medium),
			nameof(InstituteReportFilters.StageId) => q.Where(x => x.Profile.OrganizationId == id).Select(x => x.Organization),
			_ => throw new BusinessException("InstituteReports:InvalidRequest") };
		var name = await AsyncExecuter.FirstOrDefaultAsync(names);
		if (name != null && field == nameof(InstituteReportFilters.BoardId))
			name = (await InstituteReportBoardNames.CustomAsync((await boards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id), [id], AsyncExecuter)).GetValueOrDefault(id, name);
		return name;
	}
	public async Task<(List<Dictionary<string, string?>> Rows, int Count)?> PageAsync(InstituteReportRequest request, Guid user, bool all)
	{
		await access.EnsureAsync(user, request.ReportType);
		if (request.ReportType == "school-identity-counts")
		{
			var rows = InstituteReportProjection.Apply(await ReadAsync(request, user), request).ToList();
			return (all ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(), rows.Count);
		}
		var f = request.Filters;
		var query = (await QueryAsync(user)).Where(x => (!f.AcademicYearId.HasValue || x.Profile.AcademicYearId == f.AcademicYearId)
			&& (!f.BoardId.HasValue || x.Profile.EducationBoardId == f.BoardId) && (!f.MediumId.HasValue || x.Profile.EducationMediumId == f.MediumId)
			&& (!f.StageId.HasValue || x.Profile.OrganizationId == f.StageId)
			&& (f.IdentityScope == null || (x.Profile.OrganizationId.HasValue ? "organization" : x.Profile.EducationMediumId.HasValue ? "medium" : x.Profile.EducationBoardId.HasValue ? "board" : "school") == f.IdentityScope));
		var boardIds = await AsyncExecuter.ToListAsync(query.Select(x => x.Profile.EducationBoardId).Distinct());
		var customNames = await InstituteReportBoardNames.CustomAsync((await boards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id), boardIds, AsyncExecuter);
		if (customNames.Count != 0) return null;
		var page = await InstituteReportQuery.PageAsync(query, request, all, AsyncExecuter,
			q => q.OrderBy(x => x.Year).ThenBy(x => x.Board).ThenBy(x => x.Medium).ThenBy(x => x.Organization).ThenBy(x => x.Profile.Id),
			new() { ["year"] = x => x.Year, ["board"] = x => x.Board, ["medium"] = x => x.Medium, ["organization"] = x => x.Organization,
				["scope"] = x => x.Profile.OrganizationId.HasValue ? "Organization" : x.Profile.EducationMediumId.HasValue ? "Medium" : x.Profile.EducationBoardId.HasValue ? "Board" : "School" });
		if (page == null) return null;
		return (page.Value.Items.Select(x => {
			var row = new Dictionary<string, string?> { ["scope"] = ScopeValues.Find(v => v.Value == Kind(x.Profile))!.Label,
				["year"] = x.Year, ["board"] = x.Board, ["medium"] = x.Medium, ["organization"] = x.Organization };
			var choices = x.Profile.Choices;
			if (request.ReportType == "school-identity-signatures") {
				row["signatureMode"] = choices.Signature.Mode.ToString(); row["principalName"] = choices.Signature.PrincipalName; row["designation"] = choices.Signature.Designation;
			} else {
				row["logoMode"] = choices.Logo.Mode.ToString(); row["bannerMode"] = choices.Banner.Mode.ToString();
				row["createdAt"] = x.Profile.CreationTime.ToString("u", CultureInfo.InvariantCulture); row["updatedAt"] = x.Profile.LastModificationTime?.ToString("u", CultureInfo.InvariantCulture);
			}
			return row;
		}).ToList(), page.Value.Count);
	}
}
