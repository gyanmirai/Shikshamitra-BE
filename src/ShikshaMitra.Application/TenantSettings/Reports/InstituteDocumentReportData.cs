using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolDocuments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Reports;

[RemoteService(false)]
public partial class InstituteDocumentReportData(IRepository<SchoolDocument, Guid> documents,
	IRepository<SchoolDocumentFile, Guid> files, InstituteReportAccess access) : ApplicationService
{
	public static List<InstituteReportValue> Categories => Enum.GetValues<SchoolDocumentCategory>()
		.Select(x => new InstituteReportValue(x.ToString(), Label(x))).ToList();
	public async Task<List<Dictionary<string, string?>>> ReadAsync(InstituteReportRequest request, Guid user)
	{
		if (!InstituteReportCatalog.IsDocuments(request.ReportType)) throw new BusinessException("InstituteReports:InvalidReport");
		InstituteReportCatalog.Validate(request);
		await access.EnsureAsync(user, request.ReportType);
		var tenant = CurrentTenant.Id ?? throw new BusinessException("InstituteReports:TenantRequired");
		var category = request.Filters.DocumentCategory == null ? (SchoolDocumentCategory?)null : Enum.Parse<SchoolDocumentCategory>(request.Filters.DocumentCategory);
		if (request.ReportType == "school-document-counts") return await CountsAsync(category);
		var rows = await documents.GetListAsync(x => x.TenantId == tenant && (!category.HasValue || x.Category == category));
		var ids = rows.Select(x => x.Id).ToArray();
		var attachments = ids.Length == 0 ? [] : await files.GetListAsync(x => x.TenantId == tenant && Enumerable.Contains(ids, x.DocumentId));
		var lookup = attachments.ToLookup(x => x.DocumentId);
		return rows.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(document => {
			var own = lookup[document.Id].ToList();
			return new Dictionary<string, string?> {
				["name"] = document.Name, ["category"] = Label(document.Category), ["notes"] = document.Notes,
				["attachmentCount"] = Number(own.Count), ["sizeBytes"] = Number(own.Sum(x => x.Size)),
				["fileNames"] = string.Join(", ", own.OrderBy(x => x.CreationTime).Select(x => x.FileName)),
				["uploadedBy"] = document.UploadedBy, ["createdAt"] = document.CreationTime.ToString("u", CultureInfo.InvariantCulture)
			};
		}).ToList();
	}
	private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
	private static string Label(SchoolDocumentCategory value) => value switch {
		SchoolDocumentCategory.GovernmentApproval => "Government approval", SchoolDocumentCategory.PermissionNoc => "Permission / NOC",
		SchoolDocumentCategory.BoardAffiliation => "Board affiliation", SchoolDocumentCategory.Registration => "Registration", _ => "Other"
	};
}
