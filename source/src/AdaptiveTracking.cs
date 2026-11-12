using System;

namespace ScumMiniMap {
    // Classifies sampling demand, not vehicle occupancy. Speeds are metres/second.
    internal sealed class AdaptiveTrackingCadence {
        int interval=250;
        double lastObservation=double.NaN,slowSince=double.NaN,quietSince=double.NaN;
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        internal int GetIntervalMs(double now) {
            return !Finite(now) || !Finite(lastObservation) || now<lastObservation || now-lastObservation>3 ? 250 : interval;
        }
        internal void Observe(double speed,double filteredSpeed,double headingDelta,double elapsed,double now) {
            bool interrupted=Finite(lastObservation) && (now<=lastObservation || now-lastObservation>3);
            lastObservation=now;
            if(!Finite(now) || !Finite(speed) || !Finite(filteredSpeed) || !Finite(headingDelta) ||
               !Finite(elapsed) || speed<0 || filteredSpeed<0 || elapsed<=0 || elapsed>3 || interrupted) {
                interval=250;slowSince=quietSince=double.NaN;return;
            }
            double turn=Math.Abs(headingDelta);
            if(speed>=7 || filteredSpeed>=7 || turn>=15 || (turn>=5 && turn/elapsed>=30)) {
                interval=250;slowSince=quietSince=double.NaN;return;
            }
            if(speed<=5.5 && filteredSpeed<=5.5) {
                if(double.IsNaN(slowSince))slowSince=now;
                if(interval==250 && now-slowSince>=3)interval=500;
            } else slowSince=double.NaN;
            if(speed<.15 && filteredSpeed<.4 && turn<2) {
                if(double.IsNaN(quietSince))quietSince=now;
                if(now-quietSince>=5)interval=1000;
            } else {
                quietSince=double.NaN;
                if(interval==1000)interval=500;
            }
        }
    }

    public partial class MapWindow {
        readonly AdaptiveTrackingCadence trackingCadence=new AdaptiveTrackingCadence();
        int CurrentTrackingIntervalMs(double nowSec) {
            return Program.TrackingIntervalMs(Math.Max(copyIntervalMs,trackingCadence.GetIntervalMs(nowSec)),scumCopyModifierKey);
        }
        void ObserveTrackingCadence(double speed,double headingDelta,double elapsed,double nowSec) {
            trackingCadence.Observe(speed,filteredSpeed,headingDelta,elapsed,nowSec);
            int interval=CurrentTrackingIntervalMs(nowSec);
            motion.SlowSampling=interval>=500;
            // Manual clipboard updates must not bypass failure backoff or input gates.
            if(pending)next=sent.AddMilliseconds(interval);
        }
    }
}
