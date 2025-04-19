using MediatR;

namespace Trish.Application.Abstractions.Messaging
{
    public interface IQuery<TResponse> : IRequest<TResponse> { }
}
