using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Novatek.Core;

namespace Mai70
{

    public class NovatekFile
    {

        //The name of the file.
        public string FileName { get; set; }

        //The full path to the file.
        public string FilePath { get; set; }

        //The full path and name of the file.
        public string FullNameAndPath { get; }

        //Used for organising journeys, this string contains the FileName of the clip that should play prior to this one.
        public string PreviousFile { get; set; }

        //Used for organising journeys, this string contains the FileName of the clip that should play after this one.
        public string NextFile { get; set; }

        //Contains the position of the box containing the Moov data. If this is 0, the file does not contain valid video data.
        public long MoovBox { get; set; }

        //The date and time that the video clip starts.
        public DateTime StartTime { get; set; }

        //The date and time that the video clip ends.
        public DateTime EndTime { get; set; }

        //The length, in seconds, of the video clip.
        public int Duration { get; set; }

        //The GPS Box Data of the video clip.
        public GPSBox GPSBox { get; set; }

        //Whether or not the video file contain valid GPS data.
        public bool ValidGPS { get; set; }

        //The starting town of the video clip, set by SetStartTown();
        public string StartTown { get; set; }

        //The ending town of the video clip, set by SetEndTown();
        public string EndTown { get; set; }

        //The total distance traveled, in meters, in the video clip.
        public double Distance { get; set; }

        //The Kalman filtered list of GPS data points found in the video file
        public List<GPSData> GPSData { get; set; }

        //The unfiltered list of GPS data points found in the video file
        public List<GPSData> AllGPSData { get; set; }


        //This function will construct the class, open and confirm the video file is valid.
        //If the public long MoovBox is equal to 0, the video file is not valid.
        public NovatekFile(string _filePath, string _fileName)
        {
            FilePath = _filePath;
            FileName = _fileName;
            FullNameAndPath = _filePath + "\\" + _fileName; //Path.Join(filePath, fileName);

            AllGPSData = new List<GPSData>();
            GPSData = new List<GPSData>();
            GPSBox = new GPSBox();

            parseAtoms(FullNameAndPath);
        }

        //This function will scan the video file for valid GPS data and perform a Kalman filter to smooth the data points.
        //The public List GPSData stores the filtered GPS data.The public List AllGPSData stores the unfiltered GPS data.
        public NovatekFile Process()
        {
            PreviousFile = null;
            NextFile = null;
            Distance = 0.0;

            // no filtering yet, just copy over
            foreach (GPSData a in AllGPSData)
            {
                GPSData.Add(a);

                // TODO heading!!!
                a.Heading = 90;

                a.Distance = a.Speed * 1.0 / 1000.0; // quick and dirty hack assuming 1s segments...

                Distance += a.Distance;
            }
            ValidGPS = true;
            return this; // ???
        }

        //This function will use the Bing Maps Geocoding API to reverse Geocode the first latitude and longitude of the video clip into a starting town.
        public async Task SetStartTown()
        {
            //BasicGeoposition location = new BasicGeoposition();
            //location.Latitude = 47.643;
            //location.Longitude = -122.131;
            //Geopoint pointToReverseGeocode = new Geopoint(location);

            //// Reverse geocode the specified geographic location.
            //MapLocationFinderResult result =
            //      await MapLocationFinder.FindLocationsAtAsync(pointToReverseGeocode);

            //// If the query returns results, display the name of the town
            //// contained in the address of the first result.
            //if (result.Status == MapLocationFinderStatus.Success)
            //{
            //    tbOutputText.Text = "town = " +
            //          result.Locations[0].Address.Town;
            //}

            StartTown = "My Start town";
            return;// Task.FromResult<object>(null);
        }

        //This function will use the Bing Maps Geocoding API to reverse Geocode the last latitude and longitude of the video clip into a ending town.
        public async Task SetEndTown()
        {
            EndTown = "My End town";
            return; // Task.FromResult<object>(null);
        }



        static int parseLeInt(byte[] buff, long offset = 0)
        {
            int result = buff[offset + 3] << 24 | buff[offset + 2] << 16 | buff[offset + 1] << 8 | buff[offset + 0];
            return result;
        }
        static long parseBeUint(byte[] buff, long offset = 0)
        {
            long result = buff[offset + 0] << 24 | buff[offset + 1] << 16 | buff[offset + 2] << 8 | buff[offset + 3];
            return result;
        }

        static long parseBeLong(byte[] buff, long offset = 0)
        {
            long result = buff[offset + 0] << 56 | buff[offset + 1] << 48 | buff[offset + 2] << 40 | buff[offset + 3] << 32 |
                                buff[offset + 4] << 24 | buff[offset + 5] << 16 | buff[offset + 6] << 8 | buff[offset + 7];
            return result;
        }

        static bool ReadAtomHeader(FileStream f, long offset, string parent, ref long atomSize, ref string atomType, ref long headerSize)
        {
            byte[] atomHeader = new byte[8];
            if (f.Read(atomHeader, 0, 8) < 8)
            {
                //Trace.WriteLine($"End od file reached at {offset} while reading atom header");
                return false;
            }

            atomSize = parseBeUint(atomHeader);
            atomType = System.Text.Encoding.ASCII.GetString(atomHeader, 4, 4);
            headerSize = 8;

            //Trace.WriteLine($"Found atom \"{parent}/{atomType}\" at 0x{offset:X} size {atomSize} / 0x{atomSize:X}");

            if (atomSize == 0)
            {
                //Trace.WriteLine("This was the last top level atom - data till the end of the file");
                return true;
            }

            // big atom >2^32 B
            if (atomSize == 1)
            {
                if (f.Read(atomHeader, 0, 8) < 8)
                {
                    //Trace.WriteLine($"Unexpected enf of file at {offset} while reading big atom size");
                    return false;
                }
                atomSize = parseBeLong(atomHeader);
                headerSize += 8;
                //Trace.WriteLine($"Atom \"{parent}/{atomType}\" is big, its real size is {atomSize} / 0x{atomSize:X}");
            }
            return true;
        }







        // return true if ok
        private bool parseAtoms(string filePath)
        {
            MoovBox = 0;
            ValidGPS = false;
            Duration = 0;
            double duration = 0.0;

            string fName = Path.GetFileNameWithoutExtension(filePath);
            if (fName.Length < 24 || fName.Substring(0, 2) != "NO")
            {
                //Trace.WriteLine($"Expected 70mai file, not \"{inFile}\"");
                return false;
            }

            string timestamp = fName.Substring(2, 15);
            DateTime fTime;
            try
            {
                fTime = DateTime.ParseExact(timestamp, "yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                //Trace.WriteLine($"Cannot parse timestamp from \"{timestamp}\"");
                return false;
            }

            //Trace.WriteLine($"Parsed time from \"{fName}\" as {fTime}");

            // go over top level atoms
            using (FileStream f = File.OpenRead(filePath))
            {
                long offset = 0;
                long atomSize;
                string atomType;
                long headerSize;

                while (true)
                {
                    long atomStart = offset;
                    atomSize = 0;
                    atomType = "";
                    headerSize = 0;
                    if (!ReadAtomHeader(f, offset, "", ref atomSize, ref atomType, ref headerSize))
                        break;

                    if (atomType == "moov")
                    {
                        long subOffset = headerSize;
                        long subAtomSize;

                        MoovBox = offset;
                        while (subOffset < atomSize)
                        {
                            subAtomSize = 0;
                            if (!ReadAtomHeader(f, offset + subOffset, "/moov", ref subAtomSize, ref atomType, ref headerSize))
                                break;

                            subOffset += subAtomSize;

                            // get mvhd sub atom for clip duration
                            if (atomType == "mvhd")
                            {
                                int version = f.ReadByte();

                                // 3B flags
                                // version 1 = 28
                                //  8B create date
                                //  8B modify date
                                //  4B timescale
                                //  8B duration
                                // version 2 = 16
                                //  4B create date
                                //  4B modify date
                                //  4B timescale
                                //  4B duration

                                long structLen = 0;
                                if (version == 1)
                                    structLen = 31;
                                else if (version == 0)
                                    structLen = 19;
                                else
                                {
                                    //Trace.WriteLine($"Unexpected version {version} in atom mvhd");
                                    break;
                                }

                                if (structLen + headerSize > subAtomSize)
                                {
                                    //Trace.WriteLine($"Wrong size in atom mvhd");
                                    break;
                                }

                                byte[] atomHeader = new byte[structLen];
                                if (f.Read(atomHeader, 0, (int)structLen) < structLen)
                                {
                                    //Trace.WriteLine($"End od file reached at {offset} while reading atom mvhd");
                                    break;
                                }

                                long timeScale = 0;
                                long timeUnits = 0;
                                if (version == 1)
                                {
                                    timeScale = parseBeUint(atomHeader, 19);
                                    timeUnits = parseBeLong(atomHeader, 27);
                                }
                                else
                                {
                                    timeScale = parseBeUint(atomHeader, 11);
                                    timeUnits = parseBeUint(atomHeader, 15);
                                }

                                if (timeScale > 0 && timeUnits > 0)
                                {
                                    duration = timeUnits / timeScale;
                                }

                                //Trace.WriteLine($"Clip duration is {duration} seconds");

                                break; // we do not need other subatoms
                            }
                            f.Seek(offset + subOffset, SeekOrigin.Begin);
                        }
                    }
                    else if (atomType == "GPS ")
                    {
                        //Trace.WriteLine("Found GPS atom...");

                        // is it correct? GPSBox is not used so does not matter right now
                        GPSBox.Pos = offset;
                        GPSBox.Size = (int)atomSize;

                        long gpsSize = 8;
                        byte[] gpsRecord = new byte[36];

                        while (gpsSize + 36 <= atomSize)
                        {
                            if (f.Read(gpsRecord, 0, 36) < 36)
                            {
                                //Trace.WriteLine($"Unexpected end of file at {offset} while reading GPS record");
                                break;
                            }

                            int f1 = parseLeInt(gpsRecord);
                            int f2 = parseLeInt(gpsRecord, 4);
                            int timeDiff = parseLeInt(gpsRecord, 8);
                            int speed = parseLeInt(gpsRecord, 12);
                            char clat = Convert.ToChar(gpsRecord[16]);
                            int lat = parseLeInt(gpsRecord, 17);
                            char clon = Convert.ToChar(gpsRecord[21]);
                            int lon = parseLeInt(gpsRecord, 22);

                            //Trace.WriteLine($"f1={f1}, f2={f2}, time={timeDiff}, speed={speed}, clat {clat}, lat {lat}, clon {clon}, lon {lon}");

                            DateTime timeStamp = fTime.AddSeconds(timeDiff);

                            if((clon=='W' || clon=='E') && (clat=='N' || clat=='S'))
                            //if (f2 == 1) // valid GPS data, or another test?
                            {
                                double dLat = (lat / 100000) + ((lat % 100000) / 1000.0 / 60.0);
                                double dLon = (lon / 100000) + ((lon % 100000) / 1000.0 / 60.0);

                                // east+ / west-
                                if (clon == 'W')
                                    dLon = -dLon;

                                // north+ / south-
                                if (clat == 'S')
                                    dLat = -dLat;

                                double dSpeed = speed / 1000.0 / 3.6; // m/s ??

                                GPSData a = new GPSData(false);
                                a.Latitude = dLat;
                                a.Longitude = dLon;
                                a.DateTime = timeStamp;
                                a.Speed = (float)dSpeed;

                                AllGPSData.Add(a);
                            }

                            gpsSize += 36;
                        }

                        if (gpsSize != atomSize)
                        {
                            //Trace.WriteLine($"Some data left, {gpsSize} != {atomSize}");
                        }
                    }

                    offset += atomSize;
                    f.Seek(offset, SeekOrigin.Begin);
                }
            }

            StartTime = fTime;
            EndTime = fTime.AddSeconds(duration);
            Duration = (int)Math.Floor(duration);
            ValidGPS = false;
            Distance = 0.0;
            return true;
        }
    }
}