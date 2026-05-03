using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace PensionCompass.Views;

public sealed partial class AboutView : Page
{
    public AboutView()
    {
        InitializeComponent();

        var v = Package.Current.Id.Version;
        VersionText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "버전 {0}.{1}.{2}.{3}",
            v.Major, v.Minor, v.Build, v.Revision);
    }
}
