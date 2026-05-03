using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HouseHop;

public sealed class HouseHop : IDalamudPlugin
{
    private const string CommandName = "/househop";

    private IDalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }

    public Configuration Configuration { get; init; }
    public HousingStore Store { get; init; }
    public LifestreamIpc Lifestream { get; init; }
    public HouseHopUI UI { get; init; }

    [PluginService] internal static IClientState  ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable  ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework    Framework   { get; private set; } = null!;
    [PluginService] internal static IPluginLog    Log         { get; private set; } = null!;
    [PluginService] internal static IChatGui      ChatGui     { get; private set; } = null!;

    public HouseHop(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        PluginInterface = pluginInterface;
        CommandManager  = commandManager;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        Store      = new HousingStore(Configuration);
        Lifestream = new LifestreamIpc(PluginInterface);
        UI         = new HouseHopUI(this);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open HouseHop — teleport to player houses via Lifestream."
        });

        Framework.Update += OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw        += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi   += OpenMainUi;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        UI.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => UI.Visible = !UI.Visible;
    private void DrawUI()       => UI.Draw();
    private void DrawConfigUI() => UI.OpenSettings();
    private void OpenMainUi()   => UI.Visible = true;

    private void OnFrameworkUpdate(IFramework framework)
    {
        HousingObserver.Observe(Store);
    }
}
