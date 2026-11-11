using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NPoco.Tests.Common;
using NUnit.Framework;

namespace NPoco.Tests
{
    [TestFixture]
    public class LazyTransactionTests
    {
        // Runs against Postgres when TestDBType is 7, otherwise against a SQLite file
        private PostgreSQLDatabase _postgres;
        private string _path;
        private string _connectionString;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (TestConfig.TestDBType != 7) return;

            _postgres = new PostgreSQLDatabase(TestConfig.PostgresConnectionString);
            _connectionString = _postgres.ConnectionString;
            using var db = NewDatabase();
            db.Execute("CREATE TABLE items (id serial PRIMARY KEY, name text)");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _postgres?.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            if (_postgres != null)
            {
                using var pg = NewDatabase();
                pg.Execute("TRUNCATE items");
                return;
            }

            _path = Path.Combine(Path.GetTempPath(), "npoco-lazy-" + Guid.NewGuid().ToString("N") + ".db");
            _connectionString = "Data Source=" + _path + ";Pooling=False";
            using var db = NewDatabase();
            db.Execute("CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT)");
        }

        [TearDown]
        public void TearDown()
        {
            if (_path != null && File.Exists(_path)) File.Delete(_path);
        }

        private Database NewDatabase() => _postgres != null
            ? new Database(_connectionString, DatabaseType.PostgreSQL, _postgres.Factory)
            : new Database(_connectionString, DatabaseType.SQLite, SqliteFactory.Instance);

        private long Count()
        {
            using var db = NewDatabase();
            return db.ExecuteScalar<long>("SELECT COUNT(*) FROM items");
        }

        [Test]
        public void LazyTransactionDoesNotOpenConnectionUntilFirstCommand()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using var tran = db.GetTransaction();
                Assert.IsNull(db.Connection);
                Assert.IsNull(db.Transaction);

                db.Execute("INSERT INTO items (name) VALUES ('a')");
                Assert.IsNotNull(db.Connection);
                Assert.IsNotNull(db.Transaction);

                tran.Complete();
            }

            Assert.AreEqual(1, Count());
        }

        [Test]
        public void LazyTransactionWithNoCommandsNeverOpensConnection()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using (var tran = db.GetTransaction())
                {
                    tran.Complete();
                }

                using (db.GetTransaction())
                {
                    // aborted
                }

                Assert.IsNull(db.Connection);
            }
        }

        [Test]
        public void LazyTransactionRollsBackWhenNotCompleted()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using (db.GetTransaction())
                {
                    db.Execute("INSERT INTO items (name) VALUES ('a')");
                }
            }

            Assert.AreEqual(0, Count());
        }

        [Test]
        public void NestedLazyTransactionAbortAbortsWhole()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using (var outer = db.GetTransaction())
                {
                    using (db.GetTransaction())
                    {
                        db.Execute("INSERT INTO items (name) VALUES ('a')");
                    }

                    outer.Complete();
                }
            }

            Assert.AreEqual(0, Count());
        }

        [Test]
        public void NestedLazyTransactionAbortedBeforeAnyCommandLeavesNoTransaction()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using (var outer = db.GetTransaction())
                {
                    using (db.GetTransaction())
                    {
                    }

                    outer.Complete();
                }

                db.Execute("INSERT INTO items (name) VALUES ('a')");
                Assert.IsNull(db.Transaction);
            }

            Assert.AreEqual(1, Count());
        }

        [Test]
        public async Task LazyTransactionAsync()
        {
            using var db = NewDatabase();
            await using (await db.OpenSharedConnectionAsync(new OpenConnectionOptions { Lazy = true }))
            {
                await using var tran = await db.GetTransactionAsync();
                Assert.IsNull(db.Connection);

                await db.ExecuteAsync("INSERT INTO items (name) VALUES ('a')");
                Assert.IsNotNull(db.Transaction);

                await tran.CompleteAsync();
            }

            Assert.AreEqual(1, Count());
        }

        [Test]
        public async Task LazyTransactionAsyncRollsBackWhenNotCompleted()
        {
            using var db = NewDatabase();
            await using (await db.OpenSharedConnectionAsync(new OpenConnectionOptions { Lazy = true }))
            {
                await using (await db.GetTransactionAsync())
                {
                    await db.ExecuteAsync("INSERT INTO items (name) VALUES ('a')");
                }
            }

            Assert.AreEqual(0, Count());
        }

        [Test]
        public void LazyTransactionStartedOnOpenConnectionBeginsImmediately()
        {
            using var db = NewDatabase();
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                db.ExecuteScalar<long>("SELECT 1");

                using var tran = db.GetTransaction();
                Assert.IsNotNull(db.Transaction);
                tran.Complete();
            }
        }

        [Test]
        public void NonLazyTransactionBeginsImmediately()
        {
            using var db = NewDatabase();
            using var tran = db.GetTransaction();
            Assert.IsNotNull(db.Connection);
            Assert.IsNotNull(db.Transaction);
            tran.Complete();
        }

        [Test]
        public void BeginInterceptorFiresAtFirstCommand()
        {
            var interceptor = new CountingInterceptor();
            using var db = NewDatabase();
            db.Interceptors.Add(interceptor);
            using (db.OpenSharedConnection(new OpenConnectionOptions { Lazy = true }))
            {
                using var tran = db.GetTransaction();
                Assert.AreEqual(0, interceptor.Begins);

                db.Execute("INSERT INTO items (name) VALUES ('a')");
                db.Execute("INSERT INTO items (name) VALUES ('b')");
                Assert.AreEqual(1, interceptor.Begins);

                tran.Complete();
                Assert.AreEqual(1, interceptor.Completes);
            }
        }

        private class CountingInterceptor : ITransactionInterceptor
        {
            public int Begins, Aborts, Completes;
            public void OnBeginTransaction(IDatabase database) => Begins++;
            public void OnAbortTransaction(IDatabase database) => Aborts++;
            public void OnCompleteTransaction(IDatabase database) => Completes++;
        }
    }
}
