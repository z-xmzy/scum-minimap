using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Small modal progress window for downloading an installer update; Cancel stops the download.
    internal sealed class UpdateProgressDialog : Form {
        // Themed meter: a hairline track with the accent fill, no native light progress bar.
        sealed class Meter : Control {
            int value;internal int Maximum=1000;
            internal int Value { get { return value; } set { this.value=Math.Max(0,Math.Min(Maximum,value));Invalidate(); } }
            internal Meter() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.Clear(Parent==null?WinTheme.Ground:Parent.BackColor);
                var track=new Rectangle(0,0,Width-1,Height-1);int radius=WinTheme.Caps?0:Height/2;
                WinTheme.FillShape(e.Graphics,track,WinTheme.Raised,WinTheme.Line,radius);
                int fill=(int)Math.Round((Width-1)*(double)value/Math.Max(1,Maximum));
                if(fill>1)WinTheme.FillShape(e.Graphics,new Rectangle(0,0,Math.Max(Height,fill),Height-1),WinTheme.Accent,Color.Transparent,radius);
            }
        }
        readonly Meter bar;
        readonly Label status;
        readonly CancellationTokenSource cancel=new CancellationTokenSource();
        long lastReported=-1;
        internal CancellationToken Token { get { return cancel.Token; } }

        internal UpdateProgressDialog(string title,string message) {
            Text=title;
            FormBorderStyle=FormBorderStyle.FixedDialog;
            MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=true;TopMost=true;
            StartPosition=FormStartPosition.CenterScreen;
            AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(460,168);BackColor=WinTheme.Ground;ForeColor=WinTheme.Ink;Font=WinTheme.Body(9.5f);
            var label=new Label { Text=WinTheme.Upper(message),AutoSize=false,Location=new Point(24,20),Size=new Size(412,28),Font=WinTheme.Display(12f),ForeColor=WinTheme.Ink };
            bar=new Meter { Location=new Point(24,60),Size=new Size(412,8) };
            status=new Label { Text="",AutoSize=false,Location=new Point(24,76),Size=new Size(412,22),ForeColor=WinTheme.Muted,Font=new Font("Consolas",9f) };
            var button=new Button { Text=Localization.Get("Cancel"),Location=new Point(336,120),Size=new Size(100,32),DialogResult=DialogResult.Cancel };
            button.Click+=(s,e)=>RequestCancel();
            CancelButton=button;
            Controls.AddRange(new Control[] { label,bar,status,button });
            WinTheme.Caption(this);WinTheme.Secondary(button);
            FormClosing+=(s,e)=> { if(e.CloseReason==CloseReason.UserClosing)RequestCancel(); };
        }
        void RequestCancel() { if(!cancel.IsCancellationRequested)cancel.Cancel(); }

        // Called from the download thread; marshals at most one UI update per ~0.1% step.
        internal void Report(long done,long total) {
            if(total<=0 || IsDisposed)return;
            long step=done*1000/total;
            if(step==Interlocked.Exchange(ref lastReported,step))return;
            try {
                BeginInvoke(new Action(()=> {
                    if(IsDisposed)return;
                    bar.Value=(int)Math.Max(0,Math.Min(1000,step));
                    status.Text=string.Format("{0:0.0} / {1:0.0} MB  ({2:0.0}%)",done/1048576.0,total/1048576.0,step/10.0);
                }));
            } catch(InvalidOperationException) {}
        }
        protected override void Dispose(bool disposing) { if(disposing)cancel.Dispose(); base.Dispose(disposing); }
    }
}
