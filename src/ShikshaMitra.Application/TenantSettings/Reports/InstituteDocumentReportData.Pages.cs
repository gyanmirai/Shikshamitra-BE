using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolDocuments;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteDocumentReportData
{
	public async Task<(List<Dictionary<string, string?>> Rows, int Count)?> PageAsync(InstituteReportRequest request, Guid user, bool all)
	{
		await access.EnsureAsync(user, request.ReportType);
		if (request.ReportType == "school-document-counts")
		{
			var rows = InstituteReportProjection.Apply(await ReadAsync(request, user), request).ToList();
			return (all ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList(), rows.Count);
		}
		var category = request.Filters.DocumentCategory == null ? (SchoolDocumentCategory?)null : Enum.Parse<SchoolDocumentCategory>(request.Filters.DocumentCategory);
		var dq = (await documents.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && (!category.HasValue || x.Category == category));
		var fq = (await files.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		var query = dq.Select(x => new DocumentRow {
			Id = x.Id, Name = x.Name, Notes = x.Notes, UploadedBy = x.UploadedBy, CreatedAt = x.CreationTime,
			Category = x.Category == SchoolDocumentCategory.GovernmentApproval ? "Government approval" : x.Category == SchoolDocumentCategory.PermissionNoc ? "Permission / NOC"
				: x.Category == SchoolDocumentCategory.BoardAffiliation ? "Board affiliation" : x.Category == SchoolDocumentCategory.Registration ? "Registration" : "Other",
			Count = fq.Count(file => file.DocumentId == x.Id), Size = fq.Where(file => file.DocumentId == x.Id).Select(file => (long?)file.Size).Sum() ?? 0
		});
		var page = await InstituteReportQuery.PageAsync(query, request, all, AsyncExecuter, q => q.OrderBy(x => x.Name).ThenBy(x => x.Id),
			new() { ["name"] = x => x.Name, ["category"] = x => x.Category, ["notes"] = x => x.Notes, ["uploadedBy"] = x => x.UploadedBy },
			new() { ["attachmentCount"] = x => x.Count, ["sizeBytes"] = x => x.Size });
		if (page == null) return null;
		var ids = page.Value.Items.Select(x => x.Id).ToArray();
		var attachments = await AsyncExecuter.ToListAsync(fq.Where(x => Enumerable.Contains(ids, x.DocumentId)).OrderBy(x => x.CreationTime)
			.Select(x => new { x.DocumentId, x.FileName }));
		var lookup = attachments.ToLookup(x => x.DocumentId);
		return (page.Value.Items.Select(x => new Dictionary<string, string?> {
			["name"] = x.Name, ["category"] = x.Category, ["notes"] = x.Notes, ["uploadedBy"] = x.UploadedBy,
			["createdAt"] = x.CreatedAt.ToString("u", CultureInfo.InvariantCulture), ["attachmentCount"] = Number(x.Count),
			["sizeBytes"] = Number(x.Size), ["fileNames"] = string.Join(", ", lookup[x.Id].Select(file => file.FileName))
		}).ToList(), page.Value.Count);
	}
	private async Task<List<Dictionary<string, string?>>> CountsAsync(SchoolDocumentCategory? category)
	{
		var dq = (await documents.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && (!category.HasValue || x.Category == category));
		var fq = (await files.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id);
		var counts = await AsyncExecuter.ToListAsync(dq.GroupBy(x => x.Category).Select(g => new { Category = g.Key, Count = g.Count() }));
		var attachments = await AsyncExecuter.ToListAsync((from file in fq join document in dq on file.DocumentId equals document.Id select new { document.Category, file.Size })
			.GroupBy(x => x.Category).Select(g => new { Category = g.Key, Count = g.Count(), Size = g.Sum(x => x.Size) }));
		var result = new List<Dictionary<string, string?>> { Values("All categories", counts.Sum(x => x.Count), attachments.Sum(x => x.Count), attachments.Sum(x => x.Size)) };
		foreach (var value in Enum.GetValues<SchoolDocumentCategory>().Where(x => !category.HasValue || x == category))
			result.Add(Values(Label(value), counts.Find(x => x.Category == value)?.Count ?? 0, attachments.Find(x => x.Category == value)?.Count ?? 0, attachments.Find(x => x.Category == value)?.Size ?? 0));
		return result;
	}
	private static Dictionary<string, string?> Values(string category, long documents, long count, long size) => new() {
		["category"] = category, ["documentCount"] = Number(documents), ["attachmentCount"] = Number(count), ["sizeBytes"] = Number(size) };
	private sealed class DocumentRow
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = "";
		public string Category { get; set; } = "";
		public string? Notes { get; set; }
		public string UploadedBy { get; set; } = "";
		public DateTime CreatedAt { get; set; }
		public int Count { get; set; }
		public long Size { get; set; }
	}
}
