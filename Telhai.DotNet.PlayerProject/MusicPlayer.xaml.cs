using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;
using Telhai.DotNet.PlayerProject.ViewModels;
using Telhai.DotNet.PlayerProject.Views;


namespace Telhai.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for MusicPlayer.xaml
    /// </summary>
    public partial class MusicPlayer : Window
    {
        private string? _currentlyPlayingFilePath;
        private readonly SongMetadataRepository _metadataRepo = new SongMetadataRepository();
        private Dictionary<string, SongMetadata> _metadataCache = new();

        // for ItunesService
        private readonly ItunesService _itunesService = new ItunesService();
        private CancellationTokenSource? _cts;

        // Global Variables
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        public string MyProperty { get; set; } = "xxx";

        // for pic slideshow
        private readonly DispatcherTimer _slideshowTimer = new DispatcherTimer();
        private List<string> _slideshowPaths = new();
        private int _slideshowIndex = 0;

        public MusicPlayer()
        {
            // Init all Hardcoded xaml into Element Tree
            InitializeComponent();

            _slideshowTimer.Interval = TimeSpan.FromSeconds(3);
            _slideshowTimer.Tick += (_, __) => AdvanceSlideshow();


            // 1. Setup Timer
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            // LoadLibrary();
            this.Loaded += MusicPlayer_Loaded; // do it as event

            //this.MouseDoubleClick += MusicPlayer_MouseDoubleClick;
            // this.MouseDoubleClick += new MouseButtonEventHandler(MusicPlayer_MouseDoubleClick); - the same

        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            _metadataCache = _metadataRepo.Load();
            this.LoadLibrary();
            ApplyEditedTitlesToLibrary();
            UpdateLibraryUI();


        }
        private void ApplyEditedTitlesToLibrary()
        {
            foreach (var track in library)
            {
                if (_metadataCache.TryGetValue(track.FilePath, out var meta) && meta != null)
                {
                    if (!string.IsNullOrWhiteSpace(meta.EditedTitle))
                        track.Title = meta.EditedTitle!;
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update slider ONLY if music is loaded AND user is NOT holding the handle
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }


        //private void MusicPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //    //MessageBox.Show("double click");
        //    MainWindow p = new MainWindow();
        //    p.Title = "yyy";
        //    p.Show();
        //}

        // --- EMPTY PLACEHOLDERS TO MAKE IT BUILD ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                PlayTrack(track);
                return;
            }

            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            sliderProgress.Value = 0;
            txtStatus.Text = "Stopped";
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }


        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true; // Stop timer updates
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // Resume timer updates
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // File Dialog to choose file from system
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;     // choose multiple files at once
            ofd.Filter = "MP3 Files|*.mp3";

            // User Confirmed
            if (ofd.ShowDialog() == true)
            {
                // itirate all files selected as string
                foreach (string file in ofd.FileNames)  // the string if the file path
                {
                    // create object for each file
                    MusicTrack track = new MusicTrack
                    {
                        // only file name
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        // full path
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        // Helper method to refresh the box
        private void UpdateLibraryUI()
        {
            // Take all library list as source to the listbox
            // display tostring for inner object within list
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary()
        {
            var options = new JsonSerializerOptions {  WriteIndented = true };
            string json = JsonSerializer.Serialize(library, options);
            File.WriteAllText(FILE_NAME, json);
        }

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                // read file
                string json = File.ReadAllText(FILE_NAME);
                //create list of MusicTrack from json
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                // show all loaded Musictrak in listbox
                UpdateLibraryUI();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                if (_currentlyPlayingFilePath == track.FilePath)
                {
                    mediaPlayer.Stop();
                    _currentlyPlayingFilePath = null;

                    ResetPlayerUI();
                    lstLibrary.SelectedItem = null;
                }

                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();

                if (_metadataCache.Remove(track.FilePath))
                    _metadataRepo.Save(_metadataCache);
            }
        }

        private void ResetPlayerUI()
        {
            // stop playback UI
            timer.Stop();
            sliderProgress.Value = 0;

            // clear texts
            txtCurrentSong.Text = "";
            txtStatus.Text = "Stopped";

            TrackNameText.Text = "";
            ArtistNameText.Text = "";
            AlbumNameText.Text = "";

            FilePathText.Text = "";

            // default cover (or empty)
            AlbumImage.Source = null;

            // stop slideshow
            StopSlideshow();
        }

        private void lstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is not MusicTrack track)
            {
                // no selection -> initial empty look
                ResetPlayerUI();
                return;
            }

            txtCurrentSong.Text = track.Title;
            FilePathText.Text = track.FilePath;

            if (_metadataCache.TryGetValue(track.FilePath, out var meta) && meta != null)
                ShowMetadata(meta);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is not MusicTrack track)
                return;

            try
            {
                var win = new EditSongWindow
                {
                    Owner = this,
                    DataContext = new EditSongViewModel(track, _metadataRepo, _metadataCache)
                };

                win.ShowDialog();

                // Refresh UI + list title from JSON (EditedTitle)
                if (_metadataCache.TryGetValue(track.FilePath, out var meta) && meta != null)
                {
                    if (!string.IsNullOrWhiteSpace(meta.EditedTitle))
                        track.Title = meta.EditedTitle!;   // updates list item text

                    UpdateLibraryUI();   // refresh list
                    ShowMetadata(meta);  // refresh "Now Playing"
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Edit window error");
            }
        }



        private void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //if (lstLibrary.SelectedItem is MusicTrack track)
            //{
            //    mediaPlayer.Open(new Uri(track.FilePath));
            //    mediaPlayer.Play();
            //    timer.Start();
            //    txtCurrentSong.Text = track.Title;
            //    txtStatus.Text = "Playing";
            //}
            if (lstLibrary.SelectedItem is MusicTrack track)
                PlayTrack(track);
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWin = new Settings();

            // Listen for the results
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;

            settingsWin.ShowDialog();
        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracksEventData)
        {
            foreach (var track in newTracksEventData)
            {
                // Prevent duplicates based on FilePath
                if (!library.Any(x => x.FilePath == track.FilePath))
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }

        private void PlayTrack(MusicTrack track)
        {

            if (!File.Exists(track.FilePath))
                return;

            _currentlyPlayingFilePath = track.FilePath;

            // 1) מנגן מיד
            mediaPlayer.Open(new Uri(track.FilePath));
            mediaPlayer.Play();
            timer.Start();

            txtCurrentSong.Text = track.Title;
            txtStatus.Text = "Playing";
            FilePathText.Text = track.FilePath;

            if (_metadataCache.TryGetValue(track.FilePath, out var savedMeta) && savedMeta != null)
            {
                ShowMetadata(savedMeta);
                return;
            }

            // 2) ביטול קריאה קודמת
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // 3) ניקוי UI + תמונת ברירת מחדל
            ClearSongInfo(keepFilePath: true);
            txtStatus.Text = "Searching song info...";

            // 4) קריאה אסינכרונית במקביל לניגון (לא await כאן!)
            string query = BuildSearchQuery(track.Title);
            _ = LoadSongInfoAsync(query, _cts.Token, track);
        }

        private static string BuildSearchQuery(string fileNameNoExt)
        {
            var q = fileNameNoExt.Replace("-", " ").Replace("_", " ");
            q = string.Join(" ", q.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return q;
        }

        private void ClearSongInfo(bool keepFilePath)
        {
            TrackNameText.Text = "-";
            ArtistNameText.Text = "-";
            AlbumNameText.Text = "-";

            AlbumImage.Source = new BitmapImage(
        new Uri("pack://application:,,,/Telhai.DotNet.PlayerProject;component/Assets/default_cover.png"));

            if (!keepFilePath)
                FilePathText.Text = "-";
        }

        private async Task LoadSongInfoAsync(string query, CancellationToken token, MusicTrack track)
        {
            try
            {
                // לפי הדוגמה שלך: SearchOneAsync מחזיר ItunesTrackInfo
                ItunesTrackInfo? info = await _itunesService.SearchOneAsync(query, token);

                if (info == null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // דרישה: אם אין מידע/שגיאה לוגית - מציג שם קובץ ללא סיומת + מסלול
                        TrackNameText.Text = track.Title;
                        FilePathText.Text = track.FilePath;
                        txtStatus.Text = "No information found.";
                    });
                    return;
                }

                // 1️⃣ Download artwork as bytes
                byte[]? imageBytes = null;

                if (!string.IsNullOrWhiteSpace(info.ArtworkUrl))
                {
                    using var client = new HttpClient();
                    imageBytes = await client.GetByteArrayAsync(info.ArtworkUrl);
                }

                // 2️⃣ Create metadata object
                var meta = new SongMetadata
                {
                    FilePath = track.FilePath,
                    TrackName = info.TrackName,
                    ArtistName = info.ArtistName,
                    AlbumName = info.AlbumName,
                    CoverImageBase64 = imageBytes != null
                        ? Convert.ToBase64String(imageBytes)
                        : null
                };

                // 3️⃣ Save to cache + JSON
                _metadataCache[track.FilePath] = meta;
                _metadataRepo.Save(_metadataCache);

                // 4️⃣ Update UI on main thread
                Dispatcher.Invoke(() =>
                {
                    ShowMetadata(meta);
                    txtStatus.Text = "Info loaded.";
                });

            }
            catch (OperationCanceledException)
            {
                // שיר הוחלף - תקין
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    // דרישה: בשגיאה להציג שם ללא סיומת + מסלול
                    TrackNameText.Text = track.Title;
                    FilePathText.Text = track.FilePath;
                    txtStatus.Text = "Error loading song info.";
                });
            }
        }

        private void ShowMetadata(SongMetadata meta)
        {
            // prefer edited title everywhere
            string titleToShow = meta.EditedTitle ?? meta.TrackName ?? "-";

            TrackNameText.Text = titleToShow;
            txtCurrentSong.Text = titleToShow;

            ArtistNameText.Text = meta.ArtistName ?? "-";
            AlbumNameText.Text = meta.AlbumName ?? "-";
            txtStatus.Text = "Playing";

            // If user added images -> slideshow
            if (meta.ImagePaths != null && meta.ImagePaths.Count > 0)
            {
                StartSlideshow(meta.ImagePaths);
                return;
            }

            // otherwise stop slideshow and show API cover
            StopSlideshow();

            if (!string.IsNullOrEmpty(meta.CoverImageBase64))
            {
                byte[] bytes = Convert.FromBase64String(meta.CoverImageBase64);
                using var ms = new MemoryStream(bytes);

                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                AlbumImage.Source = bmp;
            }
        }

        private void StartSlideshow(List<string> paths)
        {
            _slideshowPaths = paths.Where(File.Exists).ToList();
            _slideshowIndex = 0;

            if (_slideshowPaths.Count == 0)
            {
                _slideshowTimer.Stop();
                return;
            }

            SetAlbumImageFromPath(_slideshowPaths[_slideshowIndex]);
            _slideshowTimer.Start();
        }

        private void AdvanceSlideshow()
        {
            if (_slideshowPaths.Count == 0)
                return;

            _slideshowIndex = (_slideshowIndex + 1) % _slideshowPaths.Count;
            SetAlbumImageFromPath(_slideshowPaths[_slideshowIndex]);
        }

        private void SetAlbumImageFromPath(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                AlbumImage.Source = bmp;
            }
            catch { }
        }

        private void StopSlideshow()
        {
            _slideshowTimer.Stop();
            _slideshowPaths = new();
        }

    }
}
