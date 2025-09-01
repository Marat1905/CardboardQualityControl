using CardboardQualityControl.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CardboardQualityControl.Views
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        }

        private void OpenTrainingDialog_Click(object sender, RoutedEventArgs e)
        {
            var trainingDialog = _serviceProvider.GetRequiredService<TrainingDialog>();
            trainingDialog.Owner = this;
            trainingDialog.ShowDialog();
        }
    }
}