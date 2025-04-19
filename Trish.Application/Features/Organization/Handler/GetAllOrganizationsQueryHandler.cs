using AutoMapper;
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Organization.Query;
using Trish.Application.Features.Organization.Response;
using Trish.Application.Shared;
using DomainEntities = Trish.Domain.Entities;

namespace Trish.Application.Features.Organization.Handler
{
    public class GetAllOrganizationsQueryHandler : IQueryHandler<GetAllOrganizationsQuery, Result<List<OrganizationResponse>>>
    {
        private readonly IGenericRepository<DomainEntities.Organization> genericRepository;
        private readonly IMapper mapper;

        public GetAllOrganizationsQueryHandler(IGenericRepository<DomainEntities.Organization> genericRepository, IMapper mapper)
        {
            this.genericRepository = genericRepository;
            this.mapper = mapper;
        }

        public async Task<Result<List<OrganizationResponse>>> Handle(GetAllOrganizationsQuery request, CancellationToken cancellationToken)
        {
            var organizations = await genericRepository.GetAllAsync();
            return Result.Success(mapper.Map<List<OrganizationResponse>>(organizations));
        }
    }
}
