using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>Decode the actual receipt's embedded Unicode fonts and drawn text.</summary>
internal static class ReceiptPdfTextReader
{
	public static string Read(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes);
		using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
		var result = new StringBuilder();
		foreach (var page in pdf.Pages)
		{
			var fonts = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
			var maps = new Dictionary<string, Dictionary<int, string>>();
			foreach (var fontName in fonts.Elements.Keys)
			{
				var unicode = fonts.Elements.GetDictionary(fontName)!.Elements.GetDictionary("/ToUnicode");
				if (unicode == null) continue;
				var map = new Dictionary<int, string>();
				var source = Encoding.ASCII.GetString(unicode.Stream.UnfilteredValue);
				foreach (Match block in Regex.Matches(source, @"beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline))
					foreach (Match pair in Regex.Matches(block.Groups["body"].Value, @"<(?<code>[0-9a-fA-F]+)>\s+<(?<text>[0-9a-fA-F]+)>"))
						map[Convert.ToInt32(pair.Groups["code"].Value, 16)] = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(pair.Groups["text"].Value));
				foreach (Match block in Regex.Matches(source, @"beginbfrange(?<body>.*?)endbfrange", RegexOptions.Singleline))
					foreach (Match range in Regex.Matches(block.Groups["body"].Value,
						@"<(?<start>[0-9a-fA-F]+)>\s+<(?<end>[0-9a-fA-F]+)>\s+<(?<text>[0-9a-fA-F]+)>"))
					{
						var first = Convert.ToInt32(range.Groups["start"].Value, 16);
						var last = Convert.ToInt32(range.Groups["end"].Value, 16);
						var unicodeStart = Convert.ToInt32(range.Groups["text"].Value, 16);
						for (var code = first; code <= last; code++) map[code] = char.ConvertFromUtf32(unicodeStart + code - first);
					}
				maps[fontName] = map;
			}
			string? font = null;
			foreach (var op in ContentReader.ReadContent(page).OfType<COperator>())
			{
				if (op.Name == "Tf") font = ((CName)op.Operands[0]).Name;
				if (op.Name is not ("Tj" or "TJ") || font == null || !maps.TryGetValue(font, out var map)) continue;
				foreach (var text in Strings(op.Operands))
					for (var i = 0; i + 1 < text.Value.Length; i += 2)
						if (map.TryGetValue(text.Value[i] * 256 + text.Value[i + 1], out var value)) result.Append(value);
				result.Append(' ');
			}
		}
		return result.ToString();
	}

	private static IEnumerable<CString> Strings(CSequence operands) => operands.SelectMany(item => item switch
	{
		CString text => new[] { text }, CArray array => Strings(array), _ => Array.Empty<CString>(),
	});
}
