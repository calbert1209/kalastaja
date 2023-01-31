using Spectre.Console;

namespace Kalastaja;
class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Must have 2 arguments");
        }

        var (key, plid) = (args[0], args[1]);

        using (var client = new HttpClient())
        {
            IApiQuery<PlaylistItem> query = new PlaylistItemsQuery(plid, key);
            var items = new List<PlaylistItem>();

            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.BouncingBar)
                .StartAsync($"[white]fetching \"{plid}\"...[/]", async ctx =>
                {
                    var fetched = await query.FetchItemsAsync(client);
                    items.AddRange(fetched);
                });
            
            var table = new Table();
            table.Title($"[cyan]{plid}[/]");
            table.AddColumn("name").AddColumn("vId");
            foreach (var item in items)
            {
                table.AddRow(item.FileName, item.VideoId);
            }

            AnsiConsole.Write(table);
        }
    }
}
