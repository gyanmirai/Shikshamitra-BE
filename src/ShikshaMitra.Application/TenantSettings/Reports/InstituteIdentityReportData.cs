using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Reports;

[RemoteService(false)]
public partial class InstituteIdentityReportData(IRepository<SchoolIdentityProfile, Guid> profiles,
	IRepository<AcademicYear, Guid> years, IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationMediumStage, Guid> organizations,
	IUserScopeService scopes, InstituteReportAccess access) : ApplicationService
{
	public static List<InstituteReportValue> ScopeValues => [new("school", "School"), new("board", "Board"), new("medium", "Medium"), new("organization", "Organization")];
	private async Task<List<IdentityRow>> PermittedAsync(Guid user)
	{
		await access.EnsureAsync(user, "school-identity-register");
		var rows = await AsyncExecuter.ToListAsync(await QueryAsync(user));
		var names = await InstituteReportBoardNames.CustomAsync((await boards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id), rows.Select(x => x.Profile.EducationBoardId), AsyncExecuter);
		foreach (var row in rows)
			if (row.Profile.EducationBoardId.HasValue && names.TryGetValue(row.Profile.EducationBoardId.Value, out var name)) row.Board = name;
		return rows;
	}
	public async Task<Dictionary<string, List<InstituteReportOption>>> ChoicesAsync(Guid user)
	{
		var rows = await PermittedAsync(user);
		return new() {
			["academicYearId"] = Choice(x => x.Profile.AcademicYearId, x => x.Year),
			["boardId"] = Choice(x => x.Profile.EducationBoardId, x => x.Board, x => x.Profile.AcademicYearId),
			["mediumId"] = Choice(x => x.Profile.EducationMediumId, x => x.Medium, x => x.Profile.EducationBoardId),
			["stageId"] = Choice(x => x.Profile.OrganizationId, x => x.Organization, x => x.Profile.EducationMediumId)
		};
		List<InstituteReportOption> Choice(Func<IdentityRow, Guid?> id, Func<IdentityRow, string> label, Func<IdentityRow, Guid?>? parent = null) => rows.Where(x => id(x).HasValue)
			.Select(x => new InstituteReportOption(id(x)!.Value, label(x), parent?.Invoke(x))).DistinctBy(x => x.Id).ToList();
	}
	public async Task<List<Dictionary<string, string?>>> ReadAsync(InstituteReportRequest request, Guid user)
	{
		if (!InstituteReportCatalog.IsIdentity(request.ReportType)) throw new BusinessException("InstituteReports:InvalidReport");
		InstituteReportCatalog.Validate(request);
		await access.EnsureAsync(user, request.ReportType);
		var filter = request.Filters;
		var rows = (await PermittedAsync(user)).Where(x => (!filter.AcademicYearId.HasValue || x.Profile.AcademicYearId == filter.AcademicYearId)
			&& (!filter.BoardId.HasValue || x.Profile.EducationBoardId == filter.BoardId) && (!filter.MediumId.HasValue || x.Profile.EducationMediumId == filter.MediumId)
			&& (!filter.StageId.HasValue || x.Profile.OrganizationId == filter.StageId) && (filter.IdentityScope == null || Kind(x.Profile) == filter.IdentityScope)).ToList();
		if (request.ReportType == "school-identity-counts")
		{
			var result = new List<Dictionary<string, string?>> { Count("All scopes", rows) };
			foreach (var value in ScopeValues.Where(x => filter.IdentityScope == null || x.Value == filter.IdentityScope))
				result.Add(Count(value.Label, rows.Where(x => Kind(x.Profile) == value.Value).ToList()));
			return result;
		}
		return rows.OrderBy(x => x.Year).ThenBy(x => x.Board).ThenBy(x => x.Medium).ThenBy(x => x.Organization).Select(x => {
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
		}).ToList();
	}
	private static string Kind(SchoolIdentityProfile profile) => profile.OrganizationId.HasValue ? "organization" : profile.EducationMediumId.HasValue ? "medium" : profile.EducationBoardId.HasValue ? "board" : "school";
	private static Dictionary<string, string?> Count(string scope, List<IdentityRow> rows) => new() {
		["scope"] = scope, ["profileCount"] = Number(rows.Count),
		["customLogoCount"] = Number(rows.Count(x => x.Profile.Choices.Logo.Mode == SchoolIdentityAssetMode.Custom)),
		["customBannerCount"] = Number(rows.Count(x => x.Profile.Choices.Banner.Mode == SchoolIdentityAssetMode.Custom))
	};
	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
	private sealed class IdentityRow
	{
		public SchoolIdentityProfile Profile { get; set; } = null!;
		public string Year { get; set; } = "";
		public string Board { get; set; } = "";
		public string Medium { get; set; } = "";
		public string Organization { get; set; } = "";
	}
}
