using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClassLibraryAlteryx
{
    public class XmlInputTool : AlteryxGuiToolkit.Plugins.IPlugin, ClassLibraryAlteryx.IXmlInputTool
    {

        public AlteryxGuiToolkit.Plugins.IPluginConfiguration GetConfigurationGui()
        {
            // Return a new instance of our GUI control.
            return new XmlInputToolGui();
        }

        public AlteryxGuiToolkit.Plugins.EntryPoint GetEngineEntryPoint()
        {
            return new AlteryxGuiToolkit.Plugins.EntryPoint("CustomDotNetTools.dll",
            "CustomDotNetTools.XmlInputToolEngine", true);
        }

        // For performance reasons, we cache the bitmap.
        private System.Drawing.Bitmap m_icon;
        public System.Drawing.Image GetIcon()
        {
            // NOTE: If you follow this example and include the bitmap in the dll,
            // be sure to specify its "Build Action" as "Embedded Resource", or this
            // code will not be able to locate it!
            if (m_icon == null)
            {
                // Get the assembly we are built into.
                System.IO.Stream s =
                typeof(XmlInputTool).Assembly.GetManifestResourceStream(
                "CustomDotNetTools.XmlInputTool.png");
                // Load the bitmap from the stream.
                m_icon = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(s);
                m_icon.MakeTransparent();
            }
            return m_icon;
        }

        public AlteryxGuiToolkit.Plugins.Connection[] GetInputConnections()
        {
            // Since this example is an input tool and has no input connections,
            // we will return an empty array.
            return new AlteryxGuiToolkit.Plugins.Connection[] { };
        }

        public AlteryxGuiToolkit.Plugins.Connection[] GetOutputConnections()
        {
            // In this example, we only have one output connection called "Output".
            return new AlteryxGuiToolkit.Plugins.Connection[] {
            new AlteryxGuiToolkit.Plugins.Connection("Output")
            };
        }
    }
}
