using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject.ViewModels
{
    public class EditSongViewModel : ViewModelBase
    {
        private readonly SongMetadataRepository _repo;
        private readonly Dictionary<string, SongMetadata> _cache;
        private readonly MusicTrack _track;

        private BitmapImage? _coverImage;
        public BitmapImage? CoverImage
        {
            get => _coverImage;
            set { _coverImage = value; OnPropertyChanged(); }
        }

        public string FilePath => _track.FilePath;

        private string _editedTitle = "";
        public string EditedTitle
        {
            get => _editedTitle;
            set { _editedTitle = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ImagePaths { get; } = new();

        private string? _selectedImagePath;
        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set
            {
                _selectedImagePath = value;
                OnPropertyChanged();
                RemoveImageCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand AddImageCommand { get; }
        public RelayCommand RemoveImageCommand { get; }
        public RelayCommand SaveCommand { get; }

        public EditSongViewModel(
            MusicTrack track,
            SongMetadataRepository repo,
            Dictionary<string, SongMetadata> cache)
        {
            _track = track;
            _repo = repo;
            _cache = cache;

            AddImageCommand = new RelayCommand(AddImage);
            RemoveImageCommand = new RelayCommand(RemoveSelectedImage, () => SelectedImagePath != null);
            SaveCommand = new RelayCommand(Save);

            LoadFromCache(); // NO API here
        }

        private void LoadFromCache()
        {
            _cache.TryGetValue(_track.FilePath, out var meta);

            // title shown in edit box: existing edited title, else api title, else file title
            EditedTitle = meta?.EditedTitle ?? meta?.TrackName ?? _track.Title;

            ImagePaths.Clear();
            foreach (var p in meta?.ImagePaths ?? Enumerable.Empty<string>())
                ImagePaths.Add(p);

            // cover from API base64 if exists
            if (!string.IsNullOrWhiteSpace(meta?.CoverImageBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(meta.CoverImageBase64);
                    using var ms = new MemoryStream(bytes);

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                    CoverImage = bmp;
                }
                catch
                {
                    // bad base64 -> ignore cover, window still opens
                    CoverImage = null;
                }
            }
        }

        private void AddImage()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = true
            };

            if (ofd.ShowDialog() != true)
                return;

            string folder = _repo.GetSongImagesFolder(_track.FilePath);

            foreach (var src in ofd.FileNames)
            {
                string fileName = Path.GetFileName(src);
                string dest = Path.Combine(folder, $"{Guid.NewGuid()}_{fileName}");

                File.Copy(src, dest, overwrite: true);

                ImagePaths.Add(dest);
            }
        }

        private void RemoveSelectedImage()
        {
            if (SelectedImagePath == null)
                return;

            string toRemove = SelectedImagePath;
            ImagePaths.Remove(toRemove);

            // optional: delete file physically
            try
            {
                if (File.Exists(toRemove))
                    File.Delete(toRemove);
            }
            catch { }
        }

        private void Save()
        {
            if (!_cache.TryGetValue(_track.FilePath, out var meta) || meta == null)
            {
                meta = new SongMetadata
                {
                    FilePath = _track.FilePath,
                    TrackName = _track.Title
                };
                _cache[_track.FilePath] = meta;
            }

            meta.EditedTitle = string.IsNullOrWhiteSpace(EditedTitle) ? null : EditedTitle.Trim();
            meta.ImagePaths = ImagePaths.ToList();

            _repo.Save(_cache);
        }
    }
}
