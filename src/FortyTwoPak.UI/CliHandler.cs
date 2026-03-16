namespace FortyTwoPak.UI;

/// <summary>
/// Thin delegate to the shared CLI handler in FortyTwoPak.Core.
/// The GUI uses this when invoked with command-line arguments.
/// </summary>
static class CliHandler
{
    public static int Run(string[] args) => Core.Cli.CliHandler.Run(args);
}
