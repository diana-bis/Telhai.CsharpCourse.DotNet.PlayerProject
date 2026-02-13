using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Telhai.DotNet.DianaBistrik.PlayerProject.Models;

namespace Telhai.DotNet.DianaBistrik.PlayerProject.Services
{
    /// <summary>
    /// service calling iTunes search API
    /// </summary>
    public class ItunesService
    {
        // init httpClient with prefix domain
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://itunes.apple.com/")
        };

        public async Task<ItunesTrackInfo?> SearchOneAsync(
            string songTitle,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(songTitle))
                return null;

            // build the rrequest URL
            string encodedTerm = Uri.EscapeDataString(songTitle); // here we'll have what we are searchinf
            string url = $"search?term={encodedTerm}&media=music&limit=1";

            using HttpResponseMessage response =
                await _httpClient.GetAsync(url, cancellationToken);

            response.EnsureSuccessStatusCode();

            // get the response content as JSON string
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            // deserialize the JSON to ItunesSearchResponse object
            var data = JsonSerializer.Deserialize<ItunesSearchResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var item = data?.Results?.FirstOrDefault();
            if (item == null)
                return null;

            return new ItunesTrackInfo
            {
                TrackName = item.TrackName,
                ArtistName = item.ArtistName,
                AlbumName = item.CollectionName,
                ArtworkUrl = item.ArtworkUrl100
            };
        }
    }
}
