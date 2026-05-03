using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HouseHop;

public sealed class HouseHop : IDalamudPlugin
{
    private const string CommandName = "/househop";

    private IDalamudPluginInterface PluginInterface { get; init; }
    public Configuration Configuration { get; init; }
    public HousingStore Store { get; init; }
    public LifestreamIpc Lifestream { get; init; }
    public HouseHopUI UI { get; init; }
    public ContextMenuHook ContextMenu { get; init; }
    public EstateSaver EstateSaver { get; init; }

    [PluginService] internal static IClientState    ClientState    { get; private set; } = null!;
    [PluginService] internal static IObjectTable    ObjectTable    { get; private set; } = null!;
    [PluginService] internal static IFramework      Framework      { get; private set; } = null!;
    [PluginService] internal static IPluginLog      Log            { get; private set; } = null!;
    [PluginService] internal static IChatGui        ChatGui        { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IContextMenu    ContextMenuService { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    public HouseHop(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        PluginInterface = pluginInterface;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        Store        = new HousingStore(Configuration);
        Lifestream   = new LifestreamIpc();
        UI           = new HouseHopUI(this);
        ContextMenu  = new ContextMenuHook(this, ContextMenuService);
        EstateSaver  = new EstateSaver(this, AddonLifecycle);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open HouseHop | 'save' to save last seen estate."
        });

        Framework.Update += OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw        += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        PluginInterface.UiBuilder.OpenMainUi   += OpenMainUi;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        EstateSaver.Dispose();
        ContextMenu.Dispose();
        UI.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().ToLowerInvariant() == "save")
            EstateSaver.ConfirmSave();
        else
            UI.Visible = !UI.Visible;
    }

    private void DrawUI()       => UI.Draw();
    private void DrawConfigUI() => UI.OpenSettings();
    private void OpenMainUi()   => UI.Visible = true;

    private void OnFrameworkUpdate(IFramework framework) => HousingObserver.Observe(Store);
}
