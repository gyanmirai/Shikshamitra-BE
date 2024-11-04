using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Common.PDF;

public interface IPdfService
{
	Task<Stream> GeneratePdfAsStreamAsync(PDFDocument request, CancellationToken cancellationToken = default);

	Task<byte[]> GeneratePdfAsBytes(PDFDocument request, CancellationToken cancellationToken = default);

	Task<byte[]> GenerateSimplePdf<T>(IEnumerable<T> items);

}