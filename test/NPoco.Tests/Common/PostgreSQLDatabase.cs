using System;
using Npgsql;
using NPoco.DatabaseTypes;

namespace NPoco.Tests.Common
{
    /// <summary>
    /// The tests' raw SQL uses unquoted mixed-case identifiers (FROM Users), which Postgres folds to
    /// lower case, while NPoco quotes the names it generates ("Users"). Quoting the lower-cased name
    /// lets both refer to the same unquoted, lower-case tables and columns the schema creates.
    /// </summary>
    public class TestPostgreSQLDatabaseType : PostgreSQLDatabaseType
    {
        public override string EscapeSqlIdentifier(string str)
        {
            return base.EscapeSqlIdentifier(str.ToLowerInvariant());
        }
    }

    public class PostgreSQLDatabase : TestDatabase
    {
        private readonly string DBName = "npoco_test_" + Guid.NewGuid().ToString("N");
        private readonly string _serverConnectionString;

        public override System.Data.Common.DbProviderFactory Factory => NpgsqlFactory.Instance;

        public PostgreSQLDatabase(string connectionString)
        {
            DbType = new TestPostgreSQLDatabaseType();
            _serverConnectionString = connectionString;
            ConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = DBName,
                Pooling = false
            }.ToString();
            ProviderName = "Npgsql";

            RecreateDataBase();
            EnsureSharedConnectionConfigured();
        }

        public override void EnsureSharedConnectionConfigured()
        {
            if (Connection != null) return;

            lock (_syncRoot)
            {
                Connection = new NpgsqlConnection(ConnectionString);
                Connection.Open();
            }
        }

        public override void RecreateDataBase()
        {
            base.RecreateDataBase();

            using (var conn = new NpgsqlConnection(_serverConnectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE {DBName}";
                cmd.ExecuteNonQuery();
            }

            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = Schema;
                cmd.ExecuteNonQuery();
            }
        }

        // Runs per test; the database itself lives for the whole fixture
        public override void CleanupDataBase()
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            DropDatabase();
        }

        private void DropDatabase()
        {
            using var conn = new NpgsqlConnection(_serverConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS {DBName} WITH (FORCE)";
            cmd.ExecuteNonQuery();
        }

        private const string Schema = @"
CREATE TABLE Users(
    UserId serial PRIMARY KEY NOT NULL,
    Name varchar(200) NULL,
    Age int NULL,
    DateOfBirth timestamp NULL,
    Savings numeric(10,5) NULL,
    Is_Male boolean NULL,
    UniqueId uuid NULL,
    TimeSpan interval NULL,
    TestEnum varchar(10) NULL,
    HouseId int NULL,
    SupervisorId int NULL,
    Version bytea NULL,
    VersionInt int NOT NULL DEFAULT 0,
    YorN char(1) NULL,
    Address__Street varchar(50) NULL,
    Address__City varchar(50) NULL,
    StringObject varchar(50) NULL,
    YorNBoolean char(1) NOT NULL DEFAULT 'Y',
    Expires date NULL,
    AnsiString varchar(50) NULL
);

-- Stands in for SQL Server's rowversion
CREATE FUNCTION bump_version() RETURNS trigger AS $$
BEGIN
    NEW.Version := uuid_send(gen_random_uuid());
    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER users_version BEFORE INSERT OR UPDATE ON Users FOR EACH ROW EXECUTE FUNCTION bump_version();

CREATE TABLE ExtraUserInfos(
    ExtraUserInfoId serial PRIMARY KEY NOT NULL,
    UserId int NOT NULL,
    Email varchar(200) NULL,
    Children int NULL
);

CREATE TABLE Houses(
    HouseId serial PRIMARY KEY NOT NULL,
    Address varchar(200)
);

CREATE TABLE CompositeObjects(
    Key1_ID int PRIMARY KEY NOT NULL,
    Key2ID int NOT NULL,
    Key3ID int NOT NULL,
    TextData varchar(512) NULL,
    DateEntered timestamp NOT NULL,
    DateUpdated timestamp NULL
);

CREATE TABLE ComplexMap(
    Id serial PRIMARY KEY NOT NULL,
    Name varchar(50) NULL,
    NestedComplexMap__Id int NULL,
    NestedComplexMap__NestedComplexMap2__Id int NULL,
    NestedComplexMap__NestedComplexMap2__Name varchar(50) NULL,
    NestedComplexMap2__Id int NULL,
    NestedComplexMap2__Name varchar(50) NULL
);

CREATE TABLE RecursionUser(
    Id serial PRIMARY KEY NOT NULL,
    Name varchar(50) NULL,
    CreatedById int NULL,
    SupervisorId int NULL
);

CREATE TABLE Ones(
    OneId serial PRIMARY KEY NOT NULL,
    Name varchar(50) NULL
);

CREATE TABLE Manys(
    ManyId serial PRIMARY KEY NOT NULL,
    OneId int NOT NULL,
    Value int NULL,
    Currency varchar(50) NULL
);

CREATE TABLE UserWithAddress(
    Id serial PRIMARY KEY NOT NULL,
    Name varchar(100) NULL,
    Address text NULL
);

CREATE TABLE GuidFromDb(
    Id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    Name varchar(30)
);

CREATE TABLE JustPrimaryKey(
    Id serial PRIMARY KEY NOT NULL
);

CREATE TABLE NoPrimaryKey(
    Name varchar(50) NULL
);

CREATE TABLE Parent (ParentId int NOT NULL PRIMARY KEY, Id int NOT NULL);
CREATE TABLE Child (ChildId int NOT NULL PRIMARY KEY, ParentId int NOT NULL, CONSTRAINT fk FOREIGN KEY (ParentId) REFERENCES Parent(ParentId));

CREATE TABLE ParentWithComplexMapping (ParentId int NOT NULL PRIMARY KEY, Id int NOT NULL, Address__Street varchar(100) NULL, Address__City varchar(100) NULL);
CREATE TABLE ChildWithComplexParent (ChildId int NOT NULL PRIMARY KEY, ParentId int NOT NULL);
";
    }
}
