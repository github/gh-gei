using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoshiftCLI;

internal static class MessageEnhancer
{
    private static readonly Dictionary<string, string> _map = new()
    {
        ["not have the correct permissions to execute"] = "Please check that (a) you are a member of the `{0}` organization, " +
                                                          "(b) you are an organization owner or you have been granted the migrator role and " +
                                                          "(c) your personal access token has the correct scopes. " +
                                                          "For more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer."
    };

    public static bool IsEnhanceable(string message) => GetKeyForMessage(message) is not null;

    public static string Enhance(string message, Func<string, string, string> messageFactory = null)
    {
        messageFactory ??= (originalMessage, improvedMessage) => $"{originalMessage}{improvedMessage}";

        var key = GetKeyForMessage(message);
        return key is null ? message : messageFactory(message, _map[key]);
    }

    private static string GetKeyForMessage(string message) => _map.Keys.SingleOrDefault(key => message.Contains(key, StringComparison.OrdinalIgnoreCase));
}
