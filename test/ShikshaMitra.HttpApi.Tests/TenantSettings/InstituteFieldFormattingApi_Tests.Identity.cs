using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InstituteFieldFormattingApi_Tests
{
	[Fact]
	public async Task Draft_identity_pdf_uses_the_same_changed_signature_format_as_save()
	{
		using var client = factory.CreateClient();
		await factory.SeedStageAsync();
		await SetAsync(client, "schoolIdentity", AcademicTextCase.Uppercase);
		using var upload = new MultipartFormDataContent();
		upload.Add(new StringContent("2"), "kind");
		var image = new ByteArrayContent(SchoolIdentityTestImage.Png());
		image.Headers.ContentType = new("image/png");
		upload.Add(image, "file", "signature.png");
		var uploaded = await client.PostAsync("/api/institute-setup/school-identity/assets", upload);
		uploaded.EnsureSuccessStatusCode();
		var asset = (await uploaded.Content.ReadFromJsonAsync<SchoolIdentityAssetDto>())!;
		var choices = new SchoolIdentityChoicesDto { Signature = new() { Mode = SchoolIdentityAssetMode.Custom,
			AssetId = asset.Id, PrincipalName = "jane smith", Designation = "head teacher" } };
		var preview = await client.PostAsJsonAsync("/api/institute-setup/school-identity/preview", new SchoolIdentityPreviewDto { Choices = choices });
		preview.EnsureSuccessStatusCode();
		var text = ExtractIdentityPdfText(await preview.Content.ReadAsByteArrayAsync());
		text.Contains("JANE SMITH", StringComparison.Ordinal).ShouldBeTrue(text);
		text.Contains("HEAD TEACHER", StringComparison.Ordinal).ShouldBeTrue(text);
		var response = await client.PutAsJsonAsync("/api/institute-setup/school-identity", new SchoolIdentityUpdateDto { Choices = choices });
		response.EnsureSuccessStatusCode();
		var saved = (await response.Content.ReadFromJsonAsync<SchoolIdentityProfileDto>())!;
		saved.Choices.Signature!.PrincipalName.ShouldBe("JANE SMITH");
		saved.Choices.Signature.Designation.ShouldBe("HEAD TEACHER");
		await SetAsync(client, "schoolIdentity", AcademicTextCase.CamelCase);
		var unchanged = await client.PostAsJsonAsync("/api/institute-setup/school-identity/preview", new SchoolIdentityPreviewDto { Choices = saved.Choices });
		unchanged.EnsureSuccessStatusCode();
		ExtractIdentityPdfText(await unchanged.Content.ReadAsByteArrayAsync()).Contains("JANE SMITH", StringComparison.Ordinal).ShouldBeTrue();
	}

	private static string ExtractIdentityPdfText(byte[] bytes)
	{
		using var input = new MemoryStream(bytes);
		using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Import);
		var text = new StringBuilder();
		foreach (var page in pdf.Pages)
		{
			var fonts = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
			var maps = new Dictionary<string, Dictionary<int, string>>();
			foreach (var fontName in fonts.Elements.Keys)
			{
				var font = fonts.Elements.GetDictionary(fontName)!;
				var source = Encoding.ASCII.GetString(font.Elements.GetDictionary("/ToUnicode")!.Stream.UnfilteredValue);
				var map = new Dictionary<int, string>();
				foreach (Match block in Regex.Matches(source, @"beginbfchar(?<pairs>.*?)endbfchar", RegexOptions.Singleline))
					foreach (Match pair in Regex.Matches(block.Groups["pairs"].Value, @"<(?<glyph>[0-9A-Fa-f]+)>\s*<(?<unicode>[0-9A-Fa-f]+)>"))
						map[Convert.ToInt32(pair.Groups["glyph"].Value, 16)] = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(pair.Groups["unicode"].Value));
				foreach (Match block in Regex.Matches(source, @"beginbfrange(?<ranges>.*?)endbfrange", RegexOptions.Singleline))
					foreach (Match range in Regex.Matches(block.Groups["ranges"].Value, @"<(?<first>[0-9A-Fa-f]+)>\s*<(?<last>[0-9A-Fa-f]+)>\s*<(?<unicode>[0-9A-Fa-f]+)>"))
					{
						var first = Convert.ToInt32(range.Groups["first"].Value, 16);
						var last = Convert.ToInt32(range.Groups["last"].Value, 16);
						for (var glyph = first; glyph <= last; glyph++)
							map[glyph] = RangeUnicode(range.Groups["unicode"].Value, glyph - first);
					}
				maps[fontName] = map;
			}
			var content = Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);
			content = Regex.Replace(content, @"/\w+\s*<<.*?>>\s*BDC", "/Span BMC", RegexOptions.Singleline);
			string? selectedFont = null;
			foreach (var operation in ContentReader.ReadContent(Encoding.Latin1.GetBytes(content)).OfType<COperator>())
			{
				if (operation.Name == "Tf") selectedFont = ((CName)operation.Operands[0]).Name;
				if (operation.Name is not ("Tj" or "TJ") || selectedFont == null) continue;
				foreach (var value in PdfStrings(operation.Operands))
					for (var offset = 0; offset + 1 < value.Length; offset += 2)
						text.Append(maps[selectedFont].GetValueOrDefault(value[offset] * 256 + value[offset + 1], "?"));
				text.AppendLine();
			}
		}
		return text.ToString();
	}

	private static IEnumerable<string> PdfStrings(CSequence operands) => operands.SelectMany(item => item switch
	{
		CString value => new[] { value.Value }, CArray array => PdfStrings(array), _ => Array.Empty<string>()
	});

	private static string RangeUnicode(string hex, int increment)
	{
		var bytes = Convert.FromHexString(hex);
		for (var index = bytes.Length - 1; index >= 0 && increment > 0; index--)
		{
			var value = bytes[index] + increment;
			bytes[index] = (byte)(value & 255);
			increment = value >> 8;
		}
		return Encoding.BigEndianUnicode.GetString(bytes);
	}
}
