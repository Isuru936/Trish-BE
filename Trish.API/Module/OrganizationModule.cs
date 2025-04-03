using MediatR;
using Microsoft.AspNetCore.Mvc;
using Trish.API.Interfaces;
using Trish.Application.Features.Organization.Command;
using Trish.Application.Features.Organization.Query;

namespace Trish.API.Module
{
    public class OrganizationModule : IApiModule
    {
        public void MapEndpoint(WebApplication app)
        {
            var MapGroup = app.MapGroup("Organization")
                .WithTags("organization");

            MapGroup.MapPost(
                "/",
                async (CreateOrganizationCommand command, [FromServices] IMediator _mediator) =>
                {
                    return Results.Ok(await _mediator.Send(command));
                }
            );

            MapGroup.MapGet(
                "/getAll",
                async ([FromServices] IMediator _mediator) =>
                {
                    return Results.Ok(await _mediator.Send(new GetAllOrganizationsQuery()));
                });

            MapGroup.MapGet(
                "/{id}",
                async (Guid id, [FromServices] IMediator _mediator) =>
                {
                    return Results.Ok(await _mediator.Send(new GetOrganizationByIdQuery(id)));
                });

        }
    }
}
