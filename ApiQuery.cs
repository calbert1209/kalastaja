using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kalastaja;

interface IApiQuery<T>
{
    Task<IList<T>> FetchItemsAsync(HttpClient client);
}

class PlaylistItem
{
    public int Position { get; private set; }
    public string Title { get; private set; }
    public string VideoId { get; private set; }
    private bool _isPlaylistItem;
    public PlaylistItem(int position, string title, string videoId, bool isPlaylistItem)
    {
        Position = position;
        Title = SafeTitle(title);
        VideoId = videoId;
        _isPlaylistItem = isPlaylistItem;
    }

    public string FileName
    {
        get
        {
            return _isPlaylistItem
              ? $"{Position:D2}-{Title}.mp3"
              : $"{Title}.mp3";
        }
    }

    private static string SafeTitle(string input)
    {
        var pattern = "[/\\:*?\"<>]";
        var regex = new Regex(pattern);
        return regex.Replace(input, "");
    }
}

class PlaylistItemsQuery : IApiQuery<PlaylistItem>
{
    private readonly string _playlistId;
    private readonly string _apiKey;

    public PlaylistItemsQuery(string playlistId, string apiKey)
    {
        _playlistId = playlistId;
        _apiKey = apiKey;
    }

    private Uri BuildUri()
    {
        var sb = new StringBuilder();
        sb.Append("https://youtube.googleapis.com/");
        sb.Append("youtube/v3/playlistItems");
        sb.Append("?prettyPrint=false");
        sb.Append($"&key={_apiKey}");
        sb.Append("&part=snippet");
        sb.Append("&fields=items(snippet(title%2Cposition%2CresourceId(videoId)))");
        sb.Append("&maxResults=50");
        sb.Append($"&playlistId={_playlistId}");
        return new Uri(sb.ToString());
    }


    public async Task<IList<PlaylistItem>> FetchItemsAsync(HttpClient client)
    {
        var uri = BuildUri();
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var deserialized = JsonSerializer.Deserialize<ApiPlaylistItemListResponse>(body);
        var output = new List<PlaylistItem>();
        if (deserialized?.items != null)
        {
            foreach (var item in deserialized.items)
            {
                var snippet = item.snippet;
                if (snippet?.title != null && snippet?.resourceId?.videoId != null)
                {
                    output.Add(
                        new PlaylistItem(
                            snippet.position + 1,
                            snippet.title,
                            snippet.resourceId.videoId,
                            true
                        )
                    );
                };

            }
        }
        return output;
    }

    private class ApiPlaylistItem
    {
        public class ResourceId
        {
            public string? videoId { get; set; }
        }

        public class Snippet
        {
            public string? title { get; set; }
            public int position { get; set; }
            public ResourceId? resourceId { get; set; }
        }

        public Snippet? snippet { get; set; }
    }

    private class ApiPlaylistItemListResponse
    {
        public IList<ApiPlaylistItem>? items { get; set; }
    }
}