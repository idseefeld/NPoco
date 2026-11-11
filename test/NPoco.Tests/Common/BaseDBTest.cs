using NPoco;

namespace NPoco.Tests.Common
{
    [TestDescriptor]
    public class BaseDBTest
    {
        public IDatabase Database { get; set; }
        public TestDatabase TestDatabase { get; set; }

        protected bool IsPostgres => TestDatabase is PostgreSQLDatabase;

        protected void IgnoreOnPostgres(string reason)
        {
            if (IsPostgres) NUnit.Framework.Assert.Ignore(reason);
        }
    }
}
