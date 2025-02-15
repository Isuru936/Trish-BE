using MediatR;
using Trish.Application.Shared;

namespace Trish.Application.Abstractions.Messaging
{
    public interface ICommand : IRequest<Result> { }

    public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
}
