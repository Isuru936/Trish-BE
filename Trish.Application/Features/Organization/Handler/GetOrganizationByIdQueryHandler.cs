using AutoMapper;
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Organization.Query;
using Trish.Application.Features.Organization.Response;
using Trish.Application.Shared;
using DomainEntities = Trish.Domain.Entities;

namespace Trish.Application.Features.Organization.Handler
{
    public class GetOrganizationByIdQueryHandler : IQueryHandler<GetOrganizationByIdQuery, Result<OrganizationResponse>>
    {
        private readonly IGenericRepository<DomainEntities.Organization> genericRepository;
        private readonly IMapper mapper;

        public GetOrganizationByIdQueryHandler(IGenericRepository<DomainEntities.Organization> genericRepository, IMapper mapper)
        {
            this.genericRepository = genericRepository;
            this.mapper = mapper;
        }

        public async Task<Result<OrganizationResponse>> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
        {
            var organization = await genericRepository.Get(request.id);

            return Result.Success(mapper.Map<OrganizationResponse>(organization));

        }
    }
}