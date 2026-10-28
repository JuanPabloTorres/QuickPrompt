using Microsoft.Data.Sqlite;
using QuickPrompt.Nova.Application;

namespace QuickPrompt.Web.Infrastructure;

public sealed class SqliteFailureClassifier : IStorageFailureClassifier
{
    public bool IsFailure(Exception error) => error is SqliteException;
}
