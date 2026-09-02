namespace OpenAdoration.WPF.Services;

/// <summary>Lets a page-scoped ViewModel navigate to the Stage View without depending on MainViewModel directly.</summary>
public interface IStageNavigationService
{
    void NavigateToStage();
}
