using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportFiles(AcademicPdfService pdf, AcademicExcelService excel, AcademicCsvService csv, AcademicReportIdentityService identity) : ITransientDependency
{
	public async Task<byte[]> CreateAsync(AcademicReportPreviewDto report, InstituteReportRequest request)
	{
		if (request.Format == "pdf")
		{
			await identity.PrepareAsync(report, request.Filters.BoardId, request.Filters.MediumId);
			return await pdf.GenerateReportPdf(report);
		}
		if (request.Format == "excel") return excel.GenerateReportExcel(report);
		foreach (var row in report.Rows)
			foreach (var key in row.Keys.ToArray())
				if (row[key] is { Length: > 0 } value && "=+-@\t\r\n".Contains(value[0])) row[key] = "'" + value;
		return csv.GenerateReportCsv(report);
	}
	public static string ScopeHash(UserScopeContext scope)
	{
		var value = new { scope.IsAdmin, scope.HasWildcardAcademicYear,
			years = scope.AcademicYearIds.Order(), boards = scope.BoardIds.Order(), mediums = scope.MediumIds.Order(), stages = scope.MediumStageIds.Order(),
			standards = scope.StandardIds.Order(), divisions = scope.DivisionIds.Order(),
			allBoards = scope.WildcardBoardAcademicYearIds.Order(), allMediums = scope.WildcardMediumBoardIds.Order(),
			allStages = scope.WildcardMediumStageMediumIds.Order(), allStandards = scope.WildcardStandardMediumStageIds.Order(), allDivisions = scope.WildcardDivisionStandardIds.Order() };
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
	}
	public static string ContentType(string format) => format switch { "pdf" => "application/pdf", "csv" => "text/csv; charset=utf-8", _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
}
