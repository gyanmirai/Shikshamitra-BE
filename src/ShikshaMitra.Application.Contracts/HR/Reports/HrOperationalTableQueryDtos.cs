using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public class HrOperationalTableQueryRequest
{
	[Required, MaxLength(100)] public string Table { get; set; } = "";
	public JsonElement Query { get; set; }
	public HrReportTableQueryDto Criteria { get; set; } = new();
	[Range(0, 500000)] public int Offset { get; set; }
	[Range(1, 500)] public int Size { get; set; } = 20;
}
public interface IHrOperationalTableQueryAppService : IApplicationService
{
	Task<JsonElement> QueryAsync(HrOperationalTableQueryRequest request);
}
