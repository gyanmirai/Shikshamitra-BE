using QuestPDF.Infrastructure;
using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Common;

public interface IPdfService
{
	byte[] GeneratePdf(Action<IDocumentContainer> documentAction);
}