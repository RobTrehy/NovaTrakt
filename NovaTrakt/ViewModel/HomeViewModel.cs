using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Microsoft.Maps.MapControl.WPF;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

// TODO introduce Novatekfile interface and switch implementation via factory via settings?

// TODO add some camera type switch...
//using Novatek;
using Mai70; // out placeholder hack..


using Novatek.Core;
using NovaTrakt.Classes;
using NovaTrakt.Classes.Helpers;
using NovaTrakt.Classes.NovaTrakt;
using NovaTrakt.SharedComponents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NovaTrakt.ViewModel
{
    class HomeViewModel : ObservableObject, IPageViewModel
    {
        #region Fields

        private static Log Log = new Log();

        private BackgroundWorker openWorker = new BackgroundWorker();
        private BackgroundWorker gpsWorker = new BackgroundWorker();
        private BackgroundWorker gpsWorker2 = new BackgroundWorker();
        private BackgroundWorker organisationWorker = new BackgroundWorker();

        private ProgressDialogController _progressDlg;

        private ICommand _settingsFlyout;

        private bool _about = false;
        private ICommand _aboutWindow;

        private string _currentPlayer = "ntMediaPlayer";
        private string _sparePlayer = "ntMediaPlayerB";
        private NovatekFile _spClip;

        private double _previousVol;
        private PackIconOcticonsKind _volumeIcon = PackIconOcticonsKind.Unmute;
        private PackIconFontAwesomeKind _playPauseIcon;
        private ICommand _playPause;
        private ICommand _nextClip;
        private ICommand _previousClip;
        private ICommand _changePlayerSpeed;
        private ICommand _getMediaScreen;
        private ICommand _nextMediaFrame;
        private ICommand _previousMediaFrame;
        private ICommand _muteAudio;

        private ICommand _exportJourney;
        private ICommand _exportClip;
        private ICommand _exportClips;

        private string _inputPath;
        private List<NovatekFile> _clips = new List<NovatekFile>();
        private ObservableCollection<Journey> _journeyList = new ObservableCollection<Journey>();
        private Journey _selectedTrip { get; set; }

        #endregion

        public HomeViewModel()
        {
            openWorker.DoWork += OpenWorker_DoWork;
            openWorker.RunWorkerCompleted += OpenWorker_RunWorkerCompleted;

            gpsWorker.DoWork += GpsWorker_DoWork;
            gpsWorker.RunWorkerCompleted += GpsWorker_RunWorkerCompleted;

            gpsWorker2.DoWork += GpsWorker2_DoWork;
            gpsWorker2.RunWorkerCompleted += GpsWorker2_RunWorkerCompleted;

            organisationWorker.DoWork += OrganisationWorker_DoWork;
            organisationWorker.RunWorkerCompleted += OrganisationWorker_RunWorkerCompleted;
        }

        #region Properties / Commands

        public string Name
        {
            get { return "Home"; }
        }

        public MediaElement CurrentPlayer
        {
            get
            {
                return UI.FindChild<MediaElement>(Application.Current.MainWindow, _currentPlayer);
            }
            set
            {
                _currentPlayer = value.Name;
                OnPropertyChanged("CurrentPlayer");
            }
        }

        public MediaElement SparePlayer
        {
            get
            {
                return UI.FindChild<MediaElement>(Application.Current.MainWindow, _sparePlayer);
            }
            set
            {
                _sparePlayer = value.Name;
                OnPropertyChanged("SparePlayer");
            }
        }

        public PackIconFontAwesomeKind PlayPauseIcon
        {
            get
            {
                if (_playPauseIcon != PackIconFontAwesomeKind.PlaySolid)
                    return _playPauseIcon;
                else
                    return PackIconFontAwesomeKind.PlaySolid;
            }
            set
            {
                _playPauseIcon = value;
                OnPropertyChanged("PlayPauseIcon");
            }
        }

        public PackIconOcticonsKind VolumeIcon
        {
            get { return _volumeIcon; }
            set
            {
                _volumeIcon = value;
                OnPropertyChanged("VolumeIcon");
            }
        }

        public ICommand SelectInputFolder
        {
            get { return new RelayCommand(c => _selectInputFolder()); }
        }

        public ICommand ClearFilesSource
        {
            get { return new RelayCommand(c => _clearFilesSource()); }
        }

        public IEnumerable<Journey> JourneyList
        {
            get { return _journeyList; }
        }

        public Journey SelectedTrip
        {
            get { return _selectedTrip; }
            set
            {
                if (_selectedTrip != value)
                {
                    _selectedTrip = value;
                    if (_selectedTrip != null)
                    {
                        _journeyList.Where(c => c.Selected == true).ToList().ForEach(a => a.Selected = false);
                        _journeyList.Where(c => c.StartTime == value.StartTime).ToList().ForEach(a => a.Selected = true);
                        if (!string.IsNullOrEmpty(_selectedTrip.CurrentClip.FullNameAndPath))
                            CurrentPlayer.Source = new Uri(_selectedTrip.CurrentClip.FullNameAndPath);
                    }
                    OnPropertyChanged("SelectedTrip");
                }
            }
        }

        public ICommand MuteAudio
        {
            get
            {
                if (_muteAudio == null)
                {
                    _muteAudio = new RelayCommand(
                        p => MutePlayerAudio());
                }
                return _muteAudio;
            }
        }

        public ICommand PlayPause
        {
            get
            {
                if (_playPause == null)
                {
                    _playPause = new RelayCommand(
                        p => PlayPauseMedia(),
                        p => SelectedTrip != null);
                }
                return _playPause;
            }
        }

        public ICommand NextClip
        {
            get
            {
                if (_nextClip == null)
                {
                    _nextClip = new RelayCommand(
                        p => LoadNextClip(),
                        p => SelectedTrip != null && SelectedTrip.CurrentClip.NextFile != null);
                }
                return _nextClip;
            }
        }

        public ICommand PreviousClip
        {
            get
            {
                if (_previousClip == null)
                {
                    _previousClip = new RelayCommand(
                        p => LoadPreviousClip(),
                        p => SelectedTrip != null && SelectedTrip.CurrentClip.PreviousFile != null);
                }
                return _previousClip;
            }
        }

        public ICommand ChangePlayerSpeed
        {
            get
            {
                if (_changePlayerSpeed == null)
                {
                    _changePlayerSpeed = new RelayCommand(
                    p => SetPlayerSpeed(Convert.ToDouble(p)));
                }

                return _changePlayerSpeed;
            }
        }

        public ICommand GetMediaScreen
        {
            get
            {
                if (_getMediaScreen == null)
                {
                    _getMediaScreen = new RelayCommand(
                        p => CaptureMediaScreen());
                }

                return _getMediaScreen;
            }
        }

        public ICommand NextMediaFrame
        {
            get
            {
                if (_nextMediaFrame == null)
                {
                    _nextMediaFrame = new RelayCommand(
                        p => NextFrame());
                }

                return _nextMediaFrame;
            }
        }

        public ICommand PreviousMediaFrame
        {
            get
            {
                if (_previousMediaFrame == null)
                {
                    _previousMediaFrame = new RelayCommand(
                        p => PreviousFrame());
                }

                return _previousMediaFrame;
            }
        }

        public ICommand SettingsFlyout
        {
            get
            {
                if (_settingsFlyout == null)
                {
                    _settingsFlyout = new RelayCommand(
                        p => ToggleSettingsFlyout());
                }

                return _settingsFlyout;
            }
        }

        public ICommand AboutWindow
        {
            get
            {
                if (_aboutWindow == null)
                {
                    _aboutWindow = new RelayCommand(
                        p => ShowAboutWindow());
                }

                return _aboutWindow;
            }
        }

        public bool AboutWindowShow
        {
            get { return _about; }
            set
            {
                _about = value;
                OnPropertyChanged("AboutWindowShow");
            }
        }

        public ICommand ExportJourney
        {
            get
            {
                if (_exportJourney == null)
                {
                    _exportJourney = new RelayCommand(
                        p => ExtractJourney(),
                        p => SelectedTrip != null);
                }

                return _exportJourney;
            }
        }

        public ICommand ExportClip
        {
            get
            {
                if (_exportClip == null)
                {
                    _exportClip = new RelayCommand(
                        p => ExtractClip(p.ToString()),
                        p => SelectedTrip != null);
                }

                return _exportClip;
            }
        }

        public ICommand ExportClips
        {
            get
            {
                if (_exportClips == null)
                {
                    _exportClips = new RelayCommand(
                        p => ExtractClips(),
                        p => SelectedTrip != null);
                }

                return _exportClips;
            }
        }

        #endregion

        #region Methods

        public void UpdatePlayTime()
        {
            SelectedTrip.PlayTime = SelectedTrip.CurrentClip.StartTime.AddSeconds(CurrentPlayer.Position.TotalSeconds);

            // If we are at the end of the current clip
            if (DateTime.Compare(SelectedTrip.PlayTime, SelectedTrip.CurrentClip.EndTime) >= 0)
            {
                if (string.IsNullOrEmpty(SelectedTrip.CurrentClip.NextFile))
                    CurrentPlayer.Pause();
                else
                    LoadNextClip();
            }
            // If we are near the end of the current clip
            else if (DateTime.Compare(SelectedTrip.PlayTime, SelectedTrip.CurrentClip.EndTime.AddSeconds(-2)) >= 0)
                PrepareNextClip();
        }

        private void LoadNextClip()
        {
            NovatekFile nextClip = SelectedTrip.Clips.Where(x => x.FileName == SelectedTrip.CurrentClip.NextFile).SingleOrDefault();

            // Check the player has a clip loaded
            if (SparePlayer.HasVideo && _spClip == nextClip && SparePlayer.NaturalDuration.HasTimeSpan)
            {
                // Match the SpeedRatio and Volume
                SparePlayer.SpeedRatio = CurrentPlayer.SpeedRatio;
                SparePlayer.Volume = CurrentPlayer.Volume;
                // Show the spare player
                SparePlayer.Visibility = Visibility.Visible;
                CurrentPlayer.Visibility = Visibility.Hidden;
                SparePlayer.Play();
                CurrentPlayer.Pause();
                // Set the maximum of the position bar
                UI.FindChild<Slider>(Application.Current.MainWindow, "posSlider").Maximum = SparePlayer.NaturalDuration.TimeSpan.TotalSeconds;
                // Switch the current player with spare
                var spare = SparePlayer;
                var current = CurrentPlayer;
                CurrentPlayer = spare;
                SparePlayer = current;
                // Update the curernt clip
                SelectedTrip.CurrentClip = nextClip;
            }
            else if (!string.IsNullOrEmpty(SelectedTrip.CurrentClip.NextFile))
            {
                PrepareNextClip();
                Thread.Sleep(10);
                LoadNextClip();
            }
            else
            {
                CurrentPlayer.Pause();
                SparePlayer.Pause();
            }
        }

        private void LoadPreviousClip()
        {
            NovatekFile previousClip = SelectedTrip.Clips.Where(x => x.FileName == SelectedTrip.CurrentClip.PreviousFile).SingleOrDefault();

            // Check the player has a clip loaded
            if (SparePlayer.HasVideo && _spClip == previousClip && SparePlayer.NaturalDuration.HasTimeSpan)
            {
                // Match the SpeedRatio and Volume
                SparePlayer.SpeedRatio = CurrentPlayer.SpeedRatio;
                SparePlayer.Volume = CurrentPlayer.Volume;
                // Show the spare player
                SparePlayer.Visibility = Visibility.Visible;
                CurrentPlayer.Visibility = Visibility.Hidden;
                SparePlayer.Play();
                CurrentPlayer.Pause();
                // Set the maximum of the position bar
                UI.FindChild<Slider>(Application.Current.MainWindow, "posSlider").Maximum = SparePlayer.NaturalDuration.TimeSpan.TotalSeconds;
                // Switch the current player with spare
                var spare = SparePlayer;
                var current = CurrentPlayer;
                CurrentPlayer = spare;
                SparePlayer = current;
                // Update the curernt clip
                SelectedTrip.CurrentClip = previousClip;
            }
            else if (!string.IsNullOrEmpty(SelectedTrip.CurrentClip.PreviousFile))
            {
                PreparePreviousClip();
                Thread.Sleep(10);
                LoadPreviousClip();
            }
        }

        private void PrepareNextClip()
        {
            if (!string.IsNullOrEmpty(SelectedTrip.CurrentClip.NextFile))
            {
                // Load the next file into the spare player
                NovatekFile next = SelectedTrip.Clips.Where(x => x.FileName == SelectedTrip.CurrentClip.NextFile).SingleOrDefault();
                SparePlayer.Source = new Uri(next.FullNameAndPath);
                SparePlayer.Play();
                SparePlayer.Pause();
                _spClip = next;
            }
        }

        private void PreparePreviousClip()
        {
            if (!string.IsNullOrEmpty(SelectedTrip.CurrentClip.PreviousFile))
            {
                // Load the next file into the spare player
                NovatekFile previous = SelectedTrip.Clips.Where(x => x.FileName == SelectedTrip.CurrentClip.PreviousFile).SingleOrDefault();
                SparePlayer.Source = new Uri(previous.FullNameAndPath);
                SparePlayer.Play();
                SparePlayer.Pause();
                _spClip = previous;
            }
        }

        private void ToggleSettingsFlyout()
        {
            UI.FindChild<Flyout>(Application.Current.MainWindow, "settingsFlyout").IsOpen = !UI.FindChild<Flyout>(Application.Current.MainWindow, "settingsFlyout").IsOpen;
        }

        private void ShowAboutWindow()
        {
            // Close the settings flyout if it's shown
            if (UI.FindChild<Flyout>(Application.Current.MainWindow, "settingsFlyout").IsOpen)
                UI.FindChild<Flyout>(Application.Current.MainWindow, "settingsFlyout").IsOpen = false;

            AboutWindowShow = true;
        }

        private void PlayPauseMedia()
        {
            if (Media.GetMediaState(CurrentPlayer) == MediaState.Play)
                CurrentPlayer.Pause();
            else
                CurrentPlayer.Play();

            MediaPlayToggle();
        }

        private void MediaPlayToggle()
        {
            if (Media.GetMediaState(CurrentPlayer) == MediaState.Play)
                UI.FindChild<PackIconFontAwesome>(Application.Current.MainWindow, "pausePlayBtnIcon").Kind = PackIconFontAwesomeKind.PauseSolid;
            else
                UI.FindChild<PackIconFontAwesome>(Application.Current.MainWindow, "pausePlayBtnIcon").Kind = PackIconFontAwesomeKind.PlaySolid;
        }

        private void MutePlayerAudio()
        {
            if (UI.FindChild<MediaElement>(Application.Current.MainWindow, _currentPlayer).IsMuted)
            {
                UI.FindChild<Slider>(Application.Current.MainWindow, "volSlider").Value = _previousVol;
                UI.FindChild<Slider>(Application.Current.MainWindow, "volSlider").IsEnabled = true;
                VolumeIcon = PackIconOcticonsKind.Unmute;
                UI.FindChild<MediaElement>(Application.Current.MainWindow, _currentPlayer).IsMuted = false;
            }
            else
            {
                _previousVol = UI.FindChild<Slider>(Application.Current.MainWindow, "volSlider").Value;
                UI.FindChild<Slider>(Application.Current.MainWindow, "volSlider").Value = 0;
                UI.FindChild<Slider>(Application.Current.MainWindow, "volSlider").IsEnabled = false;
                VolumeIcon = PackIconOcticonsKind.Mute;
                UI.FindChild<MediaElement>(Application.Current.MainWindow, _currentPlayer).IsMuted = true;
            }
        }

        private void SetPlayerSpeed(double speed)
        {
            CurrentPlayer.SpeedRatio = speed;
        }

        private void CaptureMediaScreen()
        {
            bool playing;
            if (Media.GetMediaState(CurrentPlayer) == MediaState.Play)
            {
                playing = true;
                CurrentPlayer.Pause();
            }
            else
            {
                playing = false;
            }

            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.FileName = _selectedTrip.PlayTime.ToString("yyyy_MMdd_HHmmss_") + "frame";
            saveDlg.DefaultExt = ".jpg";
            saveDlg.Filter = "JPEG Images|*.jpg";

            // Show the save dialog box
            Nullable<bool> result = saveDlg.ShowDialog();

            // Process the save file
            if (result == true)
            {
                // Save the screenshot
                byte[] screenshot = Media.getScreen(CurrentPlayer, 1, 90);
                FileStream fileStream = new FileStream(saveDlg.FileName, FileMode.Create, FileAccess.ReadWrite);
                BinaryWriter binaryWriter = new BinaryWriter(fileStream);
                binaryWriter.Write(screenshot);
                binaryWriter.Close();
            }

            if (playing)
                CurrentPlayer.Play();

        }

        // Hack Work-Around
        private void NextFrame()
        {
            // Get the current SpeedRatio
            double _speed = CurrentPlayer.SpeedRatio;
            // Set the speed really low
            CurrentPlayer.SpeedRatio = 0.001;
            // Play for a 1/4 second then pause
            CurrentPlayer.Play();
            Thread.Sleep(250);
            CurrentPlayer.Pause();
            // Reset the SpeedRatio
            CurrentPlayer.SpeedRatio = _speed;
        }

        // Hack Work-Around
        private void PreviousFrame()
        {
            // Get current SpeedRatio & Position
            double _speed = CurrentPlayer.SpeedRatio;
            double _pos = CurrentPlayer.Position.TotalSeconds;
            // Move a tenth of a second back
            CurrentPlayer.Position = TimeSpan.FromSeconds((_pos - 0.1));
            // Set the speed really low
            CurrentPlayer.SpeedRatio = 0.001;
            // Play for a 1/4 second then Pause
            CurrentPlayer.Play();
            Thread.Sleep(250);
            CurrentPlayer.Pause();
            // Reset the SpeedRation
            CurrentPlayer.SpeedRatio = _speed;
        }

        private async void ExtractJourney()
        {
            // Ask where to save the file
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.FileName = _selectedTrip.Clips.First().FileName.Substring(0, _selectedTrip.Clips.First().FileName.Length - 4);
            saveDlg.DefaultExt = ".gpx";
            saveDlg.Filter = "GPS Exchange Format|*.gpx";

            // Show the save dialog box
            Nullable<bool> result = saveDlg.ShowDialog();

            // Process the save file
            if (result == true && Path.GetExtension(saveDlg.FileName) == ".gpx")
            {
                // TODO: Seperate into GPX/CSV
                // GPX template
                string gpx;
                gpx = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n";
                gpx += "<gpx version=\"1.0\" ";
                gpx += "creator=\"NovaTrakt by PAW Soft\" ";
                gpx += "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ";
                gpx += "xmlns=\"http://www.topografix.com/GPX/1/0\" ";
                gpx += "xsi:schemaLocation=\"http://www.topografix.com/GPX/1/0 http://www.topografix.com/GPX/1/0/gpx.xsd\">\r\n";
                gpx += "\t<name>" + saveDlg.SafeFileName + "</name>\r\n";
                gpx += "\t<url>http://novatrakt.pawsoft.co.uk</url>\r\n";
                gpx += "\t<trk>\r\n";
                gpx += "\t<name>" + saveDlg.SafeFileName + "</name>\r\n";
                gpx += "\t<trkseg>\r\n";

                foreach (GPSData gps in _selectedTrip.GPSData)
                {
                    gpx += "\t\t<trkpt lat=\"" + gps.Latitude.ToString(CultureInfo.InvariantCulture) + "\" " +
                                      "lon=\"" + gps.Longitude.ToString(CultureInfo.InvariantCulture) + "\">" +
                                      "<time>" + String.Format("{0:s}", gps.DateTime) + "</time>" +
                                      "<speed>" + gps.Speed.ToString(CultureInfo.InvariantCulture) + "</speed></trkpt>\r\n";
                }

                gpx += "\t</trkseg>\r\n";
                gpx += "\t</trk>\r\n";
                gpx += "</gpx>\n";

                // Write to GPX file
                try
                {
                    // Delete the file if it exists.
                    if (File.Exists(saveDlg.FileName))
                        File.Delete(saveDlg.FileName);

                    // Create the file.
                    using (FileStream fs = File.Create(saveDlg.FileName))
                    {
                        Byte[] info = new UTF8Encoding(true).GetBytes(gpx);
                        // Add some information to the file.
                        fs.Write(info, 0, info.Length);
                    }

                    // DEBUG: Completed Message
                    Log.WriteLine("Info", "GPX file (" + saveDlg.FileName + ") created!");
                    // Show complete message
                    MetroWindow w = (MetroWindow)Application.Current.MainWindow;
                    await w.ShowMessageAsync("Journey Extracted", "GPS data was extracted from your journey containing (" + _selectedTrip.Clips.Count() + " clips). \r\n\r\n"
                                                + "The GPX file was saved as:  \r\n\t" + saveDlg.FileName);
                }
                catch (Exception ex)
                {
                    Log.WriteLine("ERROR", "Unable to create GPX file: " + saveDlg.FileName);
                    MessageBox.Show(ex.ToString());
                }

            }
        }

        private async void ExtractClip(string fileWithPath)
        {
            // Ask where to save the file
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.FileName = Path.GetFileNameWithoutExtension(fileWithPath);
            saveDlg.DefaultExt = ".gpx";
            saveDlg.Filter = "GPS Exchange Format|*.gpx";

            // Show the save dialog box
            Nullable<bool> result = saveDlg.ShowDialog();

            // Process the save file
            if (result == true && Path.GetExtension(saveDlg.FileName) == ".gpx")
            {
                NovatekFile file = _selectedTrip.Clips.Where(c => c.FullNameAndPath == fileWithPath).Single();

                // GPX template
                string gpx;
                gpx = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n";
                gpx += "<gpx version=\"1.0\" ";
                gpx += "creator=\"NovaTrakt by PAW Soft\" ";
                gpx += "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ";
                gpx += "xmlns=\"http://www.topografix.com/GPX/1/0\" ";
                gpx += "xsi:schemaLocation=\"http://www.topografix.com/GPX/1/0 http://www.topografix.com/GPX/1/0/gpx.xsd\">\r\n";
                gpx += "\t<name>" + saveDlg.SafeFileName + "</name>\r\n";
                gpx += "\t<url>http://novatrakt.pawsoft.co.uk</url>\r\n";
                gpx += "\t<trk>\r\n";
                gpx += "\t<name>" + saveDlg.SafeFileName + "</name>\r\n";
                gpx += "\t<trkseg>\r\n";

                foreach (GPSData gps in file.AllGPSData)
                {
                    gpx += "\t\t<trkpt lat=\"" + gps.Latitude.ToString(CultureInfo.InvariantCulture) + "\" " +
                                      "lon=\"" + gps.Longitude.ToString(CultureInfo.InvariantCulture) + "\">" +
                                      "<time>" + String.Format("{0:s}", gps.DateTime) + "</time>" +
                                      "<speed>" + gps.Speed.ToString(CultureInfo.InvariantCulture) + "</speed></trkpt>\r\n";
                }

                gpx += "\t</trkseg>\r\n";
                gpx += "\t</trk>\r\n";
                gpx += "</gpx>\n";

                // Write to GPX file
                try
                {
                    // Delete the file if it exists.
                    if (File.Exists(saveDlg.FileName))
                        File.Delete(saveDlg.FileName);

                    // Create the file.
                    using (FileStream fs = File.Create(saveDlg.FileName))
                    {
                        Byte[] info = new UTF8Encoding(true).GetBytes(gpx);
                        // Add some information to the file.
                        fs.Write(info, 0, info.Length);
                    }

                    // DEBUG: Completed Message
                    Log.WriteLine("Info", "GPX file (" + saveDlg.FileName + ") created!");

                    // Show complete message
                    MetroWindow w = (MetroWindow)Application.Current.MainWindow;
                    await w.ShowMessageAsync("File Extracted", "GPS data was extracted from " + Path.GetFileName(fileWithPath).ToString() + ". \r\n\r\n"
                                                + "The GPX file was saved as:  \r\n\t" + saveDlg.FileName);
                }
                catch (Exception ex)
                {
                    Log.WriteLine("ERROR", "Unable to create GPX file: " + saveDlg.FileName);
                    MessageBox.Show(ex.ToString());
                }
            }
        }
        private void ExtractClip(string fileWithPath, string outputDirectory)
        {
            string SafeFileName = Path.GetFileNameWithoutExtension(fileWithPath);
            string outputFile = outputDirectory + @"\" + Path.GetFileNameWithoutExtension(fileWithPath) + ".gpx";
            NovatekFile file = _selectedTrip.Clips.Where(c => c.FullNameAndPath == fileWithPath).Single();

            // GPX template
            string gpx;
            gpx = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n";
            gpx += "<gpx version=\"1.0\" ";
            gpx += "creator=\"NovaTrakt by PAW Soft\" ";
            gpx += "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ";
            gpx += "xmlns=\"http://www.topografix.com/GPX/1/0\" ";
            gpx += "xsi:schemaLocation=\"http://www.topografix.com/GPX/1/0 http://www.topografix.com/GPX/1/0/gpx.xsd\">\r\n";
            gpx += "\t<name>" + SafeFileName + "</name>\r\n";
            gpx += "\t<url>http://novatrakt.pawsoft.co.uk</url>\r\n";
            gpx += "\t<trk>\r\n";
            gpx += "\t<name>" + SafeFileName + "</name>\r\n";
            gpx += "\t<trkseg>\r\n";

            foreach (GPSData gps in file.AllGPSData)
            {
                gpx += "\t\t<trkpt lat=\"" + gps.Latitude.ToString(CultureInfo.InvariantCulture) + "\" " +
                                    "lon=\"" + gps.Longitude.ToString(CultureInfo.InvariantCulture) + "\">" +
                                    "<time>" + String.Format("{0:s}", gps.DateTime) + "</time>" +
                                    "<speed>" + gps.Speed.ToString(CultureInfo.InvariantCulture) + "</speed></trkpt>\r\n";
            }

            gpx += "\t</trkseg>\r\n";
            gpx += "\t</trk>\r\n";
            gpx += "</gpx>\n";

            // Write to GPX file
            try
            {
                // Delete the file if it exists.
                if (File.Exists(outputFile))
                    File.Delete(outputFile);

                // Create the file.
                using (FileStream fs = File.Create(outputFile))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes(gpx);
                    // Add some information to the file.
                    fs.Write(info, 0, info.Length);
                }

                // DEBUG: Completed Message
                Log.WriteLine("Info", "GPX file (" + outputFile + ") created!");
            }
            catch (Exception ex)
            {
                Log.WriteLine("ERROR", "Unable to create GPX file: " + outputFile);
                MessageBox.Show(ex.ToString());
            }
        }

        private async void ExtractClips()
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog();
            dlg.IsFolderPicker = true;
            dlg.Title = "Select The Folder You Wish To Save To";
            dlg.DefaultDirectory = AppSettings.getString("LastInputDirectory", @"C:\");

            // Show the save dialog box
            CommonFileDialogResult result = dlg.ShowDialog();

            int x = 0;

            // Process the save file
            if (result == CommonFileDialogResult.Ok)
            {
                foreach (NovatekFile clip in _selectedTrip.Clips)
                {
                    ExtractClip(clip.FullNameAndPath, dlg.FileName);
                    x++;
                }
            }

            // Show complete message
            MetroWindow w = (MetroWindow)Application.Current.MainWindow;
            await w.ShowMessageAsync("Files Extracted", "GPS data was extracted from " + x.ToString() + " clips. \r\n\r\n"
                                        + x.ToString() + " GPX files were created in: \r\n\t" + dlg.FileName);
        }

        private void _clearFilesSource()
        {
            _clips.Clear();
            _journeyList.Clear();
        }

        public async void _selectInputFolder()
        {
            // REFERENCE: http://stackoverflow.com/questions/4007882/select-folder-dialog-wpf/17712949#17712949

            // Force region to EN
            //CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CreateSpecificCulture("en");

            CommonOpenFileDialog inputFolderDialog = new CommonOpenFileDialog();
            inputFolderDialog.Title = "Select Input Folder";
            inputFolderDialog.IsFolderPicker = true;
            inputFolderDialog.InitialDirectory = AppSettings.getString("LastInputDirectory", @"C:\");
            inputFolderDialog.EnsurePathExists = true;
            inputFolderDialog.Multiselect = false;

            if (inputFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // Save the last used directory to user settings
                AppSettings.set("LastInputDirectory", inputFolderDialog.FileName);

                // Create a Progress Dialog
                MetroWindow w = (MetroWindow)Application.Current.MainWindow;
                _progressDlg = await w.ShowProgressAsync("Please wait...", "Scanning Directory for compatible files.");

                // Scan the directory for files
                _inputPath = inputFolderDialog.FileName;
                openWorker.RunWorkerAsync();
            }
        }

        private bool _gps1 = false;
        private bool _gps2 = false;

        // Retrieve all usable MP4 files in a directory
        private void OpenWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Update the progress message and set to indeterminate
            _progressDlg.SetMessage("Scanning Directory for compatible files.\r\nExtracting GPS Coordinates and Speed Data.");
            _progressDlg.SetIndeterminate();

            // Organise into journies
            gpsWorker.RunWorkerAsync();
            gpsWorker2.RunWorkerAsync();
        }
        private void OpenWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Force this thread to work in EN Culture
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en");

            // Get all the MP4 files in the chosen directory
            string[] inputFiles;
            string[] inputFolder = Directory.GetFiles(_inputPath, "*.mp4");
            if (Directory.Exists(_inputPath + @"\RO"))
            {
                string[] inputFolderRO = Directory.GetFiles(_inputPath + @"\RO", "*.mp4");
                inputFiles = inputFolder.Concat(inputFolderRO).ToArray();
            }
            else
            {
                Log.WriteVerboseLine("RO Directory does not exist.");
                inputFiles = inputFolder;
            }

            // Set the progress status
            _progressDlg.Maximum = inputFiles.Count();
            double count = 0;

            // Clear existing list of clips
            _clips.Clear();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _journeyList.Clear()));

            foreach (string file in inputFiles)
            {
                Log.WriteLine("Info", "Loading file " + file);
                FileInfo fileInfo = new FileInfo(file);

                // TODO add some switching between Novatek and 70mai
                NovatekFile _file = new NovatekFile(fileInfo.DirectoryName, fileInfo.Name);

                if (_file.MoovBox != 0)
                {
                    Log.WriteVerboseLine("File is a valid MP4 video file.");

                    _clips.Add(_file);
                }
                else
                    Log.WriteLine("ERROR", "File at " + file + " does not seem to be a valid video file. File will be ignored.");

                _progressDlg.SetProgress(count++);
            }
        }

        /*
         * We use two GPS Workers for speed, one handles "even" indexed files and the other "odd" indexed files
         */

        // Check and get the GPS for each clip - 'EVEN' files
        private void GpsWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _gps1 = true;
            if (_gps1 && _gps2)
            {
                // Update the progress message and set to indeterminate
                _progressDlg.SetMessage("Scanning Directory for compatible files.\r\nExtracting GPS Coordinates and Speed Data.\r\nOrganising Clips into Journeys.");
                _progressDlg.SetIndeterminate();

                // Organise into journies
                organisationWorker.RunWorkerAsync();
            }
        }
        private void GpsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _gps1 = false;

            // Force this thread to work in EN Culture
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en");

            // Set the progress status
            _progressDlg.Maximum = _clips.Count();
            double count = 0;


            foreach (NovatekFile clip in _clips.Where((x, i) => i % 2 == 0))
            {
                clip.Process();

                _progressDlg.SetProgress(count++);
            }
        }
        // Check and get the GPS for each clip - 'OFF' files
        private void GpsWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _gps2 = true;
            if (_gps1 && _gps2)
            {
                // Update the progress message and set to indeterminate
                _progressDlg.SetMessage("Scanning Directory for compatible files.\r\nExtracting GPS Coordinates and Speed Data.\r\nOrganising Clips into Journeys.");
                _progressDlg.SetIndeterminate();

                // Organise into journies
                organisationWorker.RunWorkerAsync();
            }
        }
        private void GpsWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            _gps2 = false;

            // Force this thread to work in EN Culture
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en");

            double count = 0;

            foreach (NovatekFile clip in _clips.Where((x, i) => i % 2 != 0))
            {
                clip.Process();

                _progressDlg.SetProgress(count++);
            }
        }


        // Organise the clips into Journeys
        private async void OrganisationWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Close the Progress Dialog
            await _progressDlg.CloseAsync();

            MetroWindow w = (MetroWindow)Application.Current.MainWindow;
            if (_clips.Count == 0)
                await w.ShowMessageAsync("No compatible files found.",
                                         "No files compatible with NovaTrakt were found in the selected folder. Please check the chosen folder and try again.");
        }
        private async void OrganisationWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Force this thread to work in EN Culture
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en");

            // Sort the list of _Clips by StartTime
            _clips = _clips.OrderBy(o => o.StartTime).ToList();

            // Organise previous/next clips
            foreach (NovatekFile A in _clips)
            {
                // Find the file directly before this one
                if (string.IsNullOrEmpty(A.NextFile))
                {
                    foreach (NovatekFile B in _clips)
                    {
                        if (A.FileName != B.FileName && string.IsNullOrEmpty(A.NextFile))
                        {
                            TimeSpan diff = B.StartTime - A.EndTime;
                            // Anywhere between a -5 seconds and 5 minutes gap between the files
                            if (diff.TotalSeconds >= -5 && diff.TotalSeconds <= 300)
                            {
                                A.NextFile = B.FileName;
                                B.PreviousFile = A.FileName;
                            }
                        }
                    }
                }
                // Find the file directly after this one
                if (string.IsNullOrEmpty(A.PreviousFile))
                {
                    foreach (NovatekFile C in _clips)
                    {
                        if (A.FileName != C.FileName && string.IsNullOrEmpty(A.PreviousFile))
                        {
                            TimeSpan diff = A.StartTime - C.EndTime;
                            // Anywhere between a -5 seconds and 5 minutes gap between the files
                            if (diff.TotalSeconds >= -5 && diff.TotalSeconds <= 300)
                            {
                                A.PreviousFile = C.FileName;
                                C.NextFile = A.FileName;
                            }
                        }
                    }
                }
            }
            // Organise Journeys
            foreach (NovatekFile D in _clips)
            {
                // If this is a starting file, create a new journey
                if (string.IsNullOrEmpty(D.PreviousFile))
                {
                    Journey j = new Journey();

                    // we probably always know the start time (timestamp, filename)
                    j.StartTime = D.StartTime;
                    j._clips.Add(D);

                    if (D.ValidGPS && D.GPSData != null)
                    {
                        // Retrieve the start town
                        await D.SetStartTown();

                        j.StartTown = D.StartTown;
                        j.Distance = D.Distance;

                        foreach (GPSData gps in D.GPSData)
                        {
                            j.GPSLocationsFiltered.Add(new Location(gps.Latitude, gps.Longitude));
                        }
                        foreach (GPSData gps in D.AllGPSData)
                        {
                            j.GPSData.Add(gps);
                            j.GPSLocations.Add(new Location(gps.Latitude, gps.Longitude));
                        }

                        if (string.IsNullOrEmpty(D.NextFile))
                        {
                            // Retrieve the end town
                            await D.SetEndTown();

                            if (D.ValidGPS)
                                j.EndTown = D.EndTown;

                            j.EndTime = D.EndTime;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(D.NextFile))
                        {
                            j.EndTime = D.EndTime;
                        }
                    }

                    await Application.Current.Dispatcher.BeginInvoke(new Action(() => _journeyList.Add(j)));
                }
                else
                {
                    NovatekFile c = _clips.Where(x => x.FileName == D.PreviousFile).FirstOrDefault();
                    // Otherwise find the journey containing the previous file and add this to it
                    foreach (Journey j in _journeyList)
                    {
                        if (j.Clips.Contains(c))
                        {
                            j._clips.Add(D);

                            if (D.ValidGPS && D.GPSData != null)
                            {
                                j.Distance = (j.Distance + D.Distance);

                                foreach (GPSData gps in D.GPSData)
                                {
                                    j.GPSLocationsFiltered.Add(new Location(gps.Latitude, gps.Longitude));
                                }
                                foreach (GPSData gps in D.AllGPSData)
                                {
                                    j.GPSData.Add(gps);
                                    j.GPSLocations.Add(new Location(gps.Latitude, gps.Longitude));
                                }

                                if (string.IsNullOrEmpty(D.NextFile))
                                {
                                    // Retrieve the end town
                                    await D.SetEndTown();
                                    j.EndTown = D.EndTown;
                                }
                            }

                            j.EndTime = D.EndTime;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
