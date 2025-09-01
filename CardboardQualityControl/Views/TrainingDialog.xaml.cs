using CardboardQualityControl.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CardboardQualityControl.Views
{
    /// <summary>
    /// Логика взаимодействия для TrainingDialog.xaml
    /// </summary>
    public partial class TrainingDialog : Window
    {
        public TrainingDialog(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            DataContext = serviceProvider.GetRequiredService<TrainingViewModel>();
        }
    }
}
