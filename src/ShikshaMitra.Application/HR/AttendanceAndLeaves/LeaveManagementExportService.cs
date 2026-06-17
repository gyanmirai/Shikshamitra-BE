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

		if (allItems.Count > exportMax)
		{
			allItems = allItems.Take(exportMax).ToList();
		}

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
