using System.Text;
using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Files;

internal sealed record TextFile(String Path, Byte[] Content)
{
    private static readonly Byte[] ByteOrderMark = [.. Encoding.UTF8.Preamble];

    private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    public String Text =>
        Utf8WithoutByteOrderMark.GetString(Content.AsSpan(HasByteOrderMark ? ByteOrderMark.Length : 0));

    public String NewLine => Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private Boolean HasByteOrderMark =>
        Content
            .AsSpan()
            .StartsWith(ByteOrderMark);

    public Result<FileEdit> PlanEdit(Func<String, Result<String>> edit) =>
        edit(Text)
            .Map(updatedText => new FileEdit(Path, Content, Encode(updatedText)));

    private Byte[] Encode(String text) =>
        HasByteOrderMark
            ? [.. ByteOrderMark, .. Utf8WithoutByteOrderMark.GetBytes(text)]
            : Utf8WithoutByteOrderMark.GetBytes(text);
}
