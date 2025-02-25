/* 
 * using MediatR;
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Organization.Query;
using Trish.Application.Features.Organization.Response;
using Trish.Application.Shared;
using DomainEntities = Trish.Domain.Entities;

namespace Trish.Application.Features.Organization.Handler
{
    public class GetAllOrganizationsQueryHandler : IQueryHandler<GetAllOrganizationsQuery, Result<ICollection<OrganizationResponse>>>
    {
        private readonly IGenericRepository<DomainEntities.Organization> genericRepository;

        public GetAllOrganizationsQueryHandler(IGenericRepository<DomainEntities.Organization> genericRepository)
        {
            this.genericRepository = genericRepository;
        }

        async Task<Result<ICollection<OrganizationResponse>>> IQueryHandler<GetAllOrganizationsQuery, Result<ICollection<OrganizationResponse>>>.Handle(GetAllOrganizationsQuery query, CancellationToken cancellationToken)
        {
            List<DomainEntities.Organization> organizations = await genericRepository.GetAll();
            ICollection<OrganizationResponse> organizationResponses = organizations.Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name
            }).ToList();
            return Result<ICollection<OrganizationResponse>>.Success(organizationResponses);
        }

        async Task<Result<ICollection<OrganizationResponse>>> IRequestHandler<GetAllOrganizationsQuery, Result<ICollection<OrganizationResponse>>>.Handle(GetAllOrganizationsQuery request, CancellationToken cancellationToken)
        {
            List<DomainEntities.Organization> organizations = await genericRepository.GetAll();
            ICollection<OrganizationResponse> organizationResponses = organizations.Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name
            }).ToList();
            return Result<ICollection<OrganizationResponse>>.Success(organizationResponses);
        }
    }
}
*/