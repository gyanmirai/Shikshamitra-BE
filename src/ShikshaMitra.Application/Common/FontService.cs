using QuestPDF.Drawing;
using ShikshaMitra.Common.PDF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common;

public class FontService(IHttpClientFactory httpClientFactory) : IFontService, IScopedDependency
{
	private readonly HashSet<string> _loadedFonts = new();

	public Task LoadFontAsync(string uri, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(uri))
		{
			return Task.CompletedTask;
		}

		return !Uri.TryCreate(uri, UriKind.Absolute, out var fontUri)
			? Task.CompletedTask
			: LoadFontAsync(fontUri, cancellationToken);
	}

	public async Task LoadFontAsync(Uri uri, CancellationToken cancellationToken)
	{
		if (_loadedFonts.Contains(uri.AbsoluteUri))
		{
			return;
		}

		using var client = httpClientFactory.CreateClient();
		var response = await client.GetAsync(uri, cancellationToken);
		response.EnsureSuccessStatusCode();
		var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		stream.Seek(0, SeekOrigin.Begin);
		FontManager.RegisterFont(stream);
		_loadedFonts.Add(uri.AbsoluteUri);
	}
}