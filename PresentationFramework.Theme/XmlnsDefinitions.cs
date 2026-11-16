using System.Windows.Markup;

// Makes the Hack* controls and the Microsoft.Windows.Themes helpers usable from XAML without a
// prefix: they join the same XML namespace as the WPF core controls, exactly like
// System.Windows.Controls.Ribbon does in .NET 4.5+.
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "System.Windows.Controls")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/winfx/2006/xaml/presentation", "Microsoft.Windows.Themes")]
