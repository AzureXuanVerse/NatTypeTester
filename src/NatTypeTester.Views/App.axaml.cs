namespace NatTypeTester.Views;

public class App : Avalonia.Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		IServiceProvider serviceProvider = AppLocator.Current.GetRequiredService<IServiceProvider>();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
			ScheduleStartupTasks(serviceProvider);
		}
		else if (ApplicationLifetime is IActivityApplicationLifetime activity)
		{
			activity.MainViewFactory = () =>
			{
				Control mainView = TopLevelHelper.RegisterActivityMainView(serviceProvider.GetRequiredService<MainView>());
				ScheduleStartupTasks(serviceProvider);
				return mainView;
			};
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = serviceProvider.GetRequiredService<MainView>();
			ScheduleStartupTasks(serviceProvider);
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static void ScheduleStartupTasks(IServiceProvider serviceProvider)
	{
		MainWindowViewModel mainViewModel = serviceProvider.GetRequiredService<MainWindowViewModel>();
		Dispatcher.UIThread.Post(mainViewModel.RunStartupTasks, DispatcherPriority.Loaded);
	}
}
