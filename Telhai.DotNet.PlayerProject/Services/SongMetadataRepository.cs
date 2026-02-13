using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class SongMetadataRepository
    {
        private const string FILE_NAME = "song_metadata.json";

        public Dictionary<string, SongMetadata> Load()
        {
            if (!File.Exists(FILE_NAME))
                return new Dictionary<string, SongMetadata>();

            string json = File.ReadAllText(FILE_NAME);
            return JsonSerializer.Deserialize<Dictionary<string, SongMetadata>>(json)
                   ?? new Dictionary<string, SongMetadata>();
        }

        public void Save(Dictionary<string, SongMetadata> data)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(FILE_NAME, json);
        }

        public string GetSongImagesFolder(string songFilePath)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string imagesRoot = Path.Combine(baseDir, "song_images");

            Directory.CreateDirectory(imagesRoot);

            // hash to safe folder name
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(songFilePath));
            string folder = Convert.ToHexString(hash);

            string songFolder = Path.Combine(imagesRoot, folder);
            Directory.CreateDirectory(songFolder);

            return songFolder;
        }
    }
}
