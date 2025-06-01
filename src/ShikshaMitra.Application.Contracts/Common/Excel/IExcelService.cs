using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Common.Excel;

public interface IExcelService
{
	byte[] GenerateExcelAsBytes(ExcelDocument request, CancellationToken cancellationToken = default);

	public byte[] GenerateExcelFromData<T>(
		IEnumerable<T> items,
		string worksheetName,
		string? title = null,
		IEnumerable<string>? includeProperties = null,
		CancellationToken cancellationToken = default);
}