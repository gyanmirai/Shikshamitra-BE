using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.TextTemplating;

namespace ShikshaMitra.Common;

public class QuestPdfService : IPdfService, ITransientDependency
{
	public byte[] GeneratePdf(Action<IDocumentContainer> documentAction)
	{
		return Document.Create(documentAction).GeneratePdf();
	}
}