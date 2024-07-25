using System;
using System.Windows;
using Microsoft.CognitiveServices.Speech;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.Extensions.Configuration;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using System.Timers;

namespace SpeechToText
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SpeechRecognizer recognizer;
        private bool isRecognizing = false;
        private string noteFilePath;
        private System.Timers.Timer microphoneCheckTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLanguageComboBox();
            microphoneCheckTimer = new System.Timers.Timer(1000); // Check every second
            microphoneCheckTimer.Elapsed += MicrophoneCheckTimer_Elapsed;
            microphoneCheckTimer.Start();
        }

        private string GetSubscriptionKey()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .Build();
            var strSub = config["Speech:SubscriptionKey"];
            return strSub;
        }

        private string GetRegionKey()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .Build();
            var strReg = config["Speech:Region"];
            return strReg;
        }

        private void InitializeSpeechRecognition(string languageCode)
        {
            string subKey = GetSubscriptionKey();
            string regKey = GetRegionKey();
            var config = SpeechConfig.FromSubscription(subKey, regKey);
            config.SpeechRecognitionLanguage = languageCode; // Set language code here
            recognizer = new SpeechRecognizer(config);
            recognizer.Recognized += Recognizer_Recognized;
        }
        private void MicrophoneCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isRecognizing && !IsMicrophonePluggedIn())
            {
                Dispatcher.Invoke(() => StopSpeechRecognitionAsync());
                MessageBox.Show("Your Microphone is disconnected !", "Error");
            }
        }
        public bool IsMicrophonePluggedIn()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            return devices.Any();
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecognizing)
            {
                var micCheck = IsMicrophonePluggedIn();
                if (!micCheck)
                {
                    MessageBox.Show("Microphone not found!", "Error");
                }
                else
                {
                    Language selectedLanguage = languageComboBox.SelectedItem as Language;
                    if (selectedLanguage != null)
                    {
                        string languageCode = selectedLanguage.Code;
                        InitializeSpeechRecognition(languageCode);
                        await StartSpeechRecognitionAsync();
                    }
                }
            }
            else
            {
                await StopSpeechRecognitionAsync();
            }
        }

        private async Task StartSpeechRecognitionAsync()
        {
            try
            {
                await recognizer.StartContinuousRecognitionAsync();
                startStopButton.Content = "Stop Recording";
                startStopButton.Background = Brushes.Red;
                isRecognizing = true;
                languageComboBox.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recognition: {ex.Message}");
            }
        }

        private async Task StopSpeechRecognitionAsync()
        {
            try
            {
                await recognizer.StopContinuousRecognitionAsync();
                startStopButton.Content = "Start Recording";
                startStopButton.Background = Brushes.LimeGreen;
                isRecognizing = false;
                languageComboBox.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recognition: {ex.Message}");
            }
        }

        private void Recognizer_Recognized(object sender, SpeechRecognitionEventArgs e)
        {
            var result = e.Result;
            string recognizedText = result.Text.ToLower();
            if (!recognizedText.Equals("update note.") &&
                !recognizedText.Equals("create note.") &&
                !recognizedText.Equals("clear note.") && !recognizedText.Equals("stop record."))
            {
                Dispatcher.Invoke(() => textBox.AppendText(result.Text + " "));
            }
            ControlByVoice(recognizedText);
        }

        private void ControlByVoice(string recognizedText)
        {
            if (recognizedText.Equals("create note."))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("Do you want to save?", "Confirmation", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    Dispatcher.Invoke(() => SaveFile());
                }
            }
            else if (recognizedText.Equals("clear note."))
            {
                Dispatcher.Invoke(() => textBox.Clear());
            }
            else if (recognizedText.Equals("update note."))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("Do you want to update?", "Confirmation", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    if (noteFilePath == null)
                    {
                        MessageBox.Show("Import file first!", "Error");
                    }
                    else
                    {
                        Dispatcher.Invoke(() => SaveNoteContent());
                        MessageBox.Show("Update Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else if (recognizedText.Equals("stop record."))
            {
                Dispatcher.Invoke(() => StopSpeechRecognitionAsync());
            }
        }

        private void InitializeLanguageComboBox()
        {
            var languages = new List<Language>
            {
                new Language("English", "en-US"),
                new Language("Vietnamese", "vi-VN"),
                new Language("Japanese","ja-JP")
            };

            foreach (var language in languages)
            {
                languageComboBox.Items.Add(language);
            }

            languageComboBox.SelectedIndex = 0;
        }

        public void SaveFile()
        {
            try
            {
                // Get the text from the TextBox
                string textToExport = textBox.Text;

                // Open a save dialog
                SaveFileDialog dialog = new SaveFileDialog()
                {
                    Filter = "Text Files(*.txt)|*.txt|All(*.*)|*"
                };
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, textToExport);
                    // Open Notepad with the created file
                    System.Diagnostics.Process.Start("notepad.exe", dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting text to Notepad: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void exportButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("Do you want to save?", "Confirmation", MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                Dispatcher.Invoke(() => SaveFile());
            }
        }

        private void openFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                noteFilePath = openFileDialog.FileName;
                LoadNoteContent();
            }
            textFilePath.Text = noteFilePath;
        }

        private void LoadNoteContent()
        {
            try
            {
                string noteContent = File.ReadAllText(noteFilePath);
                textBox.Text = noteContent;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading note content: {ex.Message}");
            }
        }

        private void SaveNoteContent()
        {
            try
            {
                File.WriteAllText(noteFilePath, textBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving note content: {ex.Message}");
            }
        }

        private void updateButton_Click(object sender, RoutedEventArgs e)
        {
            if (noteFilePath == null)
            {
                MessageBox.Show("Import file first!", "Error");
            }
            else {
                SaveNoteContent();
                MessageBox.Show("Update Successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            textBox.Clear();
        }

        private void btnTutorial_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("You want to write note by speech: \n + Update Note: You say 'Update note' \n + Clear Note: You say 'Clear note' \n + Create Note: You say 'Create note' \n + Stop Recording: You say 'Stop record'", "Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnRefreshPath_Click(object sender, RoutedEventArgs e)
        {
            noteFilePath = null;
            textFilePath.Clear();
        }
    }
}

