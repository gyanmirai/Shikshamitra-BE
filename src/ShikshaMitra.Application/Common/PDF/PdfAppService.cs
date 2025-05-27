using System;
using System.IO;
using System.Threading.Tasks;

namespace ShikshaMitra.Common.PDF;

public class PdfAppService(IPdfService pdfService) : ShikshaMitraAppService
{
	public async Task<byte[]> PostGeneratedPdfAsync(PDFDocument request)
	{
		var stream = await pdfService.GeneratePdfAsBytes(request);
		return stream;
	}
}