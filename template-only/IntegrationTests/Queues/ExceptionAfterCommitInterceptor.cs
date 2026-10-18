using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tsikhanau.Monolith.IntegrationTests.Queues;

internal sealed class ExceptionAfterCommitInterceptor : DbTransactionInterceptor
{
    public Boolean ThrowsAfterNextCommit { get; set; }

    public Int32 ThrownCount { get; private set; }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!ThrowsAfterNextCommit)
        {
            return Task.CompletedTask;
        }

        ThrowsAfterNextCommit = false;
        ThrownCount++;
        throw new InvalidOperationException("Thrown after the commit landed");
    }
}
