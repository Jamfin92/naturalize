using Microsoft.EntityFrameworkCore;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Naturalization.Api.Reports;

/// <summary>
/// The mailing-labels report, kept in its own partial file because it draws with
/// RAW PdfSharp (XGraphics absolute positioning) rather than the MigraDoc flow
/// model the rest of the generator uses. Avery 5160 is a fixed grid — 3 columns ×
/// 10 rows of 2.625" × 1" labels — and label paper is unforgiving: a millimetre of
/// accumulated table/cell drift and every label prints half off its sticker. So we
/// place each string at a computed (x, y) instead of letting a layout engine flow
/// it. Isolating the PdfSharp <c>X*</c> types here also keeps them from mixing with
/// MigraDoc's <c>Unit</c>/<c>Color</c> in the flow file.
/// </summary>
public partial class MigraDocReportGenerator
{
    public async Task<byte[]> MailingLabelsAsync(CancellationToken ct = default)
    {
        // The raw PdfDocument path never touches ReportTheme.NewDocument, which is
        // what normally installs the resolver — so do it here, or the first XFont
        // throws on macOS/Linux (PdfSharp ships no default resolver there).
        EmbeddedFontResolver.EnsureInstalled();

        // Live applicants only; the global soft-delete query filter already excludes
        // withdrawn records. Ordered as you'd sort a stack of labels.
        var people = await db.Applicants.AsNoTracking()
            .OrderBy(a => a.LastName)
            .ThenBy(a => a.FirstName)
            .ToListAsync(ct);

        // Avery 5160 on US Letter, everything in points (72pt = 1in). Arithmetic
        // closes exactly: 0.1875 + 3×2.625 + 2×0.125 + 0.1875 = 8.5in across;
        // 0.5 + 10×1.0 + 0.5 = 11in down.
        const double Pt = 72.0;
        const double MarginTop = 0.5 * Pt;
        const double MarginLeft = 0.1875 * Pt;
        const double LabelW = 2.625 * Pt;
        const double LabelH = 1.0 * Pt;
        const double ColPitch = 2.75 * Pt;   // 2.625" label + 0.125" gutter
        const double RowPitch = 1.0 * Pt;    // no vertical gutter
        const double PadX = 0.13 * Pt;
        const double PadY = 0.11 * Pt;
        const int Cols = 3, Rows = 10, PerPage = Cols * Rows;

        var doc = new PdfDocument();
        doc.Info.Title = "Mailing labels";
        doc.Info.Author = "Naturalize — open-source naturalization records system";

        var nameFont = new XFont(EmbeddedFontResolver.Sans, 10.5, XFontStyleEx.Bold);
        var addrFont = new XFont(EmbeddedFontResolver.Sans, 9, XFontStyleEx.Regular);
        var ink = new XSolidBrush(XColor.FromArgb(0x2A, 0x2A, 0x3C)); // ReportTheme "Ink"

        if (people.Count == 0)
        {
            var page = doc.AddPage();
            page.Size = PageSize.Letter;
            using var g = XGraphics.FromPdfPage(page);
            var note = new XFont(EmbeddedFontResolver.Sans, 12, XFontStyleEx.Regular);
            g.DrawString("No applicants to print.", note, ink,
                new XRect(0, 0, page.Width.Point, page.Height.Point), XStringFormats.Center);
            return RenderRaw(doc);
        }

        XGraphics? gfx = null;
        try
        {
            for (var i = 0; i < people.Count; i++)
            {
                var slot = i % PerPage;
                if (slot == 0)                       // a fresh sheet every 30 labels
                {
                    gfx?.Dispose();
                    var page = doc.AddPage();
                    page.Size = PageSize.Letter;     // 8.5" × 11", portrait
                    gfx = XGraphics.FromPdfPage(page);
                }

                var col = slot % Cols;               // row-major fill: left→right, top→bottom
                var row = slot / Cols;
                var x = MarginLeft + col * ColPitch;
                var y = MarginTop + row * RowPitch;
                var cell = new XRect(x + PadX, y + PadY, LabelW - 2 * PadX, LabelH - 2 * PadY);

                var a = people[i];
                var cityLine = $"{a.City}, {a.State} {a.PostalCode}".Trim(',', ' ');
                var lines = new (string Text, XFont Font)[]
                {
                    (a.FullName, nameFont),      // computed "First Middle Last"
                    (a.AddressLine, addrFont),
                    (cityLine, addrFont),
                };

                // Clip to the sticker so an overlong street or name can't bleed into
                // the neighbouring label.
                var state = gfx!.Save();
                gfx.IntersectClip(cell);
                var ty = cell.Y;
                foreach (var (text, font) in lines)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var lh = font.GetHeight();
                    if (ty + lh > cell.Y + cell.Height) break;   // out of vertical room
                    gfx.DrawString(text, font, ink, new XPoint(cell.X, ty), XStringFormats.TopLeft);
                    ty += lh;
                }
                gfx.Restore(state);
            }
        }
        finally
        {
            gfx?.Dispose();
        }

        return RenderRaw(doc);
    }

    private static byte[] RenderRaw(PdfDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }
}
