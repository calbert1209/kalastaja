using Spectre.Console;
using StackExchange.Redis;

namespace Kalastaja;
class Program
{
    static void ShowItemTable(List<PlaylistItem> items, string plid)
    {
            var table = new Table();
            table.Title($"[cyan]{plid}[/]");
            table.AddColumn("name").AddColumn("vId");
            foreach (var item in items)
            {
                table.AddRow(item.FileName, item.VideoId);
            }

            AnsiConsole.Write(table);
    }

    static async Task PersistItemsAsync(StatusContext ctx, List<PlaylistItem> items, string plid)
    {
        using (var redis = ConnectionMultiplexer.Connect("localhost"))
        {
            var db = redis.GetDatabase();
            var trans = db.CreateTransaction();

            var playlistVideoIds = new List<string>();

            foreach (var item in items)
            {
                ctx.Status($"preparing to save {item.Position}/{items.Count}...");
                await Task.Delay(500);
                var hashTask = trans.HashSetAsync($"video:{item.VideoId}", new HashEntry[]{
                    new HashEntry("fileName", item.FileName),
                    new HashEntry("plid", plid),
                    new HashEntry("position", item.Position),
                });

                var sortedSetTask = trans.SortedSetAddAsync($"playlist:{plid}:items", item.VideoId, item.Position);

                var trackUpdateTask = trans.ListRightPushAsync($"video:queue", item.VideoId);
            }

            ctx.Status($"saving {items.Count} items...");
            await trans.ExecuteAsync();
        }
    }
    
    static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Must have 2 arguments");
        }

        var (key, plid) = (args[0], args[1]);

        IApiQuery<PlaylistItem> query = new PlaylistItemsQuery(plid, key);
        var items = new List<PlaylistItem>();

        await AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.BouncingBar)
            .StartAsync($"[white]fetching \"{plid}\"...[/]", async ctx =>
            {
                using (var client = new HttpClient())
                {
                    var fetched = await query.FetchItemsAsync(client);
                    items.AddRange(fetched);
                }
                await PersistItemsAsync(ctx, items, plid);
            });
        
        ShowItemTable(items, plid);
    }
}
