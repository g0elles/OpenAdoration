using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class ThemeViewModel : BaseViewModel
{
    private readonly IThemeService _themeService;
    private readonly ILogger<ThemeViewModel> _logger;

    public ThemeViewModel(IThemeService themeService, ILogger<ThemeViewModel> logger)
    {
        _themeService = themeService;
        _logger       = logger;
    }
}
