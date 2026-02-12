using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
    }
}
