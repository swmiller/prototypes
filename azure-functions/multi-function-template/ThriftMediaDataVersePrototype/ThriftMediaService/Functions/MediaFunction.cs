using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ThriftMediaService.Models;
using ThriftMediaService.Services;

namespace ThriftMediaService.Functions;

public class MediaFunction
{
    private readonly ILogger<MediaFunction> _logger;
    private readonly IMediaService _mediaService;

    public MediaFunction(ILogger<MediaFunction> logger, IMediaService mediaService)
    {
        _logger = logger;
        _mediaService = mediaService;
    }

    [Function("GetMedia")]
    public async Task<IActionResult> GetMedia(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "media")] HttpRequest req)
    {
        _logger.LogInformation("Getting all media");
        
        var storeId = req.Query["storeId"].ToString();
        
        var media = string.IsNullOrEmpty(storeId)
            ? await _mediaService.GetAllMediaAsync()
            : await _mediaService.GetMediaByStoreIdAsync(storeId);
        
        return new OkObjectResult(media);
    }

    [Function("GetMediaById")]
    public async Task<IActionResult> GetMediaById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "media/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Getting media with ID: {MediaId}", id);
        
        var media = await _mediaService.GetMediaByIdAsync(id);
        
        if (media == null)
        {
            return new NotFoundObjectResult(new { message = $"Media with ID {id} not found" });
        }
        
        return new OkObjectResult(media);
    }

    [Function("CreateMedia")]
    public async Task<IActionResult> CreateMedia(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "media")] HttpRequest req)
    {
        _logger.LogInformation("Creating new media");
        
        var media = await req.ReadFromJsonAsync<Media>();
        
        if (media == null)
        {
            return new BadRequestObjectResult(new { message = "Invalid media data" });
        }
        
        var createdMedia = await _mediaService.CreateMediaAsync(media);
        
        return new CreatedResult($"/api/media/{createdMedia.Id}", createdMedia);
    }

    [Function("UpdateMedia")]
    public async Task<IActionResult> UpdateMedia(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "media/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Updating media with ID: {MediaId}", id);
        
        var media = await req.ReadFromJsonAsync<Media>();
        
        if (media == null)
        {
            return new BadRequestObjectResult(new { message = "Invalid media data" });
        }
        
        var updatedMedia = await _mediaService.UpdateMediaAsync(id, media);
        
        if (updatedMedia == null)
        {
            return new NotFoundObjectResult(new { message = $"Media with ID {id} not found" });
        }
        
        return new OkObjectResult(updatedMedia);
    }

    [Function("DeleteMedia")]
    public async Task<IActionResult> DeleteMedia(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "media/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Deleting media with ID: {MediaId}", id);
        
        var result = await _mediaService.DeleteMediaAsync(id);
        
        if (!result)
        {
            return new NotFoundObjectResult(new { message = $"Media with ID {id} not found" });
        }
        
        return new NoContentResult();
    }
}
