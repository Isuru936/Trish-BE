using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Organization.Command;
using Trish.Application.Shared;
using DomainEntities = Trish.Domain.Entities;

namespace Trish.Application.Features.Organization.Handler
{
    internal class CreateOrganizationCommandHandler : ICommandHandler<CreateOrganizationCommand>
    {
        private readonly Abstractions.Persistence.IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<DomainEntities.Organization> repository;

        public CreateOrganizationCommandHandler(IUnitOfWork unitOfWork, IGenericRepository<DomainEntities.Organization> repository)
        {
            _unitOfWork = unitOfWork;
            this.repository = repository;
        }

        public async Task<Result> Handle(CreateOrganizationCommand command, CancellationToken cancellationToken)
        {
            var organization = DomainEntities.Organization.Create(command.OrganizationName, command.Description, command.ImageUrl);
            await repository.AddAsync(organization);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
