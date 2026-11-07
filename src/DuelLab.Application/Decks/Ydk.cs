using System.Globalization;
using System.Text;
using DuelLab.Domain.Decks;

namespace DuelLab.Application.Decks;

/// <summary>A deck read from a .ydk file: passcodes per zone, in file order, plus anything skipped.</summary>
public sealed record YdkDeck(IReadOnlyList<long> Main, IReadOnlyList<long> Extra, IReadOnlyList<long> Side, IReadOnlyList<string> Warnings)
{
    /// <summary>The passcodes of a zone.</summary>
    public IReadOnlyList<long> In(DeckZone zone) => zone switch
    {
        DeckZone.Main => Main,
        DeckZone.Extra => Extra,
        _ => Side,
    };
}

/// <summary>
/// Reads and writes the .ydk deck format (YGOPro, EDOPro, YGOPRODeck and others): a <c>#main</c>,
/// <c>#extra</c> and <c>!side</c> section, each listing one card passcode per line.
/// </summary>
/// <remarks>
/// Real files vary: Windows or Unix line endings, a byte-order mark, trailing spaces after headers,
/// comment lines such as <c>#created by ...</c>, and passcodes without their leading zeros. All of
/// these are accepted. Lines that are not passcodes are skipped with a warning.
/// </remarks>
public static class Ydk
{
    /// <summary>Largest accepted file (a full deck is well under 2 KB).</summary>
    public const int MaxLength = 64 * 1024;

    /// <summary>Most passcodes accepted in one file.</summary>
    public const int MaxCards = 200;

    /// <summary>Parses a .ydk file's text.</summary>
    /// <exception cref="Common.RequestValidationException">The text is too large, has too many cards or none.</exception>
    public static YdkDeck Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MaxLength)
        {
            throw new Common.RequestValidationException("import.too_large", "That file is too large to be a .ydk deck file.");
        }

        var zones = new Dictionary<DeckZone, List<long>> { [DeckZone.Main] = [], [DeckZone.Extra] = [], [DeckZone.Side] = [] };
        var warnings = new List<string>();
        var zone = DeckZone.Main;
        var lineNumber = 0;

        foreach (var raw in content.TrimStart('﻿').Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#main", StringComparison.OrdinalIgnoreCase))
            {
                zone = DeckZone.Main;
            }
            else if (line.StartsWith("#extra", StringComparison.OrdinalIgnoreCase))
            {
                zone = DeckZone.Extra;
            }
            else if (line.StartsWith("!side", StringComparison.OrdinalIgnoreCase))
            {
                zone = DeckZone.Side;
            }
            else if (line.StartsWith('#') || line.StartsWith('!'))
            {
                // Comments such as "#created by ...".
            }
            else
            {
                var token = line.Split([' ', '\t'], 2)[0];
                if (long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var passcode) && passcode > 0)
                {
                    zones[zone].Add(passcode);
                }
                else
                {
                    warnings.Add($"Line {lineNumber} ('{Truncate(line)}') is not a card passcode and was skipped.");
                }
            }
        }

        var total = zones.Values.Sum(z => z.Count);
        if (total == 0)
        {
            throw new Common.RequestValidationException("import.empty", "No card passcodes found. Is this a .ydk deck file?");
        }

        if (total > MaxCards)
        {
            throw new Common.RequestValidationException("import.too_many", $"The file lists {total} cards; a deck has at most {DeckRules.MainDeckMax + DeckRules.ExtraDeckMax + DeckRules.SideDeckMax}.");
        }

        return new YdkDeck(zones[DeckZone.Main], zones[DeckZone.Extra], zones[DeckZone.Side], warnings);
    }

    /// <summary>Writes a .ydk file: one line per copy, using each card's main passcode.</summary>
    public static string Write(IEnumerable<(long Passcode, DeckZone Zone, int Quantity)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        var text = new StringBuilder("#created by DuelLab\n");
        foreach (var (header, zone) in new[] { ("#main", DeckZone.Main), ("#extra", DeckZone.Extra), ("!side", DeckZone.Side) })
        {
            text.Append(header).Append('\n');
            foreach (var (passcode, _, quantity) in list.Where(e => e.Zone == zone))
            {
                for (var i = 0; i < quantity; i++)
                {
                    // Passcodes are 8 digits; keep the leading zeros other tools expect.
                    text.Append(passcode.ToString("D8", CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }

        return text.ToString();
    }

    private static string Truncate(string text) => text.Length <= 30 ? text : text[..30] + "…";
}
