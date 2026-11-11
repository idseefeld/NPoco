using System;
using System.Collections.Generic;
using System.Text;

namespace NPoco.Expressions
{
    public class OracleExpression<T> : SqlExpression<T>
    {
        public OracleExpression(IDatabase database, PocoData pocoData, bool prefixTableName) : base(database, pocoData, prefixTableName)
        {
        }

        protected override string GetDateTimeSql(string memberName, object m)
        {
            // TIMESTAMP '...' is only valid for string literals. CAST rather than extracting directly,
            // as EXTRACT treats a DATE column as ANSI DATE, which has no HOUR, MINUTE or SECOND
            string sql;
            switch (memberName)
            {
                case "Year": sql = $"EXTRACT(YEAR FROM CAST({m} AS TIMESTAMP))"; break;
                case "Month": sql = $"EXTRACT(MONTH FROM CAST({m} AS TIMESTAMP))"; break;
                case "Day": sql = $"EXTRACT(DAY FROM CAST({m} AS TIMESTAMP))"; break;
                case "Hour": sql = $"EXTRACT(HOUR FROM CAST({m} AS TIMESTAMP))"; break;
                case "Minute": sql = $"EXTRACT(MINUTE FROM CAST({m} AS TIMESTAMP))"; break;
                case "Second": sql = $"EXTRACT(SECOND FROM CAST({m} AS TIMESTAMP))"; break;
                default: throw new NotSupportedException("Not Supported " + memberName);
            }
            return sql;
        }
    }
}
