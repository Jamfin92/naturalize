using System.Reflection;
using PdfSharp.Fonts;

namespace Naturalization.Api.Reports;

/// <summary>
/// PdfSharp has NO default font resolver on macOS or Linux — without one, the
/// very first render throws. So the reports carry their own fonts: PT Serif and
/// PT Sans TTFs are compiled in as embedded resources and served from here.
///
/// The upshot is that report output is byte-identical on a developer's Mac, in
/// CI, and inside a slim Linux container, none of which need any system font
/// installed. Both families are SIL OFL, so redistributing them inside this
/// repo and embedding them into generated PDFs are both permitted.
/// </summary>
public sealed class EmbeddedFontResolver : IFontResolver
{
    public const string Serif = "PT Serif";
    public const string Sans = "PT Sans";

    private const string Prefix = "Naturalization.Api.Reports.Fonts.";

    // Face name -> embedded resource file name.
    private static readonly Dictionary<string, string> Faces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PTSerif"] = "PT_Serif-Web-Regular.ttf",
        ["PTSerif-Bold"] = "PT_Serif-Web-Bold.ttf",
        ["PTSerif-Italic"] = "PT_Serif-Web-Italic.ttf",
        ["PTSans"] = "PT_Sans-Web-Regular.ttf",
        ["PTSans-Bold"] = "PT_Sans-Web-Bold.ttf"
    };

    private static readonly Dictionary<string, byte[]> Cache = new(StringComparer.OrdinalIgnoreCase);

    // Plain object, not System.Threading.Lock — that type is .NET 9+, and this
    // targets net8.0.
    private static readonly object Gate = new();

    /// <summary>
    /// Installs the resolver. GlobalFontSettings.FontResolver is a static
    /// singleton that throws if assigned twice — which bites under `dotnet
    /// watch` and in parallel test runs, where the assembly is reloaded but the
    /// static survives. Hence the idempotent guard rather than a bare assignment.
    /// </summary>
    public static void EnsureInstalled()
    {
        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is not null) return;
            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var family = familyName.Replace(" ", "", StringComparison.Ordinal);

        // PT Sans ships no italic here; fall back to regular rather than
        // returning null, which would surface as a hard failure mid-render.
        var suffix = (isBold, isItalic) switch
        {
            (true, _) => "-Bold",
            (false, true) when family.Equals("PTSerif", StringComparison.OrdinalIgnoreCase) => "-Italic",
            _ => ""
        };

        var face = family + suffix;
        if (Faces.ContainsKey(face)) return new FontResolverInfo(face);

        // Unknown family: fall back to the body sans so a stray style name
        // degrades to readable text instead of killing the request.
        return new FontResolverInfo(isBold ? "PTSans-Bold" : "PTSans");
    }

    public byte[]? GetFont(string faceName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(faceName, out var cached)) return cached;

            if (!Faces.TryGetValue(faceName, out var file))
                throw new InvalidOperationException($"No embedded font for face '{faceName}'.");

            var resource = Prefix + file;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException(
                    $"Embedded font resource '{resource}' not found. Check the EmbeddedResource " +
                    "glob in Naturalization.Api.csproj.");

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            Cache[faceName] = bytes;
            return bytes;
        }
    }
}
