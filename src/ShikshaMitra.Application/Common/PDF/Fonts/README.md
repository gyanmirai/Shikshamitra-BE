# Portable PDF fonts

Noto Sans Gujarati supplies Gujarati glyphs, and Noto Sans Devanagari supplies Devanagari glyphs without relying on fonts installed on the API host.
The application embeds and registers these fonts once; Latin text continues to use QuestPDF's bundled Lato family.
Missing-font and missing-glyph validation remains enabled.
The shared PDF page styles select Lato, then Noto Sans Gujarati and Noto Sans Devanagari as fallbacks.
Font registration alone does not select a fallback, so existing report and appraisal generators use the same style extension.
Custom document font choices retain their priority before these fallback families.
Employee cards retain their existing SVG renderer.

The font is from [Google Fonts Noto Sans Gujarati](https://github.com/google/fonts/tree/main/ofl/notosansgujarati).
The original filename is `NotoSansGujarati[wdth,wght].ttf`.
Its SHA256 is `9901D8552F1DD5D2C50DBD4CAA6F6E174E74E8264F06594AB259AE6E7B1AC428`.
The unchanged SIL Open Font License is included in `OFL-NotoSansGujarati.txt` and copied with application build/publish output.

The Devanagari Regular and Bold files are copied unchanged from `Shikshamitra-FE/public/fonts/` in the wrapper workspace.
Their unchanged SIL Open Font License is included in `NotoSansDevanagari-OFL.txt` and copied with application build/publish output.

| Font file | SHA256 |
|---|---|
| `NotoSansDevanagari-Regular.ttf` | `6600A318C94F6C7D5D6EA6E0133C017748C6449F426558ADB9691747D2165613` |
| `NotoSansDevanagari-Bold.ttf` | `68AD1DC7AED6FF8D8EA32414AACA0039C47DAD57F019E9A108ADC0F63386B991` |

Font embedding does not certify PDF accessibility or statutory wage-slip applicability.
