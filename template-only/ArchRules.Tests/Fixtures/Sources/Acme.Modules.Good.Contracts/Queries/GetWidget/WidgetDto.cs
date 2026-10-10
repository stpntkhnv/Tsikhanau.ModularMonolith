using Acme.SharedKernel;

namespace Acme.Modules.Good.Contracts.Queries.GetWidget;

public sealed record WidgetDto(Guid Id, String Label, Money Price);
