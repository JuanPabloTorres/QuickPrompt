using Microsoft.Data.Sqlite;
using QuickPrompt.Local;
using QuickPrompt.Nova.Application;
using QuickPrompt.Web.Infrastructure;
using Xunit;

namespace QuickPrompt.Web.Tests;

public sealed class StorageFailurePortTests
{
    [Fact] public void ClassificationUsesProviderTypesAndDoesNotElevateMessagesOrWrappedErrors()
    {
        IStorageFailureClassifier failures = new SqliteFailureClassifier();
        var sql = new SqliteException("PRIVATE_DATABASE_PATH", 19);
        Assert.True(failures.IsFailure(sql));
        foreach (var error in new Exception[] { new Exception("SqliteException PRIVATE_DATABASE_PATH"),
            new IOException("Storage error"), new ValidationException("SqliteException"),
            new ConflictException("Storage error"), new InvalidOperationException("wrapped", sql) })
            Assert.False(failures.IsFailure(error));
    }

    [Fact] public void RealEngineFailureIsRecognizedAndRollsBackTheVersionedWriteBeforeRecovery()
    {
        var directory = Path.Combine(Path.GetTempPath(), "storage-failure-port-" + Guid.NewGuid());
        try {
            var path = Path.Combine(directory, "synthetic.sqlite"); var module = new LocalPromptLibrary(path);
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "CREATE TRIGGER qa_reject_version BEFORE INSERT ON Documents WHEN NEW.Kind='version' BEGIN SELECT RAISE(ABORT, 'PRIVATE_DATABASE_PATH'); END;";
            command.ExecuteNonQuery();
            var value = new Template { Title = "QA rollback", Template = "Explica <tema>." };
            var error = Assert.Throws<SqliteException>(() => module.Authoring.Save(value));
            Assert.True(module.StorageFailures.IsFailure(error));
            Assert.Equal(0, module.Templates.ActiveCount); Assert.Empty(module.Templates.Versions(value.Id));
            command.CommandText = "DROP TRIGGER qa_reject_version;"; command.ExecuteNonQuery();
            var saved = module.Authoring.Save(value);
            Assert.Equal(1, saved.Revision); Assert.Equal(1, module.Templates.ActiveCount);
            Assert.Single(module.Templates.Versions(saved.Id));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
