using ClosedXML.Excel;
using Volo.Abp.DependencyInjection;
using System.IO;
using System.Threading;

namespace ShikshaMitra.Common.Excel;

public class ClosedXmlExcelService : IExcelService, ITransientDependency
{
	/// <summary>
	/// Generates an Excel document as a stream based on the provided request.
	/// </summary>
	/// <param name="request">The request containing the specifications for the Excel creation.</param>
	/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
	/// <returns>A stream containing the generated Excel document.</returns>
	public byte[] GenerateExcelAsBytes(ExcelDocument request, CancellationToken cancellationToken = default)
	{
		var stream = new MemoryStream();
		using (var workbook = new XLWorkbook())
		{
			foreach (var sheet in request.Sheets)
			{
				var worksheet = workbook.Worksheets.Add(sheet.SheetName);
				for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
				{
					var row = sheet.Rows[rowIndex];
					for (var colIndex = 0; colIndex < row.Cells.Count; colIndex++)
					{
						worksheet.Cell(rowIndex + 1, colIndex + 1).Value =
							XLCellValue.FromObject(row.Cells[colIndex].Value);
					}
				}
			}

			workbook.SaveAs(stream);
		}

		stream.Position = 0;
		return stream.ToArray();
	}
}