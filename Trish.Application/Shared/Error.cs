namespace Trish.Application.Shared
{
    public sealed record Error(string code, string? description = null)
    {
        public static readonly Error None = new(string.Empty);
    }
}
