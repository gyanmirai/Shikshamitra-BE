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
public class InstituteDocumentReportData(IRepository<SchoolDocument, Guid> documents,
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
		var rows = await documents.GetListAsync(x => x.TenantId == tenant && (!category.HasValue || x.Category == category));
		var ids = rows.Select(x => x.Id).ToArray();
		var attachments = ids.Length == 0 ? [] : await files.GetListAsync(x => x.TenantId == tenant && Enumerable.Contains(ids, x.DocumentId));
		if (request.ReportType == "school-document-counts")
		{
			var result = new List<Dictionary<string, string?>> { Count("All categories", rows, attachments) };
			foreach (var item in Enum.GetValues<SchoolDocumentCategory>().Where(x => !category.HasValue || x == category))
			{
				var selected = rows.Where(x => x.Category == item).ToList(); var selectedIds = selected.Select(x => x.Id).ToHashSet();
				result.Add(Count(Label(item), selected, attachments.Where(x => selectedIds.Contains(x.DocumentId))));
			}
			return result;
		}
		return rows.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(document => {
			var own = attachments.Where(x => x.DocumentId == document.Id).ToList();
			return new Dictionary<string, string?> {
				["name"] = document.Name, ["category"] = Label(document.Category), ["notes"] = document.Notes,
				["attachmentCount"] = Number(own.Count), ["sizeBytes"] = Number(own.Sum(x => x.Size)),
				["fileNames"] = string.Join(", ", own.OrderBy(x => x.CreationTime).Select(x => x.FileName)),
				["uploadedBy"] = document.UploadedBy, ["createdAt"] = document.CreationTime.ToString("u", CultureInfo.InvariantCulture)
			};
		}).ToList();
	}
	private static Dictionary<string, string?> Count(string category, List<SchoolDocument> documents, IEnumerable<SchoolDocumentFile> files)
	{
		var list = files.ToList();
		return new() { ["category"] = category, ["documentCount"] = Number(documents.Count), ["attachmentCount"] = Number(list.Count), ["sizeBytes"] = Number(list.Sum(x => x.Size)) };
	}
	private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
	private static string Label(SchoolDocumentCategory value) => value switch {
		SchoolDocumentCategory.GovernmentApproval => "Government approval", SchoolDocumentCategory.PermissionNoc => "Permission / NOC",
		SchoolDocumentCategory.BoardAffiliation => "Board affiliation", SchoolDocumentCategory.Registration => "Registration", _ => "Other"
	};
}
