namespace Trish.Application.Exceptions
{
    public class ValdationException : Exception
    {
        public IReadOnlyCollection<ValidationError> _errors { get; set; }
        public ValdationException(IReadOnlyCollection<ValidationError> errors)
        {
            _errors = errors;
        }
    }

    public record ValidationError(string PropertyName, string ErrorMessage);

    public class BadRequestException : ApplicationException
    {
        public BadRequestException(string message) : base(message) { }

        public override string Title => "Bad Request";
    }

    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string message) : base(message) { }

        public override string Title => "Not Found";
    }

    public abstract class ApplicationException : Exception
    {
        protected ApplicationException(string message) : base(message) { }

        public virtual string Title => "Application Error";
    }
}
