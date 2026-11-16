using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

// Keep file selection in the host when the runtime's native chooser does not appear.
// CDP supplies the exact requesting input; never guess a selector or replace page handlers.
internal sealed class FilePickerBridge
{
    readonly CoreWebView2 core;
    readonly Form owner;
    readonly Action<string> log;
    readonly Func<bool, bool, string[]> choose;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    CoreWebView2DevToolsProtocolEventReceiver receiver;
    bool pending;

    internal FilePickerBridge(CoreWebView2 core, Form owner, Action<string> log,
        Func<bool, bool, string[]> choose = null)
    {
        this.core = core; this.owner = owner; this.log = log;
        this.choose = choose ?? ChooseFiles;
    }

    internal async Task EnableAsync()
    {
        receiver = core.GetDevToolsProtocolEventReceiver("Page.fileChooserOpened");
        receiver.DevToolsProtocolEventReceived += OnFileChooser;
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Page.enable", "{}");
            await core.CallDevToolsProtocolMethodAsync("Page.setInterceptFileChooserDialog", "{\"enabled\":true}");
            log("Host file chooser enabled");
        }
        catch
        {
            receiver.DevToolsProtocolEventReceived -= OnFileChooser;
            throw;
        }
    }

    void OnFileChooser(object sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        string payload = e.ParameterObjectAsJson;
        // Return from the WebView2 callback before starting a modal message loop.
        if (!owner.IsDisposed && owner.IsHandleCreated)
            owner.BeginInvoke(new Action(async delegate { await HandleAsync(payload); }));
    }

    async Task HandleAsync(string payload)
    {
        if (pending || owner.IsDisposed) return;
        pending = true;
        try
        {
            var request = json.Deserialize<Dictionary<string, object>>(payload);
            object node;
            if (!request.TryGetValue("backendNodeId", out node))
                throw new InvalidOperationException("The file chooser did not identify its input.");
            var target = new Dictionary<string, object> { { "backendNodeId", node } };
            var description = json.Deserialize<Dictionary<string, object>>(
                await core.CallDevToolsProtocolMethodAsync("DOM.describeNode", json.Serialize(target)));
            var input = (Dictionary<string, object>)description["node"];
            bool directory = false;
            object attributes;
            if (input.TryGetValue("attributes", out attributes))
            {
                System.Collections.IList pairs = (System.Collections.IList)attributes;
                for (int i = 0; i + 1 < pairs.Count; i += 2)
                    if ((string)pairs[i] == "webkitdirectory") directory = true;
            }
            bool multiple = (string)request["mode"] == "selectMultiple";
            log("File chooser requested; multiple=" + multiple + "; directory=" + directory);
            string[] files = choose(multiple, directory);
            if (files == null) { log("File chooser cancelled"); return; }
            target["files"] = files;
            await core.CallDevToolsProtocolMethodAsync("DOM.setFileInputFiles", json.Serialize(target));
            log("File selection delivered; count=" + files.Length);
        }
        catch (Exception ex)
        {
            log("Host file chooser failed: " + ex);
            MessageBox.Show(owner, "Could not attach the selected file: " + ex.Message,
                "Muse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { pending = false; }
    }

    string[] ChooseFiles(bool multiple, bool directory)
    {
        if (directory)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a folder to attach";
                return dialog.ShowDialog(owner) == DialogResult.OK ? new[] { dialog.SelectedPath } : null;
            }
        }
        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = "Attach files to Muse";
            dialog.Multiselect = multiple;
            dialog.Filter = "All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.RestoreDirectory = true;
            // Use the classic Win32 picker, avoiding the runtime's COM shell dialog path.
            dialog.AutoUpgradeEnabled = false;
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileNames : null;
        }
    }
}
