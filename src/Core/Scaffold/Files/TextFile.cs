using System.Text;
using Tsikhanau.Monolith.Scaffold.Plans;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Files;

internal sealed record TextFile(String Path, Byte[] Content)
{
    private static readonly Byte[] ByteOrderMark = [.. Encoding.UTF8.Preamble];

    private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    public String Text { get; } = Decode(Content);

    public String NewLine => Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private Boolean HasByteOrderMark => StartsWithByteOrderMark(Content);

    public Result<FileEdit> PlanEdit(Func<String, Result<String>> edit) =>
        edit(Text)
            .Map(updatedText => new FileEdit(Path, Content, Encode(updatedText)));

    public Result<FileEdit> PlanLineEdit(Func<TextLines, Result<TextLines>> edit) =>
        PlanEdit(text =>
            edit(TextLines.Split(text, NewLine))
                .Map(updatedLines => updatedLines.Join()));

    public Error MissingAnchor(String expected) => MissingAnchor(Path, expected);

    public static Error MissingAnchor(String path, String expected) =>
        Error.Failure("scaffold.anchor", $"{path} must hold {expected}");

    private Byte[] Encode(String text) =>
        HasByteOrderMark
            ? [.. ByteOrderMark, .. Utf8WithoutByteOrderMark.GetBytes(text)]
            : Utf8WithoutByteOrderMark.GetBytes(text);

    private static String Decode(Byte[] content) =>
        Utf8WithoutByteOrderMark.GetString(content.AsSpan(StartsWithByteOrderMark(content) ? ByteOrderMark.Length : 0));

    private static Boolean StartsWithByteOrderMark(Byte[] content) =>
        content
            .AsSpan()
            .StartsWith(ByteOrderMark);
}
