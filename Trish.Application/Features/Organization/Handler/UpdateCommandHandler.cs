using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Shared;
using DomainEntities = Trish.Domain.Entities;


namespace Trish.Application.Features.Organization.Handler
{
    public class UpdateCommandHandler : ICommandHandler<UpdateCommandHandler>
    {
        private readonly Abstractions.Persistence.IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<DomainEntities.Organization> repository;

        public UpdateCommandHandler(IUnitOfWork unitOfWork, IGenericRepository<DomainEntities.Organization> repository)
        {
            _unitOfWork = unitOfWork;
            this.repository = repository;
        }

        public Task<Result> Handle(UpdateCommandHandler command, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
