// Optional Windows-only layout check. Creates an inert controller, not the timer service.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using WpsClock;
public static class LayoutSmokeTests {
    [STAThread] public static int Main(){
        try{
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var owner=(Controller)FormatterServices.GetUninitializedObject(typeof(Controller));
            owner.Config=new Settings{PanelWidth=240,PanelHeight=100};owner.Config.Normalize();
            typeof(Controller).GetField("windows",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,new List<ClockWindow>());
            using(var form=new SettingsDialog(owner)){
                form.Show();Application.DoEvents();
                TabControl tabs=null;foreach(Control c in form.Controls)if(c is TabControl)tabs=(TabControl)c;
                if(tabs==null||tabs.TabPages.Count!=4)throw new Exception("Missing settings pages.");
                float scale;using(Graphics g=form.CreateGraphics())scale=g.DpiX/96f;
                for(int round=0;round<3;round++)foreach(TabPage page in tabs.TabPages){
                    tabs.SelectedTab=page;Application.DoEvents();
                    if(page.Controls.Count<8)throw new Exception("Missing controls on "+page.Text);
                    foreach(Control c in page.Controls){
                        if(c.Right>620*scale||c.Bottom>690*scale)throw new Exception("Excessive control bounds: "+page.Text+" / "+c.Text+" / "+c.Bounds);
                        var button=c as Button;if(button!=null){Size preferred=button.GetPreferredSize(Size.Empty);if(button.Width<preferred.Width||button.Height<preferred.Height)throw new Exception("Clipped button: "+button.Text);}
                    }
                }
                if(form.AutoScaleMode!=AutoScaleMode.None)throw new Exception("Unexpected automatic scaling.");
                form.Close();
            }
            Console.WriteLine("PASS: four pages populated; control bounds stable across tab switches; button preferred sizes fit.");return 0;
        }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
    }
}
