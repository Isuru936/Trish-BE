using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Organization.Command;
using Trish.Application.Shared;
using Trish.Domain.Entities;

namespace Trish.Application.Features.Organization.Handler
{
    public class AssignToOrganizationCommandHandler : ICommandHandler<AssignUserToOrganizationCommand>
    {
        private readonly IGenericRepository<UserOrganization> genericRepository;
        private readonly IUnitOfWork unitOfWork;

        public AssignToOrganizationCommandHandler(IGenericRepository<UserOrganization> genericRepository, IUnitOfWork unitOfWork)
        {
            this.genericRepository = genericRepository;
            this.unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(AssignUserToOrganizationCommand command, CancellationToken cancellationToken)
        {

            var userOrganization = new UserOrganization
            {
                OrganizationId = command.OrganizationId,
                UserId = command.UserId
            };

            await genericRepository.AddAsync(userOrganization);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
