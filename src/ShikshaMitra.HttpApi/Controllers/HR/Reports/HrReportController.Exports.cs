using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

public partial class HrReportController
{
	[HttpDelete("exports/{jobId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteFailedExportAsync(Guid jobId)
	{
		await ReportAppService.DeleteFailedExportAsync(jobId);
		return NoContent();
	}
}
