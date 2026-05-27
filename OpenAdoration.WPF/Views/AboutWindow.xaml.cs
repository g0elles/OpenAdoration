using System.Reflection;

namespace OpenAdoration.WPF.Views;

public partial class AboutWindow : System.Windows.Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = v != null ? $"Version {v.Major}.{v.Minor}.{v.Build}" : "Version 1.0.0";
    }
}
