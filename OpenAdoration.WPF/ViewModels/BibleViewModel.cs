using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class BibleViewModel : BaseViewModel
{
    private readonly IBibleService _bibleService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<BibleViewModel> _logger;

    public BibleViewModel(
        IBibleService bibleService,
        IProjectionService projectionService,
        ILogger<BibleViewModel> logger)
    {
        _bibleService      = bibleService;
        _projectionService = projectionService;
        _logger            = logger;
    }
}
