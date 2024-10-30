using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Common.PDF.Elements;

[JsonPolymorphic]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(TableElement), "table")]
[JsonDerivedType(typeof(Cell), "cell")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(ColumnElement), "column")]
[JsonDerivedType(typeof(RowElement), "row")]
public interface IElement
{
	RowItemConfig? RowItemConfig { get; set; }
}

public abstract class Element : IElement
{
	public RowItemConfig? RowItemConfig { get; set; }
}