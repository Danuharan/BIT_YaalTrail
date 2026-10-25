using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace YaalTrail.Api.Data;

// Development only: counts SQL commands sent to the database during one HTTP request,
// so round trips to the remote database can be checked (NFR-02).
public class SqlQueryCounter
{
    public int Count { get; private set; }

    public void Increment() => Count++;
}

public class SqlQueryCountingInterceptor : DbCommandInterceptor
{
    private readonly SqlQueryCounter _counter;

    public SqlQueryCountingInterceptor(SqlQueryCounter counter)
    {
        _counter = counter;
    }

    // Called once for every command EF Core is about to execute.
    public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
    {
        _counter.Increment();
        return result;
    }
}
