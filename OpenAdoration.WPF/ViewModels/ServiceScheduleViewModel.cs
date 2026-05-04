using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class ServiceScheduleViewModel : BaseViewModel
{
    private readonly IWorshipServiceService _worshipServiceService;
    private readonly IProjectionService _projectionService;
    private readonly ILogger<ServiceScheduleViewModel> _logger;

    public ServiceScheduleViewModel(
        IWorshipServiceService worshipServiceService,
        IProjectionService projectionService,
        ILogger<ServiceScheduleViewModel> logger)
    {
        _worshipServiceService = worshipServiceService;
        _projectionService     = projectionService;
        _logger                = logger;
    }
}
