using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class SongsViewModel : BaseViewModel
{
    private readonly ISongService _songService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<SongsViewModel> _logger;

    public SongsViewModel(
        ISongService songService,
        IProjectionService projectionService,
        ILogger<SongsViewModel> logger)
    {
        _songService       = songService;
        _projectionService = projectionService;
        _logger            = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Full implementation in the Songs feature session
    }
}
