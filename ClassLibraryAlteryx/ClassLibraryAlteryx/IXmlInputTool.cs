namespace ClassLibraryAlteryx
{
    internal interface IXmlInputTool
    {
        AlteryxGuiToolkit.Plugins.IPluginConfiguration GetConfigurationGui();

        AlteryxGuiToolkit.Plugins.EntryPoint GetEngineEntryPoint();

        System.Drawing.Image GetIcon();

        AlteryxGuiToolkit.Plugins.Connection[] GetInputConnections();

        AlteryxGuiToolkit.Plugins.Connection[] GetOutputConnections();
    }
}