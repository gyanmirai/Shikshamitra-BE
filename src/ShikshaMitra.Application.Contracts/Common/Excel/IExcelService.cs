using System.Collections.Generic;
using System;
using ClosedXML.Excel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Common;

public interface IExcelService
{
	byte[] GenerateExcelAsBytes(ExcelDocument request, CancellationToken cancellationToken = default);
}