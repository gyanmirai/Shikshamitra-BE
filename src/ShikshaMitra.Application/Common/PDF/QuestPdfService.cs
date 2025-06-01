using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.Extensions;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using IElement = ShikshaMitra.Common.PDF.Elements.IElement;
using QImage = QuestPDF.Infrastructure.Image;

namespace ShikshaMitra.Common;

public class QuestPdfService(IFontService fontService, IImageService imageService) : IPdfService, ITransientDependency
{
	private readonly Dictionary<int, QImage> _images = [];


	/// <summary>
	/// Generates a PDF as a stream based on the provided request.
	/// </summary>
	/// <param name="request">The request containing the specifications for the PDF creation.</param>
	/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
	/// <returns>A stream containing the generated PDF.</returns>
	public async Task<Stream> GeneratePdfAsStreamAsync(PDFDocument request,
		CancellationToken cancellationToken = default)
	{
		var stream = await InternalGeneratePdfStreamAsync(request, cancellationToken);
		stream.Position = 0;
		return stream;
	}

	/// <summary>
	/// Generates a PDF as a byte array based on the provided request.
	/// </summary>
	/// <param name="request">The request containing the specifications for the PDF creation.</param>
	/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
	/// <returns>A byte array containing the generated PDF.</returns>
	public async Task<byte[]> GeneratePdfAsBytes(PDFDocument request, CancellationToken cancellationToken = default)
	{
		await using var stream = await InternalGeneratePdfStreamAsync(request, cancellationToken);
		return stream.ToArray();
	}

	public async Task<byte[]> GenerateReport<T>(IEnumerable<T> items, string title)
	{
		PropertyInfo[] properties = typeof(T).GetProperties();

		// Construct the PDF document
		var pdfDocument = new PDFDocument
		{
			Page = new PageSpecs { BackgroundColor = "#ffffff", Margin = 20, MarginUnit = Unit.Point },
			Header = new TextElement
			{
				Text = $"title Report",
				FontSize = 24,
				FontWeight = FontWeight.Bold,
				Alignment = TextAlignment.Center,
				Padding = 10,
				PaddingUnit = Unit.Point
			},
			Content = new ColumnElement
			{
				Elements = new List<IElement>
					{
						// Add column headers with formatted names
						new RowElement
						{
							Elements = properties.Select(prop => (IElement) new TextElement
							{
								Text = SplitCamelCase(prop.Name),
								FontWeight = FontWeight.Bold,
								Padding = 5,
								PaddingUnit = Unit.Point
							}).ToList(),
							Spacing = 10
						}
					}
					.Concat(items.Select(item => (IElement) new RowElement
					{
						Elements = properties.Select(prop => (IElement) new TextElement
						{
							Text = FormatPropertyValue(prop, item), Padding = 5, PaddingUnit = Unit.Point
						}).ToList(),
						Spacing = 10
					})).ToList(),
				Spacing = 20
			}
		};
		return await GeneratePdfAsBytes(pdfDocument);
	}

// Helper method to format property values
	private string FormatPropertyValue<T>(PropertyInfo prop, T item)
	{
		var value = prop.GetValue(item, null);
		if (value == null)
			return "N/A";

		// Custom formatting for specific types
		if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
			return ((DateTime) value).ToString("dd-MM-yyyy");


		return value.ToString();
	}


	private string SplitCamelCase(string input) =>
		Regex.Replace(
			Regex.Replace(
				input,
				@"(\P{Ll})(\P{Ll}\p{Ll})",
				"$1 $2"
			),
			@"(\p{Ll})(\P{Ll})",
			"$1 $2"
		);


	/// <summary>
	/// Helper method to generate a PDF as a MemoryStream.
	/// </summary>
	/// <param name="request">The request containing the specifications for the PDF creation.</param>
	/// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
	/// <returns>A MemoryStream containing the generated PDF.</returns>
	private async Task<MemoryStream> InternalGeneratePdfStreamAsync(PDFDocument request,
		CancellationToken cancellationToken)
	{
		await LoadImagesAsync(request, cancellationToken);
		await LoadFontAsync(request, cancellationToken);

		var stream = new MemoryStream();
		Document.Create(container =>
			container.Page(page =>
			{
				page.FromSpecs(request.Page);
				if (request.Header is not null)
				{
					page.Header().Element(request.Header);
				}

				page.Content().Element(request.Content);
				if (request.Footer is not null)
				{
					page.Footer().Element(request.Footer);
				}
			})
		).GeneratePdf(stream);

		return stream;
	}


	private async Task LoadFontAsync(PDFDocument request, CancellationToken cancellationToken)
	{
		if (request.Page.FontUris?.Length > 0)
		{
			var tasks = request.Page.FontUris.Select(uri => fontService.LoadFontAsync(uri, cancellationToken));
			await Task.WhenAll(tasks);
		}
	}

	private async Task LoadImagesAsync(PDFDocument request, CancellationToken cancellationToken)
	{
		if (request.Header is not null)
		{
			await LoadImagesAsync(request.Header, cancellationToken);
		}

		if (request.Footer is not null)
		{
			await LoadImagesAsync(request.Footer, cancellationToken);
		}

		await LoadImageAsync(request.Page, cancellationToken);
		await LoadImagesAsync(request.Content, cancellationToken);
	}

	private async Task LoadImagesAsync(IElement element, CancellationToken cancellationToken)
	{
		if (element is ImageElement imageElement)
		{
			await LoadImageAsync(imageElement, cancellationToken);
		}
		else if (element is ColumnElement columnElement)
		{
			await LoadImagesAsync(columnElement, cancellationToken);
		}
		else if (element is RowElement rowElement)
		{
			await LoadImagesAsync(rowElement, cancellationToken);
		}
		else if (element is Cell cellElement)
		{
			await LoadImagesAsync(cellElement, cancellationToken);
		}
		else if (element is TableElement tableElement)
		{
			await LoadImagesAsync(tableElement, cancellationToken);
		}
	}

	private async Task LoadImagesAsync(ColumnElement columnElement, CancellationToken cancellationToken)
	{
		foreach (var element in columnElement.Elements)
		{
			switch (element)
			{
				case ImageElement imageElement:
					await LoadImageAsync(imageElement, cancellationToken);
					break;
				case ColumnElement columnElement1:
					await LoadImagesAsync(columnElement1, cancellationToken);
					break;
				case RowElement rowElement:
					await LoadImagesAsync(rowElement, cancellationToken);
					break;
				case Cell cellElement:
					await LoadImagesAsync(cellElement, cancellationToken);
					break;
				case TableElement tableElement:
					await LoadImagesAsync(tableElement, cancellationToken);
					break;
			}
		}
	}

	private async Task LoadImagesAsync(RowElement rowElement, CancellationToken cancellationToken)
	{
		foreach (var element in rowElement.Elements)
		{
			if (element is ImageElement imageElement)
			{
				await LoadImageAsync(imageElement, cancellationToken);
			}
			else if (element is ColumnElement columnElement)
			{
				await LoadImagesAsync(columnElement, cancellationToken);
			}
			else if (element is RowElement rowElement1)
			{
				await LoadImagesAsync(rowElement1, cancellationToken);
			}
			else if (element is Cell cellElement)
			{
				await LoadImagesAsync(cellElement, cancellationToken);
			}
			else if (element is TableElement tableElement)
			{
				await LoadImagesAsync(tableElement, cancellationToken);
			}
		}
	}

	private async Task LoadImagesAsync(Cell cellElement, CancellationToken cancellationToken)
	{
		if (cellElement.Element is ImageElement imageElement)
		{
			await LoadImageAsync(imageElement, cancellationToken);
		}
		else if (cellElement.Element is ColumnElement columnElement)
		{
			await LoadImagesAsync(columnElement, cancellationToken);
		}
		else if (cellElement.Element is RowElement rowElement1)
		{
			await LoadImagesAsync(rowElement1, cancellationToken);
		}
	}

	private async Task LoadImagesAsync(TableElement tableElement, CancellationToken cancellationToken)
	{
		foreach (var cell in tableElement.Cells)
		{
			await LoadImagesAsync(cell, cancellationToken);
		}
	}

	private async Task LoadImageAsync(ImageElement imageElement, CancellationToken cancellationToken)
	{
		if (imageElement.Image is not null)
		{
			return;
		}

		if (_images.TryGetValue(imageElement.GetHashCode(), out var image))
		{
			imageElement.Image = image;
			return;
		}

		var bytes = await imageService.LoadImageAsync(imageElement, cancellationToken);
		if (bytes is null || bytes.Length < 1)
		{
			throw new InvalidOperationException("Image bytes are not loaded");
		}

		image = QImage.FromBinaryData(bytes);
		_images[imageElement.GetHashCode()] = image;
		imageElement.Image = image;
	}

	private async Task LoadImageAsync(PageSpecs pageSpecs, CancellationToken cancellationToken)
	{
		if (pageSpecs.BackgroundImage is not null || pageSpecs.BackgroundImageUri is null)
		{
			return;
		}

		if (_images.TryGetValue(pageSpecs.BackgroundImageUri.GetHashCode(), out var image))
		{
			pageSpecs.BackgroundImage = image;
			return;
		}

		var bytes = await imageService.LoadImageAsync(pageSpecs.BackgroundImageUri, cancellationToken);
		if (bytes is null || bytes.Length < 1)
		{
			throw new InvalidOperationException("Image bytes are not loaded");
		}

		image = QImage.FromBinaryData(bytes);
		_images[pageSpecs.BackgroundImageUri.GetHashCode()] = image;
		pageSpecs.BackgroundImage = image;
	}
}