using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Books;

public class LibraryCopyLabelBatchDto
{
	public Guid BookId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Author { get; set; } = string.Empty;
	public string Isbn { get; set; } = string.Empty;
	public string CategoryName { get; set; } = string.Empty;
	public List<LibraryCopyLabelDto> Labels { get; set; } = new();
}

public class LibraryCopyLabelDto
{
	public Guid CopyId { get; set; }
	public int CopyNumber { get; set; }
	public string AccessionNumber { get; set; } = string.Empty;
	public Guid SectionId { get; set; }
	public string SectionName { get; set; } = string.Empty;
	public Guid ShelfId { get; set; }
	public string ShelfName { get; set; } = string.Empty;
	public string BarcodeValue { get; set; } = string.Empty;
}
