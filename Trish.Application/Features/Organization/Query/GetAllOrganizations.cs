using Trish.Application.Abstractions.Messaging;
using Trish.Application.Features.Organization.Response;
using Trish.Application.Shared;

namespace Trish.Application.Features.Organization.Query
{
    public class GetAllOrganizationsQuery : IQuery<Result<List<OrganizationResponse>>>
    {
    }
}


