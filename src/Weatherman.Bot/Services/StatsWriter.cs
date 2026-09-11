using Discord.Interactions;
using Discord.WebSocket;
using System.Diagnostics;
using System.Text;

namespace Weatherman.Bot.Services;

public class StatsWriter(DiscordSocketClient client, BotConfiguration configuration)
{
    public async Task PostStatsMessageAsync(SocketInteractionContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("```");

        WriteHostMetrics(sb);

        if (context.User.Id == configuration.OwnerId)
        {
            WriteGuildData(sb);
        }

        sb.AppendLine("```");

        await context.Channel.SendMessageAsync(sb.ToString());
    }

    private void WriteHostMetrics(StringBuilder sb)
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

        var memoryUsed = GetHumanReadableMemory(GC.GetTotalMemory(false));
        var memoryAllocated = GetHumanReadableMemory(GC.GetTotalAllocatedBytes(false));

        sb.AppendLine($"Runtime: {Environment.Version}");
        sb.AppendLine($"Uptime: {uptime.TotalHours:N0}:{uptime:mm\\:ss}");
        sb.AppendLine($"Memory used: {memoryUsed} / {memoryAllocated}");
        sb.AppendLine();
        sb.AppendLine($"Connected servers: {client.Guilds.Count()}");
        sb.AppendLine($"Connected users: {client.Guilds.Sum(a => a.MemberCount)}");
    }

    private void WriteGuildData(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("Connected Guilds:");

        var topGuilds = client.Guilds.OrderByDescending(a => a.MemberCount).ToList().Take(3);
        foreach (var guild in topGuilds)
        {
            sb.AppendLine($"{guild.Name}: {guild.MemberCount}");
        }

        sb.AppendLine("----------");

        var newestGuilds = client.Guilds.OrderByDescending(a => a.CurrentUser.JoinedAt).ToList().Take(10);
        foreach (var guild in newestGuilds)
        {
            sb.AppendLine($"{guild.Name}: {guild.MemberCount}");
        }
    }

    private static string GetHumanReadableMemory(long value)
    {
        string[] sizes = { "b", "kb", "mb", "gb", "tb" };
        int order = 0;
        while (value >= 1024 && order < sizes.Length - 1)
        {
            order++;
            value = value / 1024;
        }

        return string.Format("{0:0.##} {1}", value, sizes[order]);
    }
}
