

using Cassandra;

namespace Trish.Application.Abstractions.Cassandra
{
    internal interface ICassandraConnectionManager
    {
        ISession GetSession();
        IEnumerable<string> GetKeyspaces();
        IEnumerable<string> GetTablesForKeyspace(string keyspaceName);
    }
}
