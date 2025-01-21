using System.Threading;

namespace ShikshaMitra.Common.Excel;

public interface IExcelService
{
	byte[] GenerateExcelAsBytes(ExcelDocument request, CancellationToken cancellationToken = default);
}