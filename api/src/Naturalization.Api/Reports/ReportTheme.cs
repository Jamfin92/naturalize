using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace Naturalization.Api.Reports;

/// <summary>
/// Shared chrome for every report: the palette, the named styles, and the
/// page furniture.
///
/// The colours are the same "Parchment &amp; Old Glory" tokens the web app uses,
/// converted out of OKLCH to sRGB — so a printed report and the screen it came
/// from are recognisably the same product.
///
/// This is the ~200-line builder that buys back MigraDoc's ergonomics gap
/// versus QuestPDF. Paid once, here, rather than taxing every downstream user
/// with a revenue-conditioned licence forever.
/// </summary>
internal static class ReportTheme
{
    // Old Glory Blue #3C3B6E, Old Glory Red #B22234, brass, and warm neutrals.
    internal static readonly Color Navy = new(0x3C, 0x3B, 0x6E);
    internal static readonly Color Red = new(0xB2, 0x22, 0x34);
    internal static readonly Color Brass = new(0xC0, 0x92, 0x2B);
    internal static readonly Color Ink = new(0x2A, 0x2A, 0x3C);
    internal static readonly Color Muted = new(0x6B, 0x6B, 0x7B);
    internal static readonly Color Rule = new(0xD8, 0xD2, 0xC4);
    internal static readonly Color Parchment = new(0xF7, 0xF4, 0xEC);
    internal static readonly Color Green = new(0x2E, 0x7D, 0x53);

    internal static Document NewDocument(string title, string subtitle)
    {
        EmbeddedFontResolver.EnsureInstalled();

        var doc = new Document
        {
            Info =
            {
                Title = title,
                Subject = subtitle,
                Author = "Naturalize — open-source naturalization records system"
            }
        };

        DefineStyles(doc);

        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;

        /*
         * The header block (title + subtitle + two rules) lives inside the top
         * margin, so the margin must be TALLER than the block or the body draws
         * straight over it. 2cm was not, and the first section heading landed on
         * top of the subtitle. Keep these two in step if the header grows.
         */
        section.PageSetup.TopMargin = Unit.FromCentimeter(3.6);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(1.0);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);

        AddHeader(section, title, subtitle);
        AddFooter(section);

        return doc;
    }

    private static void DefineStyles(Document doc)
    {
        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = EmbeddedFontResolver.Sans;
        normal.Font.Size = 9.5;
        normal.Font.Color = Ink;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);

        var title = doc.Styles.AddStyle("ReportTitle", "Normal");
        title.Font.Name = EmbeddedFontResolver.Serif;
        title.Font.Size = 19;
        title.Font.Bold = true;
        title.Font.Color = Navy;
        title.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);

        var subtitle = doc.Styles.AddStyle("ReportSubtitle", "Normal");
        subtitle.Font.Size = 9;
        subtitle.Font.Color = Muted;

        var h2 = doc.Styles.AddStyle("SectionHeading", "Normal");
        h2.Font.Name = EmbeddedFontResolver.Serif;
        h2.Font.Size = 12;
        h2.Font.Bold = true;
        h2.Font.Color = Navy;
        h2.ParagraphFormat.SpaceBefore = Unit.FromPoint(14);
        h2.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        h2.ParagraphFormat.Borders.Bottom = new Border { Width = 0.75, Color = Brass };

        var label = doc.Styles.AddStyle("FieldLabel", "Normal");
        label.Font.Size = 7.5;
        label.Font.Bold = true;
        label.Font.Color = Muted;
        label.ParagraphFormat.SpaceAfter = Unit.FromPoint(1);

        var value = doc.Styles.AddStyle("FieldValue", "Normal");
        value.Font.Size = 10;
        value.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);

        var th = doc.Styles.AddStyle("TableHead", "Normal");
        th.Font.Size = 8;
        th.Font.Bold = true;
        th.Font.Color = Colors.White;

        var td = doc.Styles.AddStyle("TableCell", "Normal");
        td.Font.Size = 9;

        var footer = doc.Styles.AddStyle("ReportFooter", "Normal");
        footer.Font.Size = 7.5;
        footer.Font.Color = Muted;
    }

    private static void AddHeader(Section section, string title, string subtitle)
    {
        var header = section.Headers.Primary;

        var t = header.AddParagraph(title);
        t.Style = "ReportTitle";

        var s = header.AddParagraph(subtitle);
        s.Style = "ReportSubtitle";
        s.Format.SpaceAfter = Unit.FromPoint(6);

        // Two rules — brass over navy — echoing the accent bar in the web UI.
        var bar = header.AddParagraph();
        bar.Format.Borders.Top = new Border { Width = 2.5, Color = Brass };
        bar.Format.SpaceAfter = Unit.FromPoint(1);

        var bar2 = header.AddParagraph();
        bar2.Format.Borders.Top = new Border { Width = 0.75, Color = Navy };
        bar2.Format.SpaceAfter = Unit.FromPoint(12);
    }

    private static void AddFooter(Section section)
    {
        var footer = section.Footers.Primary;

        var p = footer.AddParagraph();
        p.Style = "ReportFooter";
        p.Format.Borders.Top = new Border { Width = 0.5, Color = Rule };
        p.Format.SpaceBefore = Unit.FromPoint(6);
        p.AddText(
            $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  ·  Naturalize (open source)  ·  " +
            "Not affiliated with USCIS. Demo data.  ·  Page ");
        p.AddPageField();
        p.AddText(" of ");
        p.AddNumPagesField();
    }

    /// <summary>A label/value pair, stacked. Used for record particulars.</summary>
    internal static void AddField(Cell cell, string label, string value)
    {
        var l = cell.AddParagraph(label.ToUpperInvariant());
        l.Style = "FieldLabel";

        var v = cell.AddParagraph(string.IsNullOrWhiteSpace(value) ? "—" : value);
        v.Style = "FieldValue";
    }

    /// <summary>A borderless grid used to lay fields out in columns.</summary>
    internal static Table AddFieldGrid(Section section, int columns)
    {
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.TopPadding = Unit.FromPoint(2);
        table.BottomPadding = Unit.FromPoint(2);

        var width = Unit.FromCentimeter(16.6 / columns);
        for (var i = 0; i < columns; i++) table.AddColumn(width);

        return table;
    }

    /// <summary>A data table with a navy header band and zebra striping.</summary>
    internal static Table AddDataTable(Section section, params (string Header, double Cm)[] columns)
    {
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.Borders.Color = Rule;
        table.LeftPadding = Unit.FromPoint(5);
        table.RightPadding = Unit.FromPoint(5);
        table.TopPadding = Unit.FromPoint(4);
        table.BottomPadding = Unit.FromPoint(4);

        foreach (var (_, cm) in columns) table.AddColumn(Unit.FromCentimeter(cm));

        var head = table.AddRow();
        head.Shading.Color = Navy;
        head.HeadingFormat = true; // repeat the header on every page break
        head.TopPadding = Unit.FromPoint(5);
        head.BottomPadding = Unit.FromPoint(5);

        for (var i = 0; i < columns.Length; i++)
        {
            var p = head.Cells[i].AddParagraph(columns[i].Header.ToUpperInvariant());
            p.Style = "TableHead";
        }

        return table;
    }

    internal static Row AddZebraRow(Table table, int index)
    {
        var row = table.AddRow();
        if (index % 2 == 1) row.Shading.Color = Parchment;
        row.Borders.Bottom = new Border { Width = 0.4, Color = Rule };
        return row;
    }

    internal static void SetCell(Row row, int column, string text, bool bold = false, Color? color = null)
    {
        var p = row.Cells[column].AddParagraph(text);
        p.Style = "TableCell";
        p.Format.Font.Bold = bold;
        if (color is not null) p.Format.Font.Color = color.Value;
    }

    /// <summary>Outcomes and statuses are colour-coded the same way as on screen.</summary>
    internal static Color StatusColor(string status) => status switch
    {
        "Approved" or "Naturalized" => Green,
        "Denied" => Red,
        "Withdrawn" => Muted,
        _ => Ink
    };

    /// <summary>
    /// Enum names are terse and CamelCased. Mirrors STATUS_LABELS in the
    /// frontend's types.ts.
    /// </summary>
    internal static string StatusLabel(string status) => status switch
    {
        "InReview" => "In review",
        _ => status
    };

    /// <summary>
    /// A proportional bar.
    ///
    /// Built as a shaded paragraph whose right indent is widened to shrink it:
    /// paragraph shading paints the content box, so indenting from the right by
    /// (max - value) leaves a filled rectangle of exactly the width wanted.
    ///
    /// Two more obvious approaches are both wrong here. Repeating U+2588 FULL
    /// BLOCK renders as a row of tofu, because PT Sans has no such glyph — that
    /// was the first version, and it shipped straight into a screenshot. And a
    /// floating TextFrame is not reliably laid out inside a table cell. Shading
    /// plus indent uses only primitives MigraDoc is certain to honour.
    /// </summary>
    internal static void AddBar(Cell cell, int value, int max, double maxWidthCm)
    {
        if (value <= 0)
        {
            var zero = cell.AddParagraph("0");
            zero.Style = "TableCell";
            zero.Format.Font.Color = Rule;
            return;
        }

        var widthCm = Math.Max(0.10, maxWidthCm * value / Math.Max(1, max));

        // U+00A0 rather than a plain space: a trailing space can be collapsed
        // away, and a paragraph with no content paints no shading.
        var bar = cell.AddParagraph(" ");
        bar.Format.Font.Size = 7;
        bar.Format.Shading.Color = Navy;
        bar.Format.RightIndent = Unit.FromCentimeter(Math.Max(0, maxWidthCm - widthCm));
        bar.Format.SpaceBefore = Unit.FromPoint(1);
        bar.Format.SpaceAfter = Unit.FromPoint(1);
    }
}
