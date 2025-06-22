using TestConnectorUI.ViewModels.Base;

namespace TestConnectorUI.Navigation;
public class NavigationStore(Func<Type, BaseViewModel> getViewModel)
{

    public event Action? CurrentViewModelChanged;

    public BaseViewModel? CurrentViewModel { get; private set; }

    public BaseViewModel GetViewModel(Type vmType)
    {
        return getViewModel.Invoke(vmType);
    }

    public BaseViewModel GetViewModel<TViewModel>() where TViewModel : class
    {
        return getViewModel.Invoke(typeof(TViewModel));
    }

    public BaseViewModel GetViewModel<TViewModel>(Action<TViewModel> parametrizeViewModel) where TViewModel : class
    {
        BaseViewModel viewModel = getViewModel.Invoke(typeof(TViewModel));
        parametrizeViewModel((viewModel as TViewModel)!);

        return viewModel;
    }

    public void SetViewModel<TViewModel>(Action<TViewModel> parametrizeViewModel) where TViewModel : class
    {
        BaseViewModel viewModel = getViewModel.Invoke(typeof(TViewModel));
        parametrizeViewModel((viewModel as TViewModel)!);
        CurrentViewModel = viewModel;
        OnCurrentViewModelChanged();
    }

    public void SetViewModel<TViewModel>()
    {
        BaseViewModel viewModel = getViewModel.Invoke(typeof(TViewModel));
        CurrentViewModel = viewModel;
        OnCurrentViewModelChanged();
    }

    private void OnCurrentViewModelChanged()
    {
        CurrentViewModelChanged?.Invoke();
    }
}