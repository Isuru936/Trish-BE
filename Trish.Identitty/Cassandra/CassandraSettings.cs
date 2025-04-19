namespace Trish.Identitty.Cassandra
{
    public class CassandraSettings
    {
        public string[] ContactPoints { get; set; } = default!;
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
