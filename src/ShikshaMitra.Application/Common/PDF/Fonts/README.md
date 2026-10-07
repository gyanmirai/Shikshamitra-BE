# Portable PDF font

Noto Sans Gujarati supplies Gujarati glyphs without relying on fonts installed on the API host.
The application embeds and registers the font once; Latin text continues to use QuestPDF's bundled Lato family.
Missing-font and missing-glyph validation remains enabled.
The shared PDF page styles explicitly select Lato with Noto Sans Gujarati as a fallback.
Font registration alone does not select a fallback, so existing report and appraisal generators use the same style extension.
Custom document font choices retain their priority before these fallback families.
Employee cards retain their existing SVG renderer.

The font is from [Google Fonts Noto Sans Gujarati](https://github.com/google/fonts/tree/main/ofl/notosansgujarati).
The original filename is `NotoSansGujarati[wdth,wght].ttf`.
Its SHA256 is `9901D8552F1DD5D2C50DBD4CAA6F6E174E74E8264F06594AB259AE6E7B1AC428`.
The unchanged SIL Open Font License is included in `OFL-NotoSansGujarati.txt` and copied with application build/publish output.
Font embedding does not certify PDF accessibility or statutory wage-slip applicability.
