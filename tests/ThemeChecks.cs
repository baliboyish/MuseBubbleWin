using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
namespace MuseBubbleWin {
class ThemeChecks {
 static string dir = Path.Combine(Application.StartupPath,".debug","theme-checks");
 static int checks;
 static void Assert(bool condition,string label){if(!condition)throw new Exception(label);checks++;File.AppendAllText(Path.Combine(dir,"results.txt"),"PASS "+label+Environment.NewLine);}
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static void Click(Button button){typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});}
 static void Capture(Control c,string name){var form=c as Form;if(form!=null){form.Location=new Point(-20000,-20000);form.Show();form.Update();}using(var b=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(b,c.ClientRectangle);b.Save(Path.Combine(dir,name+".png"));}}
 [STAThread] static void Main(){Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"results.txt"),"");try{
 Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
 using(var owner=new BubbleForm(true)){
 var chat=Field<ChatForm>(owner,"chat");var panel=Field<PanelForm>(owner,"panel");
 IntPtr handle=chat.Handle; chat.PerformLayout();
 foreach(bool light in new[]{true,false}){
 owner.SetTheme(light?"light":"dark");
 foreach(float scale in new[]{1f,1.25f,1.5f}){
 // Scale the actual form and child controls, then restore for the next case.
 Size initial=chat.Size; chat.Scale(new SizeF(scale,scale));chat.PerformLayout();
 var bar=Field<AppleTitleBar>(chat,"themeBar");
 Assert(bar.Title.Width>40,"Title has room "+light+" "+scale);
 foreach(var b in new[]{bar.CloseButton,bar.BackButton,bar.FullButton,bar.ThemeButton,bar.PinButton}){
 Assert(b.Left>=0&&b.Right<=bar.Width&&b.Top>=0&&b.Bottom<=bar.Height,"Button within titlebar "+b.AccessibleName+" "+scale);
 Assert(!string.IsNullOrEmpty(b.AccessibleName),"Button accessible name "+b.AccessibleName);
 }
 Assert(bar.CloseButton.Right<=bar.BackButton.Left&&bar.BackButton.Right<=bar.FullButton.Left,"Traffic targets do not overlap "+scale);
 var browser=Field<Microsoft.Web.WebView2.WinForms.WebView2>(chat,"web");
 Assert(browser.Top>=bar.Bottom&&browser.Bottom<=chat.ClientSize.Height,"Browser inside frame "+scale);
 Capture(chat,(light?"light":"dark")+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
 chat.Scale(new SizeF(1/scale,1/scale));chat.Size=initial;
 }
 Capture(panel,light?"panel-light":"panel-dark");
 using(var orb=MuseOfficialIcon.Render(112,light)){Assert(orb.GetPixel(0,0).A==0,"Icon transparent corners "+light);orb.Save(Path.Combine(dir,light?"orb-light.png":"orb-dark.png"));}
 }
 var title=Field<AppleTitleBar>(chat,"themeBar");bool pinned=chat.TopMost;Click(title.PinButton);Assert(chat.TopMost!=pinned,"Pin toggles actual form TopMost");Click(title.PinButton);Assert(chat.TopMost==pinned,"Pin restores actual form TopMost");
 bool before=owner.Light;Click(title.ThemeButton);Assert(owner.Light!=before,"Theme button switches app theme");Assert(title.Palette.Surface==new ApplePalette(owner.Light).Surface,"Theme propagates to titlebar");
 panel.Dispose();chat.Dispose();
 }
 File.AppendAllText(Path.Combine(dir,"results.txt"),"PASS all "+checks+" assertions. Captures show real native controls/loading state; no authenticated web content or upload exercised."+Environment.NewLine);
 }catch(Exception ex){File.AppendAllText(Path.Combine(dir,"results.txt"),"FAIL "+ex);Environment.Exit(1);}}
}
}
