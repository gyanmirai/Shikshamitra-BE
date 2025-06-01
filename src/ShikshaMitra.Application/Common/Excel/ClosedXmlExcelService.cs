using ClosedXML.Excel;
using ShikshaMitra.Common.Excel.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Volo.Abp.DependencyInjection;

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

	public byte[] GenerateExcelFromData<T>(
		IEnumerable<T> items,
		string worksheetName,
		string? title = null,
		IEnumerable<string>? includeProperties = null,
		CancellationToken cancellationToken = default)
	{
		var stream = new MemoryStream();
		using (var workbook = new XLWorkbook())
		{
			var worksheet = workbook.Worksheets.Add(worksheetName ?? typeof(T).Name);

			// Get properties to include in the export
			var properties = GetPropertiesToInclude<T>(includeProperties);

			// Current row index
			int rowIndex = 1;

			if (!string.IsNullOrEmpty(title))
			{
				var titleCell = worksheet.Cell(rowIndex, 1);
				titleCell.Value = title;
				titleCell.Style.Font.Bold = true;
				titleCell.Style.Font.FontSize = 14;
				worksheet.Range(rowIndex, 1, rowIndex, properties.Count).Merge();
				rowIndex++;

				var metadataCell = worksheet.Cell(rowIndex, 1);
				metadataCell.Value = $"Generated on: {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss} by: {Environment.UserName}";
				metadataCell.Style.Font.Italic = true;
				worksheet.Range(rowIndex, 1, rowIndex, properties.Count).Merge();
				rowIndex++;

				rowIndex++;
			}

			for (int i = 0; i < properties.Count; i++)
			{
				var cell = worksheet.Cell(rowIndex, i + 1);
				cell.Value = GetDisplayName(properties[i]);
				cell.Style.Font.Bold = true;
				cell.Style.Fill.BackgroundColor = XLColor.LightGray;
				cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
			}
			rowIndex++;

			foreach (var item in items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				for (int i = 0; i < properties.Count; i++)
				{
					var value = properties[i].GetValue(item);
					worksheet.Cell(rowIndex, i + 1).Value = XLCellValue.FromObject(FormatPropertyValue(value));
				}
				rowIndex++;
			}

			// Auto-fit columns and add borders
			worksheet.Columns().AdjustToContents();
			worksheet.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

			workbook.SaveAs(stream);
		}

		stream.Position = 0;
		return stream.ToArray();
	}

	public ExcelDocument CreateExcelDocument<T>(
		IEnumerable<T> items,
		string worksheetName,
		string title = null,
		IEnumerable<string> includeProperties = null)
	{
		var properties = GetPropertiesToInclude<T>(includeProperties);

		var rows = new List<ExcelRow>();

		if (!string.IsNullOrEmpty(title))
		{
			rows.Add(new ExcelRow
			{
				Cells = new List<ExcelCell>
				{
					new ExcelCell
					{
						Value = title
					}
				}
			});

			rows.Add(new ExcelRow
			{
				Cells = new List<ExcelCell>
				{
					new ExcelCell
					{
						Value = $"Generated on: {DateTime.UtcNow:dd-MM-yyyy HH:mm:ss} by: {Environment.UserName}"
					}
				}
			});

			rows.Add(new ExcelRow { Cells = new List<ExcelCell> { new ExcelCell { Value = string.Empty } } });
		}

		rows.Add(new ExcelRow
		{
			Cells = properties.Select(p => new ExcelCell { Value = GetDisplayName(p) }).ToList()
		});

		// Add data rows
		foreach (var item in items)
		{
			rows.Add(new ExcelRow
			{
				Cells = properties.Select(p => new ExcelCell { Value = p.GetValue(item) }).ToList()
			});
		}

		return new ExcelDocument
		{
			Sheets = new List<ExcelSheet>
			{
				new ExcelSheet
				{
					SheetName = worksheetName ?? typeof(T).Name,
					Rows = rows
				}
			}
		};
	}


	private List<PropertyInfo> GetPropertiesToInclude<T>(IEnumerable<string> includeProperties)
	{
		var allProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead &&
					  (p.PropertyType.IsPrimitive ||
					   p.PropertyType == typeof(string) ||
					   p.PropertyType == typeof(DateTime) ||
					   p.PropertyType.IsEnum ||
					   Nullable.GetUnderlyingType(p.PropertyType) != null))
			.ToList();

		if (includeProperties == null || !includeProperties.Any())
			return allProperties;

		return allProperties
			.Where(p => includeProperties.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
			.ToList();
	}

	
	private string GetDisplayName(PropertyInfo property)
	{
		return SplitCamelCase(property.Name);
	}


	private object FormatPropertyValue(object value)
	{
		if (value == null)
			return string.Empty;

		if (value is DateTime dateTime)
			return dateTime.ToString("dd--MM-yyyy HH:mm:ss");

		if (value is bool boolean)
			return boolean ? "Yes" : "No";

		return value;
	}

	private string SplitCamelCase(string input)
	{
		if (string.IsNullOrEmpty(input))
			return input;

		return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
	}


	public byte[] GenerateReport<T>(IEnumerable<T> items, string reportTitle = null)
	{
		string title = reportTitle ?? $"{typeof(T).Name} Report";
		string worksheetName = typeof(T).Name;

		return GenerateExcelFromData(
			items,
			worksheetName,
			title,
			null,
			CancellationToken.None
		);
	}
}