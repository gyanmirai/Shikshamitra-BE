using ShikshaMitra.Common.PDF.Elements;

namespace ShikshaMitra.Common.PDF;

public class CreatePDF
{
	/// <summary>
	/// Gets or sets the specifications for the page.
	/// </summary>
	public required PageSpecs Page { get; set; }

	/// <summary>
	/// Gets or sets the title element of the PDF.
	/// </summary>
	public IElement? Header { get; set; }

	/// <summary>
	/// Gets or sets the content element of the PDF.
	/// </summary>
	public required IElement Content { get; set; }

	/// <summary>
	/// Gets or sets the footer element of the PDF.
	/// </summary>
	public IElement? Footer { get; set; }

	/// <summary>
	/// Gets or sets the name of the output file.
	/// <para />
	/// If not specified, the output file will be named "output.pdf".
	/// </summary>
	public string? OutputFileName { get; set; }
}
