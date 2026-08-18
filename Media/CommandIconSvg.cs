using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svg;

namespace ClipboardManager;

public enum CommandIconKind
{
    Clipboard,
    Pin,
    Settings,
    Search,
    Empty,
    Paste,
    OpenUrl,
    Folder,
    Json,
    Edit,
    QuickPhrase,
    Favorite,
    Delete,
    Batch,
    Text,
    Image,
    File,
    Filter,
}

/// <summary>Color SVG command logos rendered once and shared as frozen WPF images.</summary>
public static class CommandIconSvg
{
    private const int RenderSize = 64;
    private static readonly Dictionary<CommandIconKind, ImageSource> Cache = new();
    private static readonly object CacheLock = new();

    public static ImageSource Get(CommandIconKind kind)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(kind, out var cached)) return cached;
            var rendered = Render(kind);
            Cache[kind] = rendered;
            return rendered;
        }
    }

    private static ImageSource Render(CommandIconKind kind)
    {
        var (color, glyph) = kind switch
        {
            CommandIconKind.Clipboard => ("#139493", ClipboardGlyph),
            CommandIconKind.Pin => ("#8B5CF6", PinGlyph),
            CommandIconKind.Settings => ("#64748B", SettingsGlyph),
            CommandIconKind.Search => ("#0EA5E9", SearchGlyph),
            CommandIconKind.Empty => ("#64748B", EmptyGlyph),
            CommandIconKind.Paste => ("#1687E8", PasteGlyph),
            CommandIconKind.OpenUrl => ("#06A77D", GlobeGlyph),
            CommandIconKind.Folder => ("#D99A24", FolderGlyph),
            CommandIconKind.Json => ("#7C3AED", JsonGlyph),
            CommandIconKind.Edit => ("#F59E0B", EditGlyph),
            CommandIconKind.QuickPhrase => ("#EAB308", LightningGlyph),
            CommandIconKind.Favorite => ("#A855F7", StarGlyph),
            CommandIconKind.Delete => ("#DC4C4C", DeleteGlyph),
            CommandIconKind.Batch => ("#4F46E5", BatchGlyph),
            CommandIconKind.Text => ("#1687E8", TextGlyph),
            CommandIconKind.Image => ("#EC4899", ImageGlyph),
            CommandIconKind.File => ("#D99A24", FileGlyph),
            CommandIconKind.Filter => ("#06A77D", FilterGlyph),
            _ => ("#64748B", EmptyGlyph),
        };

        var svg = $"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <rect x="20" y="20" width="216" height="216" rx="54" fill="{color}"/>
  {glyph}
</svg>
""";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        var document = SvgDocument.Open<SvgDocument>(input);
        using var bitmap = document.Draw(RenderSize, RenderSize);
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = png;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private const string ClipboardGlyph = "<path d=\"M78 71h100v128H78z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"16\"/><path d=\"M103 57h50v30h-50zM99 119h58M99 151h58\" fill=\"none\" stroke=\"#fff\" stroke-width=\"14\" stroke-linecap=\"round\"/>";
    private const string PinGlyph = "<path d=\"M92 61h72l-13 51 28 29v14h-43v43l-16 22v-65H77v-14l28-29z\" fill=\"#fff\"/>";
    private const string SettingsGlyph = "<path d=\"M128 55l15 21 25-4 8 24 24 8-4 25 21 15-15 21 4 25-24 8-8 24-25-4-15 21-21-15-25 4-8-24-24-8 4-25-21-15 15-21-4-25 24-8 8-24 25 4z\" fill=\"#fff\"/><circle cx=\"128\" cy=\"144\" r=\"31\" fill=\"#64748B\"/>";
    private const string SearchGlyph = "<circle cx=\"112\" cy=\"111\" r=\"48\" fill=\"none\" stroke=\"#fff\" stroke-width=\"17\"/><path d=\"M147 146l48 48\" stroke=\"#fff\" stroke-width=\"18\" stroke-linecap=\"round\"/>";
    private const string EmptyGlyph = "<path d=\"M61 91h134l18 92H43z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"16\" stroke-linejoin=\"round\"/><path d=\"M51 142h42l12 20h46l12-20h42\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\"/>";
    private const string PasteGlyph = "<path d=\"M73 67h110v132H73z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\"/><path d=\"M103 55h50v29h-50zM128 103v61m-24-24 24 24 24-24\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
    private const string GlobeGlyph = "<circle cx=\"128\" cy=\"128\" r=\"70\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\"/><path d=\"M58 128h140M128 58c25 23 35 46 35 70s-10 47-35 70c-25-23-35-46-35-70s10-47 35-70z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"12\"/>";
    private const string FolderGlyph = "<path d=\"M51 83h66l18 20h70v91H51z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"16\" stroke-linejoin=\"round\"/>";
    private const string JsonGlyph = "<path d=\"M109 59H92c-17 0-18 15-18 29v20c0 14-8 20-20 20 12 0 20 6 20 20v20c0 14 1 29 18 29h17M147 59h17c17 0 18 15 18 29v20c0 14 8 20 20 20-12 0-20 6-20 20v20c0 14-1 29-18 29h-17\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\" stroke-linecap=\"round\"/>";
    private const string EditGlyph = "<path d=\"M68 172l9-42 80-80 40 40-80 80z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"16\" stroke-linejoin=\"round\"/><path d=\"M146 62l38 38M68 172l45-6\" stroke=\"#fff\" stroke-width=\"15\"/>";
    private const string LightningGlyph = "<path d=\"M139 47L72 143h48l-5 67 69-103h-49z\" fill=\"#fff\" stroke=\"#fff\" stroke-width=\"7\" stroke-linejoin=\"round\"/>";
    private const string StarGlyph = "<path d=\"M128 49l23 48 53 8-39 37 10 53-47-25-47 25 10-53-39-37 53-8z\" fill=\"#fff\" stroke=\"#fff\" stroke-width=\"6\" stroke-linejoin=\"round\"/>";
    private const string DeleteGlyph = "<path d=\"M78 88h100l-8 112H86zM68 72h120M105 72V55h46v17M108 112v58M148 112v58\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
    private const string BatchGlyph = "<rect x=\"61\" y=\"61\" width=\"94\" height=\"94\" rx=\"13\" fill=\"none\" stroke=\"#fff\" stroke-width=\"14\"/><rect x=\"101\" y=\"101\" width=\"94\" height=\"94\" rx=\"13\" fill=\"none\" stroke=\"#fff\" stroke-width=\"14\"/>";
    private const string TextGlyph = "<path d=\"M69 70h118M128 70v120M96 190h64\" stroke=\"#fff\" stroke-width=\"18\" stroke-linecap=\"round\"/>";
    private const string ImageGlyph = "<rect x=\"55\" y=\"62\" width=\"146\" height=\"132\" rx=\"15\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\"/><circle cx=\"101\" cy=\"103\" r=\"14\" fill=\"#fff\"/><path d=\"M70 176l43-44 28 27 20-21 25 38\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\" stroke-linejoin=\"round\"/>";
    private const string FileGlyph = "<path d=\"M77 53h70l35 35v115H77zM147 53v36h35\" fill=\"none\" stroke=\"#fff\" stroke-width=\"15\" stroke-linejoin=\"round\"/><path d=\"M101 128h57M101 159h57\" stroke=\"#fff\" stroke-width=\"13\" stroke-linecap=\"round\"/>";
    private const string FilterGlyph = "<path d=\"M55 66h146l-56 63v57l-34 20v-77z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"16\" stroke-linejoin=\"round\"/>";
}

[MarkupExtensionReturnType(typeof(ImageSource))]
public sealed class SvgIconExtension : MarkupExtension
{
    public CommandIconKind Kind { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => CommandIconSvg.Get(Kind);
}
