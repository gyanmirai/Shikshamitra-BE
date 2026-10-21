using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.HR.Reports;

public class HrReportTableQueryDto
{
	public string Search { get; set; } = "";
	public List<HrReportTableFilterDto> Filters { get; set; } = [];
	public List<HrReportTableSortDto> Sorts { get; set; } = [];
	public bool HasCriteria => !string.IsNullOrWhiteSpace(Search) || Filters == null || Sorts == null || Filters.Count > 0 || Sorts.Count > 0;

	public static HrReportTableQueryDto Parse(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return new();
		if (json.Length > 32768) throw Invalid();
		try { return JsonSerializer.Deserialize<HrReportTableQueryDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw Invalid(); }
		catch (JsonException) { throw Invalid(); }
	}
	private static BusinessException Invalid() => new BusinessException(HRErrorCodes.ReportDesignInvalid).WithData("errors", "Provide valid report table criteria of at most 32 KB.");
}

public class HrReportTableFilterDto
{
	public string Column { get; set; } = "";
	public string Type { get; set; } = "";
	public JsonElement Value { get; set; }
}

public class HrReportTableSortDto
{
	public string Column { get; set; } = "";
	public string Direction { get; set; } = "asc";
}

public class HrReportTablePageQueryDto
{
	public int Skip { get; set; }
	public int Take { get; set; } = 50;
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
	public HrReportTableQueryDto Criteria { get; set; } = new();
}
