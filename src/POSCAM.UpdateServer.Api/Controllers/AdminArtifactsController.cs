using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin/releases/{releaseCode:long}/artifacts")]
public sealed class AdminArtifactsController : ControllerBase
{
    private readonly IArtifactUploadService _artifactUploadService;

    public AdminArtifactsController(IArtifactUploadService artifactUploadService)
    {
        _artifactUploadService = artifactUploadService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiResponse<ArtifactUploadResponse>), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ApiResponse<ArtifactUploadResponse>>> UploadAsync(
        long releaseCode,
        [FromForm] ArtifactUploadRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _artifactUploadService.UploadAsync(
            releaseCode,
            request,
            cancellationToken);

        var response = result.Success && result.Data is not null
            ? ApiResponse<ArtifactUploadResponse>.Ok(result.Data, result.Message)
            : ApiResponse<ArtifactUploadResponse>.Fail(result.ErrorCode, result.Message);

        return StatusCode(result.HttpStatusCode, response);
    }
}
