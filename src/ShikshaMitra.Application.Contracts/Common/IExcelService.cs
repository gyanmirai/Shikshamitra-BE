using System.Collections.Generic;
using System;
using ClosedXML.Excel;

namespace ShikshaMitra.Common;

public interface IExcelService
{
	byte[] GenerateExcel<T>(IEnumerable<T> data, Action<IXLWorksheet, IEnumerable<T>> contentAction);

}
