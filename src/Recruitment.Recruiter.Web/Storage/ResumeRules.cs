namespace Recruitment.Recruiter.Web.Storage;

public static class ResumeRules
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public static string AcceptAttribute => string.Join(",", AllowedTypes.Keys);

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Selecione um ficheiro de currículo.";
        if (file.Length > MaxFileSizeBytes) return "O ficheiro excede o limite de 10 MB.";
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedTypes.ContainsKey(ext)) return "Formato não suportado. Use PDF, DOC ou DOCX.";
        if (!HasValidSignature(file, ext)) return "O conteúdo do ficheiro não corresponde a um PDF, DOC ou DOCX válido.";
        return null;
    }

    // File signatures ("magic numbers"): the extension alone proves nothing.
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];                       // .docx (OOXML = zip)
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]; // .doc (OLE2)

    private static bool HasValidSignature(IFormFile file, string ext)
    {
        var expected = ext.ToLowerInvariant() switch { ".pdf" => Pdf, ".docx" => Zip, ".doc" => Ole, _ => null };
        if (expected is null) return false;

        Span<byte> header = stackalloc byte[8];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, expected.Length, throwOnEndOfStream: false);
        return read >= expected.Length && header[..expected.Length].SequenceEqual(expected);
    }

    /// <summary>Content type derived from the extension (never trust the browser-sent value).</summary>
    public static string ContentTypeFor(string fileName) =>
        AllowedTypes.TryGetValue(Path.GetExtension(fileName), out var ct) ? ct : "application/octet-stream";
}
