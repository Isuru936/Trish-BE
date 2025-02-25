using MediatR;
using Microsoft.AspNetCore.Mvc;
using Trish.API.Interfaces;
using Trish.Application.Features.Organization.Command;

namespace Trish.API.Module
{
    public class OrganizationModule : IApiModule
    {
        public void MapEndpoint(WebApplication app)
        {
            var MapGroup = app.MapGroup("Organization")
                .WithTags("organization");

            MapGroup.MapPost(
                "/organization",
                async (CreateOrganizationCommand command, [FromServices] IMediator _mediator) =>
                {
                    return Results.Ok(await _mediator.Send(command));
                }
            );

            /* MapGroup.MapGet(
                "/getAll",
                async (GetAllOrganizationsQuery query, [FromServicesAttribute] IMediator _mediator) =>
                {
                    return Results.Ok(await _mediator.Send(query));
                }); */

        }
    }
}
