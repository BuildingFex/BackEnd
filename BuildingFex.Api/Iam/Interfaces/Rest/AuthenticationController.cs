using BuildingFex.Api.Iam.Application.CommandServices;
using BuildingFex.Api.Iam.Domain.Model;
using BuildingFex.Api.Iam.Domain.Model.Commands;
using BuildingFex.Api.Iam.Interfaces.Rest.Resources;
using BuildingFex.Api.Iam.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Mvc;

namespace BuildingFex.Api.Iam.Interfaces.Rest;

/// <summary>
/// API versionada (estilo learning-center). Uso recomendado a largo plazo.
/// </summary>
[ApiController]
[Route("api/v1/authentication")]
public class AuthenticationController(IUserCommandService userCommandService) : ControllerBase
{
    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInResource resource, CancellationToken ct)
    {
        var result = await userCommandService.Handle(new SignInCommand(resource.Email, resource.Password), ct);
        if (result.IsFailure)
            return BadRequest(new { code = MapErrorCode(result.Error), message = result.Message });

        return Ok(new AuthenticatedUserResource(
            UserResourceAssembler.ToResource(result.Value!.user),
            result.Value.token));
    }

    [HttpPost("register-admin")]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminResource resource, CancellationToken ct)
    {
        var result = await userCommandService.Handle(
            new RegisterAdminCommand(
                resource.Name,
                resource.Email,
                resource.Password,
                resource.Dni,
                resource.Address,
                resource.Company,
                resource.Ruc),
            ct);

        if (result.IsFailure)
            return BadRequest(new { code = MapErrorCode(result.Error), message = result.Message });

        return Ok(new AuthenticatedUserResource(
            UserResourceAssembler.ToResource(result.Value!.user),
            result.Value.token));
    }

    private static string MapErrorCode(Enum? error) =>
        error switch
        {
            IamError.EmailNotFound => "EMAIL_NOT_FOUND",
            IamError.InvalidPassword => "INVALID_PASSWORD",
            IamError.EmailAlreadyExists => "EMAIL_ALREADY_EXISTS",
            IamError.InvalidCredentials => "INVALID_PASSWORD",
            _ => "AUTH_ERROR",
        };
}
