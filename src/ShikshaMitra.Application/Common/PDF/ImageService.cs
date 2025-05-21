using ShikshaMitra.Common.PDF.Elements;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.PDF;

public class ImageService(IHttpClientFactory httpClientFactory) : IImageService, IScopedDependency
{
	/// <summary>
	/// Loads an image from the specified URI asynchronously.
	/// </summary>
	/// <param name="uri">The URI of the image to load.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the loaded image as a byte array.</returns>
	public async Task<byte[]> LoadImageAsync(string uri, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(uri))
		{
			throw new ArgumentException("The URI cannot be null or whitespace.", nameof(uri));
		}

		using var client = httpClientFactory.CreateClient();
		var response = await client.GetAsync(uri, cancellationToken);
		response.EnsureSuccessStatusCode();
		var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		return bytes;
	}

	/// <summary>
	/// Loads an image from the specified <see cref="ImageElement"/> asynchronously.
	/// </summary>
	/// <param name="element">The <see cref="ImageElement"/> containing the source URI of the image to load.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the loaded image as a byte array.</returns>
	public Task<byte[]> LoadImageAsync(ImageElement element, CancellationToken cancellationToken)
		=> LoadImageAsync(element.Source, cancellationToken);
}