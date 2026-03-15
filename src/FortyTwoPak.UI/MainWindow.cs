using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace FortyTwoPak.UI;

public class MainWindow : Form
{
    private WebView2 _webView = null!;
    private BridgeService _bridge = null!;

    public MainWindow()
    {
        Text = "42pak-generator";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(900, 600);

        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_webView);

        var env = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "42pak-generator", "WebView2"));

        await _webView.EnsureCoreWebView2Async(env);

        _bridge = new BridgeService(this);
        _webView.CoreWebView2.AddHostObjectToScript("bridge", _bridge);

        // Configure WebView2
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

        // Set virtual host mapping for embedded web resources
        string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "42pak.local", wwwroot,
            CoreWebView2HostResourceAccessKind.Allow);

        _webView.CoreWebView2.Navigate("https://42pak.local/index.html");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _webView?.Dispose();
        base.OnFormClosing(e);
    }
}
