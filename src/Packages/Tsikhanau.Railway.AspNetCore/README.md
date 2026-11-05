# Tsikhanau.Railway.AspNetCore

ASP.NET Core integration for [Tsikhanau.Railway](https://www.nuget.org/packages/Tsikhanau.Railway): turn `Result<T>` and `Error` into HTTP results with ProblemDetails. Works in Minimal APIs and in controllers (actions can return `IResult`).

## Installation

```bash
dotnet add package Tsikhanau.Railway.AspNetCore
```

## Usage

```csharp
using Tsikhanau.Railway;

builder.Services.AddProblemDetails();

app.MapGet("/users/{id}", (Guid id, UserService users) =>
    users.GetUser(id)
        .MapAsync(u => new UserDto(u.Id, u.Name))
        .ToHttpResultAsync());

app.MapPost("/users", (CreateUser request, UserService users) =>
    users.Create(request)
        .ToHttpResultAsync(user => TypedResults.Created($"/users/{user.Id}", user)));

app.MapDelete("/users/{id}", (Guid id, UserService users) =>
    users.Delete(id).ToHttpResultAsync());
```

- Success of `Result<T>` gives `200 OK` with the value as JSON; success of `Result<Unit>` gives `204 No Content`.
- Pass your own success result (for example `Created`) as `onSuccess`.
- A failure gives `application/problem+json` with the error `code` as an extension:

| ErrorKind | Status |
|---|---|
| Validation | 400, `errors` grouped by field; a message without a field goes under the `""` key |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Failure | 422 |
| Unexpected | 500, message and code are not exposed |

`Error.Metadata` is never written to the response, and neither is the message of an unexpected error, also when it sits inside another error. `error.ToHttpResult()` and `kind.ToStatusCode()` are available for custom handling.

For an endpoint that declares its results, `error.ToProblem<TSuccess>()` gives the same answer typed as `Results<TSuccess, ValidationProblem, ProblemHttpResult>`:

```csharp
app.MapGet("/users/{id}", async Task<Results<Ok<UserDto>, ValidationProblem, ProblemHttpResult>> (
    Guid id,
    UserService users) =>
{
    var user = await users.GetUser(id);
    return user.IsSuccess
        ? TypedResults.Ok(new UserDto(user.Value.Id, user.Value.Name))
        : user.Error.ToProblem<Ok<UserDto>>();
});
```

`ErrorHttpExtensions.ExpectedKinds` lists the kinds that answer with their own status, `error.IsExpected()` says whether an error answers that way or as a bare 500, and `ProblemMembers` names the body members `code`, `errors` and `traceId`.

## License

MIT
