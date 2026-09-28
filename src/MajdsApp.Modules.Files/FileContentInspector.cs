namespace MajdsApp.Modules.Files;

/// <summary>
/// Looks at what an uploaded file really is, from its first bytes, instead of trusting the name or the content type the client sent (F-Files FR-FILE-002).
/// A program, whatever it is called, is refused; a file named as an image, a PDF or an archive must be one; and the content type stored (and later served) comes from
/// what was found, never from the client.
/// </summary>
public static class FileContentInspector
{
    public const int HeaderBytes = 16;

    public readonly record struct Result(bool Allowed, string ContentType, string? Problem);

    // Signatures of things that run: Windows programs, Linux programs, macOS programs, Java classes and scripts with a shebang.
    private static readonly byte[][] ProgramSignatures =
    [
        "MZ"u8.ToArray(), [0x7F, (byte)'E', (byte)'L', (byte)'F'], [0xFE, 0xED, 0xFA, 0xCE], [0xFE, 0xED, 0xFA, 0xCF], [0xCE, 0xFA, 0xED, 0xFE], [0xCF, 0xFA, 0xED, 0xFE],
        [0xCA, 0xFE, 0xBA, 0xBE], "#!"u8.ToArray()
    ];

    // Files whose name promises a format that can be checked: extension -> (content type, does the header match).
    private static readonly Dictionary<string, (string ContentType, Func<byte[], bool> Matches)> Checked = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = ("image/png", h => Starts(h, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])),
        [".jpg"] = ("image/jpeg", h => Starts(h, [0xFF, 0xD8, 0xFF])),
        [".jpeg"] = ("image/jpeg", h => Starts(h, [0xFF, 0xD8, 0xFF])),
        [".gif"] = ("image/gif", h => Starts(h, "GIF87a"u8.ToArray()) || Starts(h, "GIF89a"u8.ToArray())),
        [".webp"] = ("image/webp", h => Starts(h, "RIFF"u8.ToArray()) && h.Length >= 12 && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P'),
        [".pdf"] = ("application/pdf", h => Starts(h, "%PDF-"u8.ToArray())),
        [".zip"] = ("application/zip", IsZip),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", IsZip),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", IsZip),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", IsZip)
    };

    // Plain text formats have no signature; they are served as the type their extension says, and anything else as an opaque download.
    private static readonly Dictionary<string, string> PlainText = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain", [".csv"] = "text/csv", [".json"] = "application/json", [".md"] = "text/markdown", [".xml"] = "application/xml"
    };

    public static Result Inspect(string fileName, byte[] header)
    {
        if (ProgramSignatures.Any(signature => Starts(header, signature)))
            return new Result(false, "application/octet-stream", "programs and scripts cannot be uploaded, whatever the file is called");

        var extension = Path.GetExtension(fileName);
        if (Checked.TryGetValue(extension, out var known))
            return known.Matches(header)
                ? new Result(true, known.ContentType, null)
                : new Result(false, known.ContentType, $"the content is not a {extension.TrimStart('.').ToUpperInvariant()} file");

        return new Result(true, PlainText.GetValueOrDefault(extension, "application/octet-stream"), null);
    }

    private static bool IsZip(byte[] h) => Starts(h, [0x50, 0x4B, 0x03, 0x04]) || Starts(h, [0x50, 0x4B, 0x05, 0x06]);

    private static bool Starts(byte[] header, byte[] prefix) => header.Length >= prefix.Length && header.AsSpan(0, prefix.Length).SequenceEqual(prefix);
}
