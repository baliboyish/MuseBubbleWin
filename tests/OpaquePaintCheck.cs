using System;using System.IO;using System.Drawing;using System.Reflection;using System.Windows.Forms;
class OpaquePaintCheck {
 [STAThread]static void Main(string[] args){Application.EnableVisualStyles();var assembly=Assembly.Load(File.ReadAllBytes(args[0]));var type=assembly.GetType("MuseBubbleWin.AppleButton");
 using(var button=(Button)Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{"Back to previous app",null},null))
 using(var b=new Bitmap(440,200))using(var g=Graphics.FromImage(b)){
 button.Size=new Size(240,42);g.Clear(Color.Magenta);g.TranslateTransform(80,90);g.SetClip(new Rectangle(0,0,200,42));
 type.GetMethod("OnPaint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,new object[]{new PaintEventArgs(g,new Rectangle(0,0,200,42))});
 g.ResetTransform();g.ResetClip();int outside=0,uncleared=0;
 for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){
 bool inside=x>=80&&x<280&&y>=90&&y<132;bool sentinel=b.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb();
 if(inside&&sentinel)uncleared++;if(!inside&&!sentinel)outside++;}
 var getStyle=typeof(Control).GetMethod("GetStyle",BindingFlags.NonPublic|BindingFlags.Instance);
 bool opaque=(bool)getStyle.Invoke(button,new object[]{ControlStyles.Opaque});bool transparent=(bool)getStyle.Invoke(button,new object[]{ControlStyles.SupportsTransparentBackColor});
 string result=(outside==0&&uncleared==0&&opaque&&!transparent?"PASS":"FAIL")+" opaque repaint: outside="+outside+" uncleared="+uncleared+" opaque="+opaque+" transparent="+transparent;
 File.WriteAllText(Path.Combine(Application.StartupPath,".debug",args[1]+".txt"),result);
 b.Save(Path.Combine(Application.StartupPath,".debug",args[1]+".png"));Environment.ExitCode=result.StartsWith("PASS")?0:1;
 }} }
