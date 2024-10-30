using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Common.PDF;

public interface IPdfService
{
	byte[] GeneratePdf(Action<IDocumentContainer> documentAction);

	Task<Stream> GeneratePdfAsStreamAsync(CreatePDF request, CancellationToken cancellationToken);

	Task<byte[]> GeneratePdfAsBytes(CreatePDF request, CancellationToken cancellationToken);
}