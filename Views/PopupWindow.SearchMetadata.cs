using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void UpdateSearchMetadataPreviews(SearchQuerySpec spec)
    {
        foreach (var item in _displayItems)
            item.SearchMetadataPreviewChips = BuildSearchMetadataPreviewChips(item, spec);
    }

    private static SearchMetadataChip[]? BuildSearchMetadataPreviewChips(ClipboardEntry item, SearchQuerySpec spec)
    {
        var chips = new List<SearchMetadataChip>();
        if (!string.IsNullOrWhiteSpace(item.ShortcutPhrase))
            chips.Add(new SearchMetadataChip(item.ShortcutPhrase, !spec.IsEmpty && SourceMetadataPartMatchesQuery(item.ShortcutPhrase, spec)));

        if (!spec.IsEmpty && item.Source != null && item.Source.HasAny)
        {
            var sourceSearchText = item.SourceSearchText;
            var displayParts = item.SourceMetadataDisplayParts;
            var displayText = string.Join(" ", displayParts);
            if (!string.IsNullOrWhiteSpace(sourceSearchText)
                && displayParts.Length > 0
                && SourceMetadataMatchesQuery(sourceSearchText, displayText, spec))
            {
                chips.AddRange(displayParts
                    .Select(part => new SearchMetadataChip(part, SourceMetadataPartMatchesQuery(part, spec))));
            }
        }

        return chips.Count > 0 ? chips.ToArray() : null;
    }

    private static bool SourceMetadataMatchesQuery(string sourceSearchText, string displayText, SearchQuerySpec spec)
    {
        if (spec.MatchesTextOrPinyin(
                sourceSearchText,
                (_, token) => SourceMetadataTokenMatches(sourceSearchText, displayText, token)))
            return true;

        foreach (var token in spec.BroadTokens)
        {
            if (SourceMetadataTokenMatches(sourceSearchText, displayText, token))
                return true;
        }

        return false;
    }

    private static bool SourceMetadataTokenMatches(string sourceSearchText, string displayText, string token)
    {
        return sourceSearchText.Contains(token, StringComparison.OrdinalIgnoreCase)
            || displayText.Contains(token, StringComparison.OrdinalIgnoreCase)
            || PinyinSearchIndex.MatchesToken(sourceSearchText, token, ClipboardEntry.PinyinFilterMode)
            || PinyinSearchIndex.MatchesToken(displayText, token, ClipboardEntry.PinyinFilterMode);
    }

    private static bool SourceMetadataPartMatchesQuery(string part, SearchQuerySpec spec)
    {
        if (string.IsNullOrWhiteSpace(part)) return false;
        if (spec.MatchesTextOrPinyin(part, (_, token) => SourceMetadataTokenMatches(part, part, token)))
            return true;

        foreach (var token in spec.BroadTokens)
        {
            if (SourceMetadataTokenMatches(part, part, token))
                return true;
        }

        return false;
    }
}
