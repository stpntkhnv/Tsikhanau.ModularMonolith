# Tsikhanau.Railway

Result and Optional types for railway-oriented programming in .NET: functional error handling without exceptions.

## Features

- **Result&lt;T&gt;** - success with a value or failure with an `Error`
- **Optional&lt;T&gt;** - a value or nothing
- **Error** - record with `Kind` (Failure, Unexpected, Validation, NotFound, Conflict, Unauthorized, Forbidden), `Code`, `Message`, `Inner` and `Metadata`; derive your own errors from it
- **ValidationError** - field errors, merged automatically by `CombineAll`
- **Bind**, **Map**, **MapError** - chain and transform
- **Ensure** - conditional checks with a fixed error or an error built from the value
- **Tap**, **TapError** - side effects
- **Result.Combine**, **Result.CombineAll**, **Result.FirstSuccess**, **Zip** - work with several results
- **Result.Collect** - collect up to eight results of different types and map or bind them together
- **Result.Try** - turn exceptions into errors
- **Match**, **GetValueOrDefault** - get the value out
- **Where**, **Or**, **OrElse** - filter and fallback for Optional
- **ToResult**, **ToOptional** - conversions between Result and Optional
- Async variants for all operations
- Roslyn analyzers for ignored results, Map instead of Bind, and unchecked Value/Error access
- ASP.NET Core integration (ProblemDetails, status codes by ErrorKind) in [Tsikhanau.Railway.AspNetCore](https://www.nuget.org/packages/Tsikhanau.Railway.AspNetCore)

## Installation

```bash
dotnet add package Tsikhanau.Railway
```

## Usage

```csharp
using Tsikhanau.Railway;

public sealed record UserBannedError(DateTime Until)
    : Error(ErrorKind.Forbidden, "user.banned", $"User is banned until {Until:yyyy-MM-dd}");

public async Task<Result<User>> GetUser(Guid id)
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Error.NotFound("user.not_found", "User not found");

    if (user.BannedUntil > DateTime.UtcNow)
        return new UserBannedError(user.BannedUntil.Value);

    return user;
}

var message = await GetUser(id)
    .EnsureAsync(u => u.IsActive, u => Error.Forbidden("user.inactive", $"{u.Name} is inactive"))
    .MapAsync(u => u.DisplayName)
    .TapAsync(name => logger.LogInformation("Retrieved: {Name}", name))
    .MatchAsync(
        onSuccess: name => $"Hello, {name}",
        onFailure: error => error switch
        {
            UserBannedError banned => $"Come back after {banned.Until:d}",
            _ => error.Message
        });

var validated = Result.CombineAll(
    ValidateEmail(request.Email),
    ValidateName(request.Name));
```

`Collect` gathers results of different types and hands every value to `Map` or `Bind` by name. If any result failed, the function is not called and the errors are merged the way `CombineAll` merges them.

```csharp
var food = Result
    .Collect(FoodName.Create(command.Name, "name"))
    .And(Kcal.Create(command.Kcal, "kcal"))
    .And(Grams.Create(command.Portion, "portion"))
    .Map((name, kcal, portion) => Food.Create(id, name, kcal, portion, now));
```

## Analyzers

The package ships Roslyn analyzers. All rules are warnings by default, so with `TreatWarningsAsErrors` they fail the build.

| Rule | What it finds |
|------|---------------|
| TR0001 | A `Result` (or a `Task` of one) that is not used, so its error is lost. Discard it with `_ =` when that is intended. `Tap`/`TapError` on a result stored in a variable is fine. |
| TR0002 | `Map`/`MapAsync` with a mapper that returns a `Result`, which produces `Result<Result<T>>`. Use `Bind`/`BindAsync`; a code fix does the replacement. |
| TR0003 | `Value` read without checking `IsSuccess`, or `Error` without checking `IsFailure`. Follows `if`, early `return`/`throw`, `?:`, `&&`/`\|\|`, patterns, loops and assertions marked with `[DoesNotReturnIf]` (`Debug.Assert`, xUnit `Assert.True`, Shouldly `ShouldBeTrue`). |

Severity is set per rule in `.editorconfig`, for example to turn TR0003 off in tests:

```ini
[tests/**.cs]
dotnet_diagnostic.TR0003.severity = none
```

## License

MIT
