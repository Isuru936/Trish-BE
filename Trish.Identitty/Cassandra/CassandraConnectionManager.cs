using Cassandra;
using Microsoft.Extensions.Options;
using Trish.Application.Abstractions.Cassandra;

namespace Trish.Identitty.Cassandra
{
    public class CassandraConnectionManager : ICassandraConnectionManager, IDisposable
    {
        private readonly ICluster _cluster;
        private readonly ISession _session;

        public CassandraConnectionManager(IOptions<CassandraSettings> settings)
        {
            Console.WriteLine("CONNECTING TP +O");
            var builder = Cluster.Builder()
                .AddContactPoints(settings.Value.ContactPoints)
                .WithPort(settings.Value.Port);

            if (!string.IsNullOrEmpty(settings.Value.Username))
            {
                builder.WithCredentials(settings.Value.Username, settings.Value.Password);
            }

            _cluster = builder.Build();
            _session = _cluster.Connect();
        }

        public ISession GetSession() => _session;

        public IEnumerable<string> GetKeyspaces()
        {
            return _session
                .Execute("SELECT * FROM system_schema.keyspaces")
                .Select(row => row.GetValue<string>("keyspace_name"));
        }

        public IEnumerable<string> GetTablesForKeyspace(string keyspaceName)
        {
            return _session
                .Execute($"SELECT table_name FROM system_schema.tables WHERE keyspace_name = '{keyspaceName}'")
                .Select(row => row.GetValue<string>("table_name"));
        }

        public void Dispose()
        {
            _session?.Dispose();
            _cluster?.Dispose();
        }
    }
}
