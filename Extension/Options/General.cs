using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Extension
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
        }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("Debugging")]
        [DisplayName("Detach stops the app")]
        [Description("If this true, then detaching from the app closes the Electron.NET window. If false, nothing will happen.")]
        [DefaultValue(true)]
        public bool DetachStopApp { get; set; } = true;
    }
}
