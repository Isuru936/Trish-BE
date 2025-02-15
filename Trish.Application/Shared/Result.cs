namespace Trish.Application.Shared
{
    public class Result
    {
        protected Result(bool isSuccess, Shared.Error error)
        {
            if (isSuccess && error != Shared.Error.None ||
                !isSuccess && error == Shared.Error.None)
            {
                throw new ArgumentException("Invalid Error", nameof(error));
            }
            IsSuccess = isSuccess;
            Error = error;
        }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }
        public static Result Success() => new(true, Shared.Error.None);
        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
        public static Result Failure(Error error) => new(false, error);
        public static Result<TValue> Failure<TValue>(TValue? value, Error error) => new(value, false, error);
    }
}
