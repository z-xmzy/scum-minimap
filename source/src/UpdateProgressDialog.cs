using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ScumMiniMap {
    // Small modal progress window for downloading an installer update; Cancel stops the download.
    internal sealed class UpdateProgressDialog : Form {
        readonly ProgressBar bar;
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
            ClientSize=new Size(440,140);
            var label=new Label { Text=message,AutoSize=false,Location=new Point(16,14),Size=new Size(408,22) };
            bar=new ProgressBar { Location=new Point(16,42),Size=new Size(408,20),Minimum=0,Maximum=1000,Style=ProgressBarStyle.Continuous };
            status=new Label { Text="",AutoSize=false,Location=new Point(16,68),Size=new Size(408,22) };
            var button=new Button { Text=Localization.Get("Cancel"),Location=new Point(334,100),Size=new Size(90,28),DialogResult=DialogResult.Cancel };
            button.Click+=(s,e)=>RequestCancel();
            CancelButton=button;
            Controls.AddRange(new Control[] { label,bar,status,button });
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
