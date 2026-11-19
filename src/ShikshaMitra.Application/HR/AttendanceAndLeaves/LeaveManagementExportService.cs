using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.Reports;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[RemoteService(false)]
public class LeaveManagementExportService(
	LeaveManagementReader leaveManagementReader,
	HRExcelService hrExcelService)
	: ApplicationService
{
	public async Task<byte[]> ExportToExcelAsync(LeaveManagementGetListInput input)
	{
		const int exportMax = 10_000;
		const int batch = 1_000;
		var allItems = new List<LeaveManagementDto>();
		var page = 1;

		while (allItems.Count < exportMax)
		{
			var paged = await leaveManagementReader.GetListAsync(CloneListInputWithPage(input, page, batch));
			if (paged.TotalCount > exportMax) throw HrReportingEngine.Invalid("This direct leave export supports up to 10,000 rows. Narrow its filters or use the table export.");
			if (paged.Items.Count == 0 && allItems.Count < paged.TotalCount) throw HrReportingEngine.Invalid("The leave export returned an incomplete population. Retry it.");
			if (paged.Items.Count == 0)
			{
				break;
			}

			allItems.AddRange(paged.Items);
			if (allItems.Count >= paged.TotalCount || paged.Items.Count < batch)
			{
				break;
			}

			page++;
		}

		if (allItems.Count > exportMax) throw HrReportingEngine.Invalid("Narrow this leave export to at most 10,000 rows.");

		return hrExcelService.GenerateLeaveManagementExcel(allItems);
	}

	private static LeaveManagementGetListInput CloneListInputWithPage(LeaveManagementGetListInput input, int page, int pageSize) =>
		new()
		{
			AcademicYearId = input.AcademicYearId,
			MonthYearAnchor = input.MonthYearAnchor,
			Search = input.Search,
			Page = page,
			PageSize = pageSize,
			UseExportBatching = true,
			Status = input.Status,
			EmployeeId = input.EmployeeId,
			LeavePolicyId = input.LeavePolicyId,
			PaidType = input.PaidType
		};
}
