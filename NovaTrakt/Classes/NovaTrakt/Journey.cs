using Microsoft.Maps.MapControl.WPF;

// TODO add some camera type switch...
//using Novatek;
using Mai70; // out placeholder hack..

using Novatek.Core;
using NovaTrakt.Classes.Helpers;
using NovaTrakt.SharedComponents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace NovaTrakt.Classes.NovaTrakt
{
    class Journey : INotifyPropertyChanged
    {
        // Is the selected file in the datagrid
        private bool _selected;
        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                if (CurrentClip.ValidGPS)
                    UI.FindChild<Pushpin>(Application.Current.MainWindow, "ntMarker").Visibility = Visibility.Visible;
                else
                    UI.FindChild<Pushpin>(Application.Current.MainWindow, "ntMarker").Visibility = Visibility.Hidden;
                PlayTime = StartTime;
                OnPropertyChanged("Selected");
            }
        }

        // Journey Start and End Times
        private DateTime _startTime;
        public DateTime StartTime
        {
            get { return _startTime; }
            set
            {
                _startTime = value;
                OnPropertyChanged("StartTime");
            }
        }

        private DateTime _endTime;
        public DateTime EndTime
        {
            get { return _endTime; }
            set
            {
                _endTime = value;
                OnPropertyChanged("EndTime");
            }
        }

        // Current time from Video
        private DateTime _playTime;
        public DateTime PlayTime
        {
            get { return _playTime; }
            set
            {
                _playTime = value;
                PlayLocation = getCurrentLocation(value);
                OnPropertyChanged("PlayTime");
            }
        }

        // Current Clip (Defaults to first clip)
        private NovatekFile _currentClip;
        public NovatekFile CurrentClip
        {
            get
            {
                if (_currentClip != null)
                    return _currentClip;

                return Clips.FirstOrDefault();
            }
            set
            {
                _currentClip = value;
                OnPropertyChanged("CurrentClip");
            }
        }

        // Current Location matching PlayTime
        private Location _playLocation;
        public Location PlayLocation
        {
            get
            {
                if (_playLocation == null)
                    return GPSLocations.FirstOrDefault();
                return _playLocation;
            }
            set
            {
                _playLocation = value;
                if (CurrentClip.ValidGPS)
                    UI.FindChild<Map>(Application.Current.MainWindow, "ntMap").SetView(_playLocation, 16);
                OnPropertyChanged("PlayLocation");
            }
        }

        // Current GPSData matching PlayTime
        private GPSData _playData;
        public GPSData PlayData
        {
            get
            {
                if (_playData == null)
                    return new GPSData(AppSettings.getBool("MPH", false));
                return _playData;
            }
            set
            {
                _playData = value;
                OnPropertyChanged("PlayData");
            }
        }

        // Total distance travelled in the clip (m)
        private double _distance;
        public double Distance
        {
            get { return _distance; }
            set
            {
                _distance = value;
                OnPropertyChanged("Distance");
            }
        }

        // Total distance travelled with unit
        public string DistanceString
        {
            get
            {
                if (AppSettings.getBool("MPH", false))
                {
                    return Math.Round(_distance, 2).ToString() + " Miles";
                }
                else
                {
                    return Math.Round(_distance * 1.609344, 2).ToString() + " KM";
                }
            }
        }

        // Town at start of clip
        private string _startTown;
        public string StartTown
        {
            get { return _startTown; }
            set
            {
                _startTown = value;
                OnPropertyChanged("StartTown");
            }
        }
        // Town at end of clip
        private string _endTown;
        public string EndTown
        {
            get { return _endTown; }
            set
            {
                _endTown = value;
                OnPropertyChanged("EndTown");
            }
        }

        // All the clips and GPS data within this journey
        public ObservableCollection<NovatekFile> _clips = new ObservableCollection<NovatekFile>();
        public IEnumerable<NovatekFile> Clips
        {
            get { return _clips; }
        }
        public List<GPSData> GPSData = new List<GPSData>();
        private LocationCollection _gpsLocations { get; set; }
        public LocationCollection GPSLocations
        {
            get
            {
                if (_gpsLocations == null)
                {
                    _gpsLocations = new LocationCollection();
                }
                return _gpsLocations;
            }
            set
            {
                _gpsLocations = value;
                OnPropertyChanged("GPSLocations");
            }
        }
        private LocationCollection _gpsLocationsFiltered { get; set; }
        public LocationCollection GPSLocationsFiltered
        {
            get
            {
                if (_gpsLocationsFiltered == null)
                {
                    _gpsLocationsFiltered = new LocationCollection();
                }
                return _gpsLocationsFiltered;
            }
            set
            {
                _gpsLocationsFiltered = value;
                OnPropertyChanged("GPSLocationsFiltered");
            }
        }

        // Return a Location from GPS Data based upon a date time
        public Location getCurrentLocation(DateTime d)
        {
            foreach (GPSData x in this.GPSData)
            {
                if (x.DateTime.ToString() == d.ToString())
                {
                    PlayData = x;
                    return new Location(x.Latitude, x.Longitude);
                }
            }
            return PlayLocation;
        }

        // Custom handler for property changed notification
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
