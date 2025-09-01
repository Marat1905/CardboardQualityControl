using CardboardQualityControl.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CardboardQualityControl.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
            DataContext = _viewModel;
            Loaded += OnWindowLoaded;
            Closing += Window_Closing;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Освобождаем ресурсы при закрытии окна
            _viewModel?.Dispose();
        }

        private void OpenTrainingDialog_Click(object sender, RoutedEventArgs e)
        {
            var trainingDialog = _serviceProvider.GetService<TrainingDialog>();
            if (trainingDialog != null)
            {
                trainingDialog.Owner = this;
                trainingDialog.ShowDialog();
            }
        }

        private void ResetCounters_Click(object sender, RoutedEventArgs e)
        {
            // Сбрасываем счетчики
            _viewModel.DefectCount = 0;
            _viewModel.TotalFramesProcessed = 0;
            _viewModel.StatusMessage = "Counters reset";
        }
    }
}