using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.Reports;

public static class InstituteReportCatalog
{
	public static IReadOnlyList<InstituteReportDefinition> All { get; } =
	[
		Define("academic-hierarchy", "Academic hierarchy", true, "year:Academic year", "board:Board", "medium:Medium", "stage:Stage", "standard:Standard", "division:Division"),
		Define("class-division-summary", "Class division summary", true, "year:Academic year", "board:Board", "medium:Medium", "stage:Stage", "standard:Standard", "divisionCount:Permitted division count", "divisions:Permitted divisions"),
		Define("division-capacity", "Division capacity and room mapping", true, "year:Academic year", "board:Board", "medium:Medium", "stage:Stage", "standard:Standard", "division:Division", "capacity:Configured capacity", "room:Room", "roomAssignment:Room assignment", "shift:Shift"),
		Define("rooms-inventory", "Rooms and buildings inventory", false, "building:Building", "floor:Floor", "room:Room", "roomType:Room type", "capacity:Capacity", "benches:Benches", "benchCapacity:Seats per bench", "features:Features"),
		Define("shift-register", "Shift register", false, "shift:Shift", "start:Start time", "end:End time", "totalHours:Total hours"),
		Define("school-document-register", "School document register", false, "name:Document name", "category:Category", "attachmentCount:Attachment count", "sizeBytes:Total size (bytes)", "fileNames:File names", "uploadedBy:Uploaded by", "createdAt:Created at", "notes:Notes"),
		Define("school-document-counts", "School document counts", false, "category:Category", "documentCount:Document count", "attachmentCount:Attachment count", "sizeBytes:Total size (bytes)"),
		Define("school-identity-register", "School identity configuration", false, "scope:Scope", "year:Academic year", "board:Board", "medium:Medium", "organization:Organization", "logoMode:Logo selection", "bannerMode:Letterhead selection", "createdAt:Created at", "updatedAt:Updated at"),
		Define("school-identity-counts", "School identity profile counts", false, "scope:Scope", "profileCount:Configured profiles", "customLogoCount:Custom logo selections", "customBannerCount:Custom letterhead selections"),
		Define("school-identity-signatures", "School identity signature configuration", false, "scope:Scope", "year:Academic year", "board:Board", "medium:Medium", "organization:Organization", "signatureMode:Signature selection", "principalName:Principal name", "designation:Designation")
	];
	private static InstituteReportDefinition Define(string id, string label, bool academic, params string[] columns) =>
		new(id, label, academic, columns.Select(x => new AcademicReportColumnDto { Id = x.Split(':')[0], Label = x.Split(':')[1] }).ToList()) { RequiredPermissions = SourcePermissions(id) };
	public static InstituteReportDefinition Get(string id) => All.FirstOrDefault(x => x.Id == id)
		?? throw new BusinessException("InstituteReports:InvalidReport", "Choose an available Institute Setup report.");
	public static bool IsIdentity(string id) => id is "school-identity-register" or "school-identity-counts" or "school-identity-signatures";
	public static bool IsDocuments(string id) => id is "school-document-register" or "school-document-counts";
	public static bool RequiresScopeStamp(string id) => Get(id).Academic || IsIdentity(id);
	public static bool IsNumeric(string field) => field is "capacity" or "benches" or "benchCapacity" or "divisionCount" or "totalHours" or "documentCount" or "attachmentCount" or "sizeBytes" or "profileCount" or "customLogoCount" or "customBannerCount";
	public static string[] SourcePermissions(string id) => id switch
	{
		"academic-hierarchy" => [ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View],
		"class-division-summary" => [ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View],
		"division-capacity" => [ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View, ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View, ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.View],
		"rooms-inventory" => [ShikshaMitraSubmodulePermissions.InstituteSetup.RoomManagement.View, ShikshaMitraSubmodulePermissions.InstituteSetup.BuildingManagement.View, ShikshaMitraSubmodulePermissions.InstituteSetup.RoomTypeManagement.View],
		"shift-register" => [ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.View],
		"school-document-register" or "school-document-counts" => [SchoolDocumentPermissions.View],
		"school-identity-register" or "school-identity-counts" => [SchoolIdentityPermissions.View],
		"school-identity-signatures" => [SchoolIdentityPermissions.View, SchoolIdentityPermissions.AccessSignature],
		_ => throw new BusinessException("InstituteReports:InvalidReport")
	};
	public static void Validate(InstituteReportRequest request)
	{
		var definition = Get(request.ReportType);
		if (request.Filters == null || request.Columns == null || request.Conditions == null || request.Format is not ("pdf" or "excel" or "csv") || request.Page < 1 || request.Page > 100000 || request.PageSize is < 1 or > 100)
			throw Invalid();
		if (definition.Academic && !request.Filters.AcademicYearId.HasValue)
			throw new BusinessException("InstituteReports:YearRequired", "Select an academic year for this report.");
		var fields = definition.Columns.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
		if (request.Columns.Count != request.Columns.Distinct().Count() || request.Columns.Any(x => !fields.Contains(x)) ||
			(request.SortField != null && !fields.Contains(request.SortField)) || request.Conditions.Count > 12 ||
			request.Conditions.Any(x => !fields.Contains(x.Field) || x.Operator is not ("eq" or "contains" or "gt" or "lt") || x.Value == null || x.Value.Length > 200)) throw Invalid();
		if (request.Conditions.Any(x => x.Operator is "gt" or "lt" && !IsNumeric(x.Field))) throw Invalid();
		if (request.ReportType == "class-division-summary" && request.Filters.DivisionId.HasValue) throw Invalid();
		if (request.Filters.RoomAssignment is not (null or "assigned" or "unassigned" or "unresolved")) throw Invalid();
		if (request.ReportType != "division-capacity" && request.Filters.RoomAssignment != null) throw Invalid();
		if (!definition.Academic && !IsIdentity(request.ReportType) && (request.Filters.AcademicYearId.HasValue || request.Filters.BoardId.HasValue || request.Filters.MediumId.HasValue || request.Filters.StageId.HasValue || request.Filters.StandardId.HasValue || request.Filters.DivisionId.HasValue)) throw Invalid();
		if (IsIdentity(request.ReportType) && (request.Filters.StandardId.HasValue || request.Filters.DivisionId.HasValue)) throw Invalid();
		if (request.Filters.DocumentCategory != null && (!IsDocuments(request.ReportType) || !Enum.GetNames<SchoolDocumentCategory>().Contains(request.Filters.DocumentCategory))) throw Invalid();
		if (request.Filters.IdentityScope != null && (!IsIdentity(request.ReportType) || request.Filters.IdentityScope is not ("school" or "board" or "medium" or "organization"))) throw Invalid();
		if (request.ReportType != "rooms-inventory" && (request.Filters.BuildingId.HasValue || request.Filters.FloorId.HasValue || request.Filters.RoomTypeId.HasValue)) throw Invalid();
		if (request.ReportType is not ("division-capacity" or "shift-register") && request.Filters.ShiftId.HasValue) throw Invalid();
	}
	private static BusinessException Invalid() => new("InstituteReports:InvalidRequest", "The report contains unsupported fields or filters.");
}
