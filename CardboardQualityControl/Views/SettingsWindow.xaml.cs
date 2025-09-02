using CardboardQualityControl.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace CardboardQualityControl.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void BrowseVideoFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.flv;*.webm|All Files|*.*",
                Title = "Select Video File",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Config.FileVideoSettings.Path = openFileDialog.FileName;
            }
        }

        private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
        {
            // Используем OpenFileDialog для выбора папки (хитрость)
            var dialog = new OpenFileDialog
            {
                Title = "Select Output Folder",
                FileName = "SelectFolder", // Любое имя файла
                Filter = "Folders|\n", // Фильтр для папок
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _viewModel.Config.VideoRecordingSettings.OutputPath = selectedPath;
                }
            }
        }

        private void BrowseModelPath_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "ML Model Files|*.zip;*.onnx;*.mlmodel|All Files|*.*",
                Title = "Select Machine Learning Model",
                InitialDirectory = Environment.CurrentDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.Config.ModelSettings.ModelPath = openFileDialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SaveSettings())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to save settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveSettings();
        }
    }
}