using MediatR;

namespace Trish.Application.Abstractions.Messaging
{
    public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        //Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
    }
}
