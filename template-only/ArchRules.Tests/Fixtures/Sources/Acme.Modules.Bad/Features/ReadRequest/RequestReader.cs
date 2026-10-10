using Microsoft.AspNetCore.Http;

namespace Acme.Modules.Bad.Features.ReadRequest;

internal sealed class RequestReader
{
    public String Read(HttpContext context) => context.TraceIdentifier;
}
