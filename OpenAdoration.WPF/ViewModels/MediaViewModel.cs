using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class MediaViewModel : BaseViewModel
{
    private readonly IMediaService _mediaService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<MediaViewModel> _logger;

    public MediaViewModel(
        IMediaService mediaService,
        IProjectionService projectionService,
        ILogger<MediaViewModel> logger)
    {
        _mediaService      = mediaService;
        _projectionService = projectionService;
        _logger            = logger;
    }
}
