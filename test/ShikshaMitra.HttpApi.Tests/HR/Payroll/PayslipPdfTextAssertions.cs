using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using Shouldly;

namespace ShikshaMitra.HR.Payroll;

// Inspect the generated PDF, never reconstruct text from salary DTOs.
// PDF.js qualification separately verifies the complete reading order and exact field values.
internal static class PayslipPdfTextAssertions
{
	public static void AssertReadable(byte[] bytes, params string[] expectedLogicalValues)
	{
		using var input = new MemoryStream(bytes);
		using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Import);
		var spans = new Dictionary<(int Page, int Mcid), string>();
		Visit(pdf.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot")?.Elements["/K"]);
		foreach (var expected in expectedLogicalValues)
			spans.Values.Count(x => x == expected).ShouldBe(1, $"The stored logical text must contain '{expected}' exactly once.");
		var inspectedMaps = 0;
		for (var pageIndex = 0; pageIndex < pdf.PageCount; pageIndex++)
		{
			var page = pdf.Pages[pageIndex];
			var pageResources = page.Elements.GetDictionary("/Resources");
			pageResources.ShouldNotBeNull("Every payslip page must retain its font resources.");
			var resources = pageResources!.Elements.GetDictionary("/Font");
			resources.ShouldNotBeNull("Every payslip page must have inspectable fonts.");
			var missingByFont = new Dictionary<string, (int Width, HashSet<int> Missing)>();
			foreach (var fontName in resources!.Elements.Keys)
			{
				var font = resources.Elements.GetDictionary(fontName);
				font.ShouldNotBeNull("Every recorded payslip font must resolve to a dictionary.");
				var unicode = font!.Elements.GetDictionary("/ToUnicode");
				unicode.ShouldNotBeNull("Every payslip font must have an inspectable Unicode map.");
				inspectedMaps++;
				var map = Encoding.ASCII.GetString(unicode!.Stream.UnfilteredValue);
				var missing = Regex.Matches(map, @"<(?<code>[0-9a-fA-F]+)>\s+<0000>")
					.Select(x => Convert.ToInt32(x.Groups["code"].Value, 16)).ToHashSet();
				missingByFont[fontName] = (font.Elements.GetName("/Subtype") == "/Type0" ? 2 : 1, missing);
			}
			var source = Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);
			// PDFsharp deliberately omits inline BDC dictionaries from its operator operands.
			// Preserve Skia's MCID as a name for the parser; reject any unsupported text-marking syntax below.
			var parsedSource = Regex.Replace(source, @"/Span\s*<<\s*/MCID\s+(?<id>\d+)\s*>>\s*BDC",
				x => $"/Span /RecordedMcid{x.Groups["id"].Value} BDC");
			parsedSource = Regex.Replace(parsedSource, @"/Span\s*<<\s*/ActualText\s*<[0-9a-fA-F]+>\s*>>\s*BDC", "/Span /ShapedText BDC");
			parsedSource = Regex.Replace(parsedSource, @"/Artifact\s*<<\s*/Type\s*/(?<type>Layout|Background|Pagination)(?:\s*/Subtype\s*/Header)?\s*>>\s*BDC", x => $"/Artifact /{x.Groups["type"].Value} BDC");
			var contexts = new Stack<int?>();
			string? selectedFont = null;
			foreach (var op in ContentReader.ReadContent(Encoding.Latin1.GetBytes(parsedSource)).OfType<COperator>())
			{
				if (op.Name == "BDC")
				{
					op.Operands.Count.ShouldBe(2, "Unknown marked-content syntax must not bypass the glyph coverage check.");
					var marker = ((CName) op.Operands[1]).Name;
					contexts.Push(marker.StartsWith("/RecordedMcid", StringComparison.Ordinal) ? int.Parse(marker[13..]) : null);
				}
				else if (op.Name == "BMC") contexts.Push(null);
				else if (op.Name == "EMC") { contexts.Count.ShouldBeGreaterThan(0); contexts.Pop(); }
				else if (op.Name == "Tf") selectedFont = ((CName) op.Operands[0]).Name;
				else if (op.Name is "Tj" or "TJ")
				{
					selectedFont.ShouldNotBeNull();
					var (width, missing) = missingByFont[selectedFont];
					foreach (var text in Strings(op.Operands))
					{
						(text.Value.Length % width).ShouldBe(0);
						for (var offset = 0; offset < text.Value.Length; offset += width)
						{
							var code = width == 2 ? text.Value[offset] * 256 + text.Value[offset + 1] : text.Value[offset];
							if (!missing.Contains(code)) continue;
							var mcid = contexts.FirstOrDefault(x => x.HasValue);
							(mcid.HasValue && spans.ContainsKey((pageIndex, mcid.Value))).ShouldBeTrue(
								"A NUL-mapped shaping glyph must be covered by valid logical text on this exact page and MCID.");
						}
					}
				}
			}
			contexts.Count.ShouldBe(0);
		}
		inspectedMaps.ShouldBeGreaterThan(0);

		void Visit(PdfItem? item)
		{
			if (item is PdfReference reference) item = reference.Value;
			if (item is PdfArray array) { foreach (var child in array.Elements) Visit(child); return; }
			if (item is not PdfDictionary node) return;
			if (node.Elements.GetName("/S") == "/Span" && node.Elements.ContainsKey("/Alt"))
			{
				var text = node.Elements.GetString("/Alt");
				text.ShouldNotContain('\0');
				text.ShouldNotContain('\uFFFD');
				text.ShouldNotBeNullOrWhiteSpace();
				var pageReference = node.Elements.GetReference("/Pg");
				pageReference.ShouldNotBeNull("Logical text must have one explicit page association.");
				var pageNumber = Enumerable.Range(0, pdf.PageCount)
					.Single(i => pdf.Pages[i].Reference!.ObjectID == pageReference.ObjectID);
				// One original value per single-page MCID; no continued-span duplication.
				node.Elements["/K"].ShouldBeOfType<PdfInteger>();
				var mcid = node.Elements.GetInteger("/K");
				spans.TryAdd((pageNumber, mcid), text).ShouldBeTrue("Conflicting logical text must not be accepted.");
			}
			Visit(node.Elements["/K"]);
		}
	}

	private static IEnumerable<CString> Strings(CSequence operands) =>
		operands.SelectMany(item => item switch { CString text => new[] { text }, CArray array => Strings(array), _ => Array.Empty<CString>() });
}