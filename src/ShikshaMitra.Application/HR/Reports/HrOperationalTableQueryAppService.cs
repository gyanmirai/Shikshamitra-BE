using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

[Authorize, RemoteService(false)]
public class HrOperationalTableQueryAppService(HrTableExportSources sources, IUnitOfWorkManager uows)
	: ApplicationService, IHrOperationalTableQueryAppService
{
	public async Task<JsonElement> QueryAsync(HrOperationalTableQueryRequest request)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
		if (request == null || request.Size is < 1 or > 500 || request.Offset is < 0 or > HrReportingEngine.MaximumPopulation ||
			string.IsNullOrWhiteSpace(request.Table) || request.Query.ValueKind != JsonValueKind.Object || request.Query.GetRawText().Length > 32768 ||
			!HrTableExportSources.Fields.TryGetValue(request.Table, out var ids) || request.Table is "hr-generated-report" or "hr-generated-report-history")
			throw HrReportingEngine.Invalid("Choose a supported operational HR table and page.");
		var query = JsonNode.Parse(request.Query.GetRawText())!.AsObject();
		query["criteria"] = JsonSerializer.SerializeToNode(request.Criteria, HrReportingEngine.Json);
		if (request.Table == "hr-salary-documents")
		{
			var source = query["dataSource"]?.ToString();
			if (source is not ("payroll_statement" or "payroll_register")) throw HrReportingEngine.Invalid("Choose a salary statement or payroll register.");
			// Keep exactly the selected report fields, validated against its source catalog by the reader.
			ids = query["columns"]?.Deserialize<string[]>() ?? [];
		}
		var input = new HrTableExportRequest { Table = request.Table, Query = JsonSerializer.SerializeToElement(query),
			Columns = ids.Select(id => new HrTableExportColumn { Id = id, Label = id }).ToList() };
		HrTableExportSources.Validate(input);
		// All pages use a single snapshot; filters/count/sort precede response pagination.
		using var snapshot = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var page = await sources.ReadAsync(input, request.Offset, request.Size);
		await snapshot.CompleteAsync();
		return page.Envelope;
	}
}
