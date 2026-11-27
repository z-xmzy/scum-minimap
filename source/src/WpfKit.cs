using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ScumMiniMap {
    // Shared building blocks for every secondary window (guides, routes, markers, groups, filters, zones).
    // Each block reads the active theme, so one layout renders as Tactical, Glass or Minimal:
    //   Tactical: condensed caps headings, accent ticks, hairlines, square corners.
    //   Glass:    soft headings, sections on raised rounded cards, pill segments.
    //   Minimal:  quiet type, hairline-separated sections, compact controls.
    internal static class WpfKit {
        static WpfThemeSpec T { get { return WpfFieldTheme.Current; } }

        // ── Type ──────────────────────────────────────────────────────────
        internal static TextBlock Title(string text,double size=26) {
            var t=T;var block=WpfFieldTheme.Text(t.Caps(text),t.Uppercase?size+6:t.Tabs==WpfTabStyle.Rail?size-4:size-2,WpfFieldTheme.Ink,true);
            block.TextWrapping=TextWrapping.Wrap;block.TextTrimming=TextTrimming.None;
            if(t.Uppercase) { block.FontFamily=WpfFieldTheme.DisplayFont;block.FontWeight=FontWeights.Bold; }
            return block;
        }
        internal static TextBlock Help(string text,double size=13) {
            var block=WpfFieldTheme.Text(text,size,WpfFieldTheme.Muted);block.TextWrapping=TextWrapping.Wrap;block.TextTrimming=TextTrimming.None;block.LineHeight=size*1.45;return block;
        }
        internal static TextBlock Label(string text) {
            var t=T;var block=WpfFieldTheme.Text(t.Caps(text),t.Uppercase?13:12,WpfFieldTheme.Muted,true);block.TextWrapping=TextWrapping.Wrap;block.TextTrimming=TextTrimming.None;
            if(t.Uppercase) { block.FontFamily=WpfFieldTheme.DisplayFont;block.FontWeight=FontWeights.Bold; }
            block.Margin=new Thickness(0,0,0,6);return block;
        }
        // Legacy labels carry ASCII arrows and shouting caps ("NEXT >", "< PREVIOUS"): strip the arrows and,
        // outside the caps theme, turn all-caps text into sentence case.
        internal static string Plain(string text) {
            string value=(text??"").Replace("<","").Replace(">","").Trim();
            if(T.Uppercase||value.Length<2||!value.Any(char.IsLetter)||value!=value.ToUpperInvariant()||value==value.ToLowerInvariant())return value;
            return value.Substring(0,1)+value.Substring(1).ToLowerInvariant();
        }
        internal static TextBlock Strong(string text,double size=15) { var block=WpfFieldTheme.Text(text,size,WpfFieldTheme.Ink,true);block.TextWrapping=TextWrapping.Wrap;block.TextTrimming=TextTrimming.None;return block; }
        internal static TextBlock Mono(string text,double size=13,Brush ink=null) { var block=WpfFieldTheme.Text(text,size,ink??WpfFieldTheme.Ink);block.FontFamily=WpfFieldTheme.MonoFont;return block; }

        // ── Buttons ───────────────────────────────────────────────────────
        // Content stays the plain label (screen readers, language refresh, tests find buttons by text);
        // the caps theme shows it in capitals through the template.
        internal sealed class CapsConverter : System.Windows.Data.IValueConverter {
            internal static readonly CapsConverter Instance=new CapsConverter();
            public object Convert(object value,Type targetType,object parameter,CultureInfo culture) { var text=value as string;return text==null?value:text.ToUpper(CultureInfo.CurrentCulture); }
            public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture) { return value; }
        }
        internal static void CapsLabel(ContentControl control) {
            var label=new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding { Converter=CapsConverter.Instance });
            label.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            control.ContentTemplate=new DataTemplate { VisualTree=label };
        }
        static Button Base(string label,Action action,string icon) {
            var button=WpfFieldTheme.Button(label,action);button.Margin=new Thickness(0);button.MinHeight=36;button.Padding=new Thickness(16,6,16,6);button.HorizontalAlignment=HorizontalAlignment.Left;
            if(T.Uppercase) { button.FontFamily=WpfFieldTheme.DisplayFont;button.FontWeight=FontWeights.Bold;button.FontSize=15; }
            if(icon!=null)WpfFieldTheme.IconLabel(button,icon,false,15,T.Uppercase);else if(T.Uppercase)CapsLabel(button);
            return button;
        }
        internal static Button Primary(string label,Action action,string icon=null) {
            var button=Base(label,action,icon);button.Background=WpfFieldTheme.AccentFill;button.Foreground=WpfFieldTheme.AccentInk;button.BorderBrush=Brushes.Transparent;button.MinWidth=112;return button;
        }
        internal static Button Secondary(string label,Action action,string icon=null) {
            var button=Base(label,action,icon);button.Background=WpfFieldTheme.Raised;button.Foreground=WpfFieldTheme.Ink;button.BorderBrush=WpfFieldTheme.Line2;return button;
        }
        internal static Button Quiet(string label,Action action,string icon=null) {
            var button=Base(label,action,icon);button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");button.Foreground=WpfFieldTheme.Muted;button.Padding=new Thickness(10,6,10,6);
            if(T.Uppercase) { button.FontFamily=WpfFieldTheme.DisplayFont;button.FontWeight=FontWeights.Bold;button.FontSize=14; } else button.FontSize=14;
            return button;
        }
        internal static Button Danger(string label,Action action,string icon=null) {
            var t=T;var button=Base(label,action,icon);button.Foreground=WpfFieldTheme.Warn;button.Background=t.Alpha(t.Danger,0x1C);button.BorderBrush=t.Alpha(t.Danger,0x59);return button;
        }
        // Wide primary action that spans its column (start route, send, import).
        internal static Button Wide(Button button) { button.HorizontalAlignment=HorizontalAlignment.Stretch;return button; }
        internal static Button Tagged(Button button,string tag) { button.Tag=tag;return button; }
        // Buttons laid out with consistent gaps; wraps when the window narrows.
        internal static WrapPanel Actions(params UIElement[] items) {
            var row=new WrapPanel { Margin=new Thickness(0,4,0,4) };
            foreach(var item in items) { var element=item as FrameworkElement;if(element!=null)element.Margin=new Thickness(0,0,8,8);row.Children.Add(item); }
            return row;
        }

        // ── Structure ─────────────────────────────────────────────────────
        // A titled group of controls. Tactical: tick + caps title + rule. Glass: card. Minimal: hairline group.
        internal static FrameworkElement Section(string title,UIElement content,string note=null,UIElement trailing=null,bool framed=true) {
            var t=T;var root=new StackPanel { Margin=new Thickness(0,t.Uppercase?18:t.GradientAccent?14:20,0,4) };
            var head=new DockPanel { Margin=new Thickness(0,0,0,t.GradientAccent?8:10) };
            if(trailing!=null) { DockPanel.SetDock(trailing,Dock.Right);head.Children.Add(trailing); }
            if(title!=null) {
                if(t.Uppercase) {
                    var tick=new Border { Width=3,Height=14,Background=WpfFieldTheme.Accent,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center };DockPanel.SetDock(tick,Dock.Left);head.Children.Add(tick);
                    var label=WpfFieldTheme.Text(t.Caps(title),16,WpfFieldTheme.Ink,true);label.FontFamily=WpfFieldTheme.DisplayFont;label.FontWeight=FontWeights.Bold;label.TextTrimming=TextTrimming.None;DockPanel.SetDock(label,Dock.Left);head.Children.Add(label);
                    head.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(12,0,8,0),VerticalAlignment=VerticalAlignment.Center });
                } else {
                    var label=WpfFieldTheme.Text(title,t.GradientAccent?13:15,t.GradientAccent?WpfFieldTheme.Muted:WpfFieldTheme.Ink,true);label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.None;head.Children.Add(label);
                }
                root.Children.Add(head);
            }
            var inner=new StackPanel();
            if(note!=null) { var help=Help(note);help.Margin=new Thickness(0,0,0,10);inner.Children.Add(help); }
            inner.Children.Add(content);
            if(t.GradientAccent&&!framed)root.Children.Add(inner);
            else if(t.GradientAccent)root.Children.Add(new Border { Child=inner,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(16,t.PanelRadius)),Padding=new Thickness(18,14,18,10) });
            else if(t.Uppercase) { inner.Margin=new Thickness(12,0,0,0);root.Children.Add(inner); }
            else { root.Children.Insert(0,new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(0,0,0,14) });root.Children.Add(inner); }
            return root;
        }
        // Label above its input, optional help below.
        internal static FrameworkElement Field(string label,UIElement input,string help=null) {
            var root=new StackPanel { Margin=new Thickness(0,4,0,12) };
            if(label!=null)root.Children.Add(Label(label));
            root.Children.Add(input);
            if(help!=null) { var note=Help(help,12);note.Margin=new Thickness(0,6,0,0);root.Children.Add(note); }
            return root;
        }
        // Inline callout with a leading icon. Tones: info, warn, ok.
        internal static Border Note(string text,string tone="info") {
            var t=T;string colour=tone=="warn"?t.Warning:tone=="ok"?t.Success:t.Info;var ink=t.Brush(colour);
            var row=new DockPanel();var icon=WpfIcons.Create(tone=="warn"?"triangle-alert":tone=="ok"?"circle-check":"info",16,ink);icon.Margin=new Thickness(0,1,10,0);icon.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(icon,Dock.Left);row.Children.Add(icon);
            var body=WpfFieldTheme.Text(text,13,WpfFieldTheme.Ink);body.TextWrapping=TextWrapping.Wrap;body.TextTrimming=TextTrimming.None;row.Children.Add(body);
            return new Border { Child=row,Background=t.Alpha(colour,0x14),BorderBrush=t.Alpha(colour,0x4D),BorderThickness=t.Uppercase?new Thickness(3,0,0,0):new Thickness(1),CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(10,t.ControlRadius)),Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,4,0,10) };
        }
        internal static FrameworkElement Empty(string text,string icon="inbox") {
            var root=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,18,0,18) };
            var glyph=WpfIcons.Create(icon,26,WpfFieldTheme.Dim);glyph.HorizontalAlignment=HorizontalAlignment.Center;glyph.Margin=new Thickness(0,0,0,8);root.Children.Add(glyph);
            var label=Help(text,13);label.TextAlignment=TextAlignment.Center;label.MaxWidth=360;root.Children.Add(label);return root;
        }
        internal static Border Badge(string text,Brush ink=null) {
            var t=T;var label=WpfFieldTheme.Text(t.Caps(text),11,ink??WpfFieldTheme.Accent,true);label.TextTrimming=TextTrimming.None;
            if(t.Uppercase) { label.FontFamily=WpfFieldTheme.DisplayFont;label.FontSize=12;label.FontWeight=FontWeights.Bold; }
            var brush=ink as SolidColorBrush;var fill=brush==null?WpfFieldTheme.AccentDim:new SolidColorBrush(Color.FromArgb(0x24,brush.Color.R,brush.Color.G,brush.Color.B));
            return new Border { Child=label,Background=fill,CornerRadius=new CornerRadius(Math.Min(10,t.ChipRadius)),Padding=new Thickness(7,1,7,2),VerticalAlignment=VerticalAlignment.Center };
        }
        // One row of a list: optional leading element, title + meta text, trailing controls; hairline below.
        internal static DockPanel Row(UIElement lead,string title,string meta,params UIElement[] trailing) {
            var row=new DockPanel { Margin=new Thickness(0),LastChildFill=true };
            var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            foreach(var item in trailing.Where(i=>i!=null)) { var element=item as FrameworkElement;if(element!=null)element.Margin=new Thickness(6,0,0,0);actions.Children.Add(item); }
            DockPanel.SetDock(actions,Dock.Right);row.Children.Add(actions);
            if(lead!=null) { var element=lead as FrameworkElement;if(element!=null) { element.Margin=new Thickness(0,0,12,0);element.VerticalAlignment=VerticalAlignment.Center; }DockPanel.SetDock(lead,Dock.Left);row.Children.Add(lead); }
            var text=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
            if(title!=null) { var name=WpfFieldTheme.Text(title,14,WpfFieldTheme.Ink,true);name.TextWrapping=TextWrapping.Wrap;name.TextTrimming=TextTrimming.None;text.Children.Add(name); }
            if(!string.IsNullOrEmpty(meta)) { var info=WpfFieldTheme.Text(meta,12,WpfFieldTheme.Muted);info.TextWrapping=TextWrapping.Wrap;info.TextTrimming=TextTrimming.None;info.Margin=new Thickness(0,2,0,0);text.Children.Add(info); }
            row.Children.Add(text);return row;
        }
        // Rows stacked with hairlines between them inside one frame.
        internal static Border List(IEnumerable<UIElement> rows) {
            var t=T;var stack=new StackPanel();int index=0;
            foreach(var row in rows) {
                stack.Children.Add(new Border { Child=row,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,index==0?0:1,0,0),Padding=new Thickness(14,10,10,10) });index++;
            }
            return new Border { Child=stack,Background=t.GradientAccent?Brushes.Transparent:WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(t.GradientAccent?0:1),CornerRadius=new CornerRadius(Math.Min(12,t.ControlRadius)),Margin=new Thickness(0,2,0,8) };
        }
        internal static Border IconTile(string icon,Brush ink=null,double size=34) {
            var t=T;var colour=ink??WpfFieldTheme.Accent;var glyph=WpfIcons.Create(icon,size*.5,colour);glyph.HorizontalAlignment=HorizontalAlignment.Center;glyph.VerticalAlignment=VerticalAlignment.Center;
            var brush=colour as SolidColorBrush;var fill=brush==null?WpfFieldTheme.AccentDim:new SolidColorBrush(Color.FromArgb(0x26,brush.Color.R,brush.Color.G,brush.Color.B));
            return new Border { Width=size,Height=size,Background=fill,CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(size/2,t.GradientAccent?10:8)),Child=glyph };
        }

        // Thin progress meter: two star columns, so it needs no measuring and resizes with its container.
        internal static Grid Meter(int value,int total,double height=4) {
            var t=T;var meter=new Grid { Height=height,VerticalAlignment=VerticalAlignment.Center };
            meter.ColumnDefinitions.Add(new ColumnDefinition());meter.ColumnDefinitions.Add(new ColumnDefinition());
            var radius=new CornerRadius(t.Uppercase?0:height/2);
            meter.Children.Add(new Border { Background=WpfFieldTheme.Line,CornerRadius=radius });Grid.SetColumnSpan(meter.Children[0],2);
            var fill=new Border { Background=WpfFieldTheme.AccentFill,CornerRadius=radius };meter.Children.Add(fill);
            SetMeter(meter,value,total);return meter;
        }
        internal static void SetMeter(Grid meter,int value,int total) {
            double done=Math.Max(0,Math.Min(total,value)),left=Math.Max(0,total-done);
            meter.ColumnDefinitions[0].Width=new GridLength(done,GridUnitType.Star);meter.ColumnDefinitions[1].Width=new GridLength(left,GridUnitType.Star);
            meter.Children[1].Visibility=done>0?Visibility.Visible:Visibility.Collapsed;
        }
        // Keyboard key face (current bindings, shortcuts).
        internal static Border KeyCap(string text,string tag=null) {
            var t=T;var label=Mono(text,13,WpfFieldTheme.Ink);label.Tag=tag;label.FontWeight=FontWeights.SemiBold;label.TextTrimming=TextTrimming.None;
            return new Border { Child=label,Background=WpfFieldTheme.Current.Brush(WpfFieldTheme.Current.Input),BorderBrush=WpfFieldTheme.Line2,BorderThickness=new Thickness(1,1,1,2),CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(6,t.ControlRadius)),Padding=new Thickness(9,3,9,3),VerticalAlignment=VerticalAlignment.Center };
        }
        // Menu path shown as chips: OPTIONS › CONTROLS › … The highlighted steps take the accent.
        internal static WrapPanel Breadcrumb(IList<string> steps,params int[] highlight) {
            var t=T;var row=new WrapPanel();
            for(int i=0;i<steps.Count;i++) {
                bool on=highlight.Contains(i);var label=Mono(steps[i],12,on?WpfFieldTheme.Accent:WpfFieldTheme.Ink);label.TextWrapping=TextWrapping.Wrap;label.TextTrimming=TextTrimming.None;
                row.Children.Add(new Border { Child=label,Background=on?WpfFieldTheme.AccentDim:WpfFieldTheme.Current.Brush(WpfFieldTheme.Current.Input),BorderBrush=on?t.Alpha(t.Accent,0x66):WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(t.Uppercase?0:Math.Min(6,t.ControlRadius)),Padding=new Thickness(7,2,7,3),Margin=new Thickness(0,0,0,6),VerticalAlignment=VerticalAlignment.Center });
                if(i<steps.Count-1) { var arrow=WpfIcons.Create("chevron-right",12,WpfFieldTheme.Dim);arrow.Margin=new Thickness(4,0,4,6);row.Children.Add(arrow); }
            }
            return row;
        }

        // ── Inputs ────────────────────────────────────────────────────────
        internal static CheckBox Check(object content,bool? value,string tag=null) {
            var box=new CheckBox { Content=content,IsChecked=value,Tag=tag,Margin=new Thickness(0,2,0,2) };box.SetResourceReference(FrameworkElement.StyleProperty,"Check");return box;
        }
        internal static CheckBox CheckCard(object content,bool? value,string tag=null) {
            var box=new CheckBox { Content=content,IsChecked=value,Tag=tag,Margin=new Thickness(0,0,0,8) };box.SetResourceReference(FrameworkElement.StyleProperty,"CheckCard");return box;
        }
        internal static RadioButton ChoiceCard(object content,bool value,string group,string tag=null) {
            var choice=new RadioButton { Content=content,IsChecked=value,GroupName=group,Tag=tag,Margin=new Thickness(0,0,0,8) };choice.SetResourceReference(FrameworkElement.StyleProperty,"ChoiceCard");return choice;
        }
        // Checklist card: tick box and text, with detail controls below that stay usable while the tick is off
        // or disabled (key capture, menu paths). Checked cards take an accent frame (and bar in the caps theme).
        internal static Border CheckItem(CheckBox check,string number,params UIElement[] details) {
            var t=T;check.SetResourceReference(FrameworkElement.StyleProperty,"Check");check.VerticalContentAlignment=VerticalAlignment.Top;check.HorizontalContentAlignment=HorizontalAlignment.Stretch;check.Margin=new Thickness(0);
            var text=check.Content as TextBlock;if(text!=null) { text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;text.LineHeight=text.FontSize*1.4;text.Margin=new Thickness(0,-1,0,0); }
            var stack=new StackPanel();stack.Children.Add(check);
            var extra=new StackPanel { Margin=new Thickness(28,12,0,0) };foreach(var detail in details.Where(d=>d!=null))extra.Children.Add(detail);if(extra.Children.Count>0)stack.Children.Add(extra);
            var body=new DockPanel();TextBlock index=null;
            if(number!=null) { index=Mono(number,12,WpfFieldTheme.Dim);index.Width=30;index.VerticalAlignment=VerticalAlignment.Top;index.Margin=new Thickness(0,3,0,0);DockPanel.SetDock(index,Dock.Left);body.Children.Add(index); }
            body.Children.Add(stack);
            var bar=new Border { Width=3,HorizontalAlignment=HorizontalAlignment.Left,Background=WpfFieldTheme.Accent,Visibility=Visibility.Collapsed };
            var layer=new Grid();layer.Children.Add(new Border { Child=body,Padding=new Thickness(16,14,16,14) });layer.Children.Add(bar);
            var card=new Border { Child=layer,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(12,t.ControlRadius)),Margin=new Thickness(0,0,0,8),SnapsToDevicePixels=true };
            Action sync=()=> {
                bool on=check.IsChecked==true;card.BorderBrush=on?t.Alpha(t.Accent,0x80):WpfFieldTheme.Line;
                bar.Visibility=on&&t.Chamfer>0?Visibility.Visible:Visibility.Collapsed;if(index!=null)index.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Dim;
            };
            check.Checked+=(s,e)=>sync();check.Unchecked+=(s,e)=>sync();check.MouseEnter+=(s,e)=> { if(check.IsChecked!=true)card.BorderBrush=WpfFieldTheme.Line2; };check.MouseLeave+=(s,e)=>sync();sync();
            return card;
        }
        // Numbered step in a vertical stepper (setup rail): marker shows the number, a tick when done.
        internal static Button Step(int number,string label,bool current,bool done,Action action) {
            var t=T;double size=t.Tabs==WpfTabStyle.Rail?22:26;
            var marker=new Border { Width=size,Height=size,CornerRadius=new CornerRadius(t.Uppercase?0:size/2),BorderThickness=new Thickness(current||done?0:1.5),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0) };
            if(current) { marker.Background=WpfFieldTheme.AccentFill; } else if(done) { marker.Background=WpfFieldTheme.AccentDim; } else { marker.Background=Brushes.Transparent;marker.BorderBrush=WpfFieldTheme.Line2; }
            if(done&&!current) { var tick=WpfIcons.Create("check",size*.55,WpfFieldTheme.Accent,2);marker.Child=tick; }
            else { var digit=Mono(number.ToString(CultureInfo.InvariantCulture),size*.48,current?WpfFieldTheme.AccentInk:WpfFieldTheme.Muted);digit.FontWeight=FontWeights.Bold;digit.HorizontalAlignment=HorizontalAlignment.Center;digit.TextTrimming=TextTrimming.None;marker.Child=digit; }
            var text=WpfFieldTheme.Text(t.Caps(label),t.Uppercase?16:14,current||done?WpfFieldTheme.Ink:WpfFieldTheme.Muted,current);text.TextWrapping=TextWrapping.Wrap;text.TextTrimming=TextTrimming.None;
            if(t.Uppercase) { text.FontFamily=WpfFieldTheme.DisplayFont;text.FontWeight=current?FontWeights.Bold:FontWeights.SemiBold; }
            var row=new DockPanel();DockPanel.SetDock(marker,Dock.Left);row.Children.Add(marker);row.Children.Add(text);
            var button=new Button { Content=row,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,0,2) };
            button.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");
            if(current) {
                button.Background=t.Tabs==WpfTabStyle.Strip?WpfFieldTheme.AccentDim:t.Tabs==WpfTabStyle.Segmented?WpfFieldTheme.Raised:WpfFieldTheme.Hover;
                if(t.Uppercase) { button.BorderBrush=WpfFieldTheme.Accent;button.BorderThickness=new Thickness(3,0,0,0);button.Padding=new Thickness(7,7,10,7); }
            }
            button.Click+=(s,e)=>action();System.Windows.Automation.AutomationProperties.SetName(button,label);return button;
        }
        // Segmented tab bar. Strip: underlined caps tabs. Segmented: pills on a track. Rail: quiet chips.
        internal static ListBox Tabs(IList<string> labels,int selected,Action<int> select,string tagPrefix=null) {
            var t=T;var bar=new ListBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),Focusable=false };
            bar.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
            ScrollViewer.SetHorizontalScrollBarVisibility(bar,ScrollBarVisibility.Disabled);
            for(int i=0;i<labels.Count;i++) {
                var text=WpfFieldTheme.Text(t.Caps(labels[i]),t.Uppercase?16:14,null,t.Uppercase||t.GradientAccent);text.TextTrimming=TextTrimming.None;
                text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ListBoxItem),1) });
                if(t.Uppercase) { text.FontFamily=WpfFieldTheme.DisplayFont;text.FontWeight=FontWeights.Bold; }
                var item=new ListBoxItem { Content=text,Margin=new Thickness(0,0,t.Tabs==WpfTabStyle.Segmented?2:4,0),Tag=tagPrefix==null?null:tagPrefix+i };
                item.SetResourceReference(FrameworkElement.StyleProperty,"Segment");bar.Items.Add(item);
            }
            bar.SelectedIndex=Math.Max(0,Math.Min(labels.Count-1,selected));
            bar.SelectionChanged+=(s,e)=> { if(bar.SelectedIndex>=0)select(bar.SelectedIndex); };
            return bar;
        }
        // Tab bar of buttons, for pages that shortcuts and tests switch by Click. Call MarkTabs after each change.
        internal static WrapPanel ButtonTabs(IList<string> labels,IList<string> icons,Action<int> select,string tagPrefix) {
            var t=T;var bar=new WrapPanel();
            for(int i=0;i<labels.Count;i++) {
                int index=i;var tab=new Button { Content=labels[i],Tag=tagPrefix+i,Padding=t.Tabs==WpfTabStyle.Strip?new Thickness(14,9,16,8):t.Tabs==WpfTabStyle.Segmented?new Thickness(16,7,16,7):new Thickness(12,7,12,7),Margin=new Thickness(0,0,t.Tabs==WpfTabStyle.Segmented?2:4,0),MinHeight=34,Focusable=true };
                tab.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");tab.FontSize=t.Uppercase?16:14;
                if(t.Uppercase) { tab.FontFamily=WpfFieldTheme.DisplayFont;tab.FontWeight=FontWeights.Bold; }
                if(icons!=null&&i<icons.Count&&icons[i]!=null)WpfFieldTheme.IconLabel(tab,icons[i],false,15,t.Uppercase);else if(t.Uppercase)CapsLabel(tab);
                tab.Click+=(s,e)=>select(index);bar.Children.Add(tab);
            }
            return bar;
        }
        internal static void MarkTabs(Panel bar,int selected) {
            var t=T;
            for(int i=0;i<bar.Children.Count;i++) {
                var tab=bar.Children[i] as Button;if(tab==null)continue;bool on=i==selected;
                if(t.Tabs==WpfTabStyle.Strip) { tab.Background=on?WpfFieldTheme.AccentDim:Brushes.Transparent;tab.BorderBrush=on?WpfFieldTheme.Accent:Brushes.Transparent;tab.BorderThickness=new Thickness(0,0,0,3);tab.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Muted; }
                else if(t.Tabs==WpfTabStyle.Segmented) { tab.Background=on?WpfFieldTheme.AccentFill:Brushes.Transparent;tab.BorderThickness=new Thickness(0);tab.Foreground=on?WpfFieldTheme.AccentInk:WpfFieldTheme.Muted;tab.FontWeight=on?FontWeights.SemiBold:FontWeights.Medium; }
                else { tab.Background=on?WpfFieldTheme.Hover:Brushes.Transparent;tab.BorderThickness=new Thickness(0);tab.Foreground=on?WpfFieldTheme.Ink:WpfFieldTheme.Muted;tab.FontWeight=on?FontWeights.SemiBold:FontWeights.Normal; }
            }
        }
        // Selectable option chip (route modes, route kinds, quick names). MarkChip shows the chosen one.
        internal static Button Chip(string label,Action action,string icon=null,string tag=null) {
            var t=T;var chip=new Button { Content=label,Tag=tag,Padding=new Thickness(12,5,12,6),MinHeight=32,FontSize=13,Margin=new Thickness(0,0,6,6),BorderThickness=new Thickness(1) };
            chip.SetResourceReference(FrameworkElement.StyleProperty,"Ghost");
            if(t.Uppercase) { chip.FontFamily=WpfFieldTheme.DisplayFont;chip.FontWeight=FontWeights.Bold;chip.FontSize=14; }
            if(icon!=null)WpfFieldTheme.IconLabel(chip,icon,false,14,t.Uppercase);else if(t.Uppercase)CapsLabel(chip);
            chip.Click+=(s,e)=> { if(chip.IsEnabled)action(); };MarkChip(chip,false);return chip;
        }
        internal static void MarkChip(Button chip,bool on) {
            chip.Background=on?WpfFieldTheme.AccentDim:WpfFieldTheme.Raised;chip.BorderBrush=on?WpfFieldTheme.Accent:WpfFieldTheme.Line;chip.Foreground=on?WpfFieldTheme.Accent:WpfFieldTheme.Ink;
        }
        // Round (square in the caps theme) step number for route stops.
        internal static Border Number(int value,bool on=false) {
            var t=T;var digit=Mono(value.ToString(CultureInfo.InvariantCulture),12,on?WpfFieldTheme.AccentInk:WpfFieldTheme.Accent);digit.FontWeight=FontWeights.Bold;digit.HorizontalAlignment=HorizontalAlignment.Center;digit.TextTrimming=TextTrimming.None;
            return new Border { Width=24,Height=24,CornerRadius=new CornerRadius(t.Uppercase?0:12),Background=on?WpfFieldTheme.AccentFill:WpfFieldTheme.AccentDim,Child=digit };
        }
        // Raised panel for a group of controls that is not a titled section (hero cards, summaries).
        internal static Border Card(UIElement content,Thickness? padding=null) {
            var t=T;return new Border { Child=content,Background=WpfFieldTheme.Raised,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(Math.Min(12,t.ControlRadius)),Padding=padding??new Thickness(18,16,18,16),Margin=new Thickness(0,4,0,8) };
        }
        // The track/underline the tab bar sits on.
        internal static FrameworkElement TabTrack(FrameworkElement tabs) {
            var t=T;
            if(t.Tabs==WpfTabStyle.Segmented)return new Border { Child=tabs,Background=WpfFieldTheme.Raised,CornerRadius=new CornerRadius(13),Padding=new Thickness(3),HorizontalAlignment=HorizontalAlignment.Left };
            if(t.Tabs==WpfTabStyle.Strip)return new Border { Child=tabs,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1) };
            return new Border { Child=tabs,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,0,0,8) };
        }
        // A colour swatch. Content keeps its "✓ #RRGGBB" text for automation; the template shows a ring and tick instead.
        sealed class TickVisibility:System.Windows.Data.IValueConverter {
            internal static readonly TickVisibility Instance=new TickVisibility();
            public object Convert(object value,Type type,object parameter,CultureInfo culture) { var text=value as string;return text!=null&&text.StartsWith("\u2713")?Visibility.Visible:Visibility.Collapsed; }
            public object ConvertBack(object value,Type type,object parameter,CultureInfo culture) { throw new NotSupportedException(); }
        }
        internal static Button Swatch(string content,Brush fill,Brush tick,Action action) {
            var t=T;double size=38,radius=t.Uppercase?2:size/2;
            var button=new Button { Content=content,Background=fill,Foreground=tick,Width=size,Height=size,Margin=new Thickness(0,0,6,6),Cursor=Cursors.Hand,FocusVisualStyle=null };button.Click+=(s,e)=>action();
            var self=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent);
            var root=new FrameworkElementFactory(typeof(Grid));root.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
            var ring=new FrameworkElementFactory(typeof(Border));ring.Name="ring";ring.SetValue(Border.BorderBrushProperty,WpfFieldTheme.Ink);ring.SetValue(Border.BorderThicknessProperty,new Thickness(2));ring.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));
            ring.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("Content") { RelativeSource=self,Converter=TickVisibility.Instance });root.AppendChild(ring);
            var dot=new FrameworkElementFactory(typeof(Border));dot.Name="dot";dot.SetValue(FrameworkElement.MarginProperty,new Thickness(5));dot.SetValue(Border.CornerRadiusProperty,new CornerRadius(t.Uppercase?1:(size-10)/2));
            dot.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=self });dot.SetValue(Border.BorderBrushProperty,WpfFieldTheme.Line2);dot.SetValue(Border.BorderThicknessProperty,new Thickness(1));root.AppendChild(dot);
            var mark=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));mark.SetValue(System.Windows.Shapes.Path.DataProperty,Geometry.Parse("M 13,19.5 L 17,23.5 L 25,15"));
            mark.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { RelativeSource=self });mark.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty,2.4);
            mark.SetValue(System.Windows.Shapes.Shape.StrokeStartLineCapProperty,PenLineCap.Round);mark.SetValue(System.Windows.Shapes.Shape.StrokeEndLineCapProperty,PenLineCap.Round);mark.SetValue(System.Windows.Shapes.Shape.StrokeLineJoinProperty,PenLineJoin.Round);
            mark.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("Content") { RelativeSource=self,Converter=TickVisibility.Instance });root.AppendChild(mark);
            var template=new ControlTemplate(typeof(Button)) { VisualTree=root };
            var hover=new Trigger { Property=UIElement.IsMouseOverProperty,Value=true };hover.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(3),"dot"));template.Triggers.Add(hover);
            var focus=new Trigger { Property=UIElement.IsKeyboardFocusedProperty,Value=true };focus.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"ring"));focus.Setters.Add(new Setter(Border.BorderBrushProperty,WpfFieldTheme.Accent,"ring"));template.Triggers.Add(focus);
            button.Template=template;return button;
        }
        internal static TextBox Input(string text=null,string tag=null,int maxLength=0) {
            var box=new TextBox { Text=text??"",Tag=tag,MinHeight=38,Padding=new Thickness(10,7,10,7),VerticalContentAlignment=VerticalAlignment.Center };if(maxLength>0)box.MaxLength=maxLength;return box;
        }

        // Themed replacement for System.Windows.MessageBox: same arguments and results.
        internal static MessageBoxResult Ask(Window owner,string text,string caption,MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage image=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.None) {
            var t=T;MessageBoxResult result=buttons==MessageBoxButton.OK?MessageBoxResult.OK:buttons==MessageBoxButton.YesNo?MessageBoxResult.No:MessageBoxResult.Cancel;
            try {
                var window=new Window { Title=caption??"SCUM MiniMap",Width=500,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,AllowsTransparency=false,
                    Background=WpfFieldTheme.Ground,Foreground=WpfFieldTheme.Ink,ShowInTaskbar=owner==null,Topmost=true,WindowStartupLocation=owner!=null&&owner.IsVisible?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen,
                    BorderBrush=t.Chamfer>0?WpfFieldTheme.Accent:WpfFieldTheme.Line2,BorderThickness=new Thickness(1) };
                if(owner!=null&&owner.IsVisible)window.Owner=owner;
                window.SourceInitialized+=(s,e)=>ThemedCaption.Apply(new System.Windows.Interop.WindowInteropHelper(window).Handle);
                var root=new DockPanel();var body=new DockPanel { Margin=new Thickness(26,24,26,22) };
                string glyph=image==MessageBoxImage.Warning?"triangle-alert":image==MessageBoxImage.Error?"circle-x":image==MessageBoxImage.Question?"circle-help":image==MessageBoxImage.Information?"info":null;
                var tone=image==MessageBoxImage.Warning?t.Brush(t.Warning):image==MessageBoxImage.Error?WpfFieldTheme.Warn:WpfFieldTheme.Accent;
                if(glyph!=null) { var tile=IconTile(glyph,tone,42);tile.Margin=new Thickness(0,0,16,0);tile.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(tile,Dock.Left);body.Children.Add(tile); }
                var words=new StackPanel();words.Children.Add(Title(caption??"",20));var message=Help(text??"",14);message.Foreground=WpfFieldTheme.Ink;message.Margin=new Thickness(0,8,0,0);words.Children.Add(message);body.Children.Add(words);
                var footer=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
                var footerFrame=new Border { Child=footer,Background=t.GradientAccent?Brushes.Transparent:WpfFieldTheme.Panel,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(26,14,26,16) };
                MessageBoxResult[] choices=buttons==MessageBoxButton.OKCancel?new[]{MessageBoxResult.OK,MessageBoxResult.Cancel}:buttons==MessageBoxButton.YesNo?new[]{MessageBoxResult.Yes,MessageBoxResult.No}:buttons==MessageBoxButton.YesNoCancel?new[]{MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel}:new[]{MessageBoxResult.OK};
                for(int i=choices.Length-1;i>=1;i--) { var choice=choices[i];var button=Secondary(Localization.Get(choice==MessageBoxResult.No?"DialogNo":"Cancel"),()=> { result=choice;window.Close(); });button.Margin=new Thickness(0,0,8,0);button.IsCancel=choice!=MessageBoxResult.No||choices.Length==2;if(choice==defaultResult)button.IsDefault=true;footer.Children.Add(button); }
                var first=choices[0];var primary=(image==MessageBoxImage.Warning&&choices.Length>1?Danger(Localization.Get(first==MessageBoxResult.Yes?"DialogYes":"DialogOk"),()=> { result=first;window.Close(); }):Primary(Localization.Get(first==MessageBoxResult.Yes?"DialogYes":"DialogOk"),()=> { result=first;window.Close(); }));
                primary.IsDefault=defaultResult==MessageBoxResult.None||defaultResult==first;if(choices.Length==1)primary.IsCancel=true;footer.Children.Add(primary);
                DockPanel.SetDock(footerFrame,Dock.Bottom);root.Children.Add(footerFrame);root.Children.Add(body);window.Content=root;
                window.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed&&!(e.OriginalSource is ButtonBase)) { try { window.DragMove(); } catch(InvalidOperationException) {} } };
                window.ShowDialog();return result;
            } catch(Exception error) {
                Program.LogException("ThemedAsk",error);
                return owner==null?MessageBox.Show(text,caption,buttons,image,defaultResult):MessageBox.Show(owner,text,caption,buttons,image,defaultResult);
            }
        }

        // ── Window frame ──────────────────────────────────────────────────
        // Standard dialog layout: header (title, subtitle, trailing controls), optional tab track,
        // scrolling body and a footer with actions. Padding and dividers follow the theme.
        internal sealed class Dialog {
            internal readonly Window Window;internal readonly StackPanel Body=new StackPanel();internal readonly ScrollViewer Scroll;
            internal readonly TextBlock Heading,Subtitle;internal readonly DockPanel Footer=new DockPanel { LastChildFill=false };
            internal readonly StackPanel FooterActions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            readonly DockPanel header=new DockPanel();readonly StackPanel headerTrailing=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top };
            readonly DockPanel root=new DockPanel();readonly Border footerFrame;readonly StackPanel top=new StackPanel();
            internal Dialog(Window window,string title,string subtitle=null) {
                var t=T;Window=window;window.Background=WpfFieldTheme.Ground;
                double side=t.Uppercase?34:t.GradientAccent?28:32;
                Heading=Title(title,t.GradientAccent?24:24);Subtitle=Help(subtitle??"",14);Subtitle.Margin=new Thickness(0,6,0,0);Subtitle.Visibility=string.IsNullOrEmpty(subtitle)?Visibility.Collapsed:Visibility.Visible;
                DockPanel.SetDock(headerTrailing,Dock.Right);header.Children.Add(headerTrailing);
                var titles=new StackPanel();
                if(t.Uppercase) { var bar=new Border { Width=28,Height=3,Background=WpfFieldTheme.Accent,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10) };titles.Children.Add(bar); }
                titles.Children.Add(Heading);titles.Children.Add(Subtitle);header.Children.Add(titles);
                header.MouseLeftButtonDown+=(s,e)=> { if(e.ButtonState==MouseButtonState.Pressed&&!(e.OriginalSource is ButtonBase)) { try { window.DragMove(); } catch(InvalidOperationException) {} } };
                top.Children.Add(header);top.Margin=new Thickness(side,t.Uppercase?24:22,side,0);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
                Footer.Children.Add(FooterActions);DockPanel.SetDock(FooterActions,Dock.Right);
                footerFrame=new Border { Child=Footer,BorderBrush=WpfFieldTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(side,14,side,16),Background=t.GradientAccent?Brushes.Transparent:WpfFieldTheme.Panel,Visibility=Visibility.Collapsed };
                DockPanel.SetDock(footerFrame,Dock.Bottom);root.Children.Add(footerFrame);
                Body.Margin=new Thickness(side,t.Uppercase?6:8,side-8,24);
                Scroll=new ScrollViewer { Content=Body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0) };
                root.Children.Add(Scroll);window.Content=root;
                if(!t.GradientAccent)top.Children.Add(new Border { Height=1,Background=WpfFieldTheme.Line,Margin=new Thickness(-side,18,-side,0),Visibility=Visibility.Collapsed,Tag="dialog-rule" });
            }
            // A rule under the header (hidden when tabs provide their own line).
            internal void Rule() { foreach(var rule in top.Children.OfType<Border>().Where(b=>(b.Tag as string)=="dialog-rule"))rule.Visibility=Visibility.Visible; }
            internal ListBox AddTabs(IList<string> labels,int selected,Action<int> select,string tagPrefix=null) {
                var tabs=Tabs(labels,selected,select,tagPrefix);var track=TabTrack(tabs);((FrameworkElement)track).Margin=new Thickness(0,18,0,0);top.Children.Add(track);return tabs;
            }
            internal void AddTabBar(FrameworkElement bar) { var track=TabTrack(bar);track.Margin=new Thickness(0,18,0,0);top.Children.Add(track); }
            internal void AddTrailing(UIElement element) { var item=element as FrameworkElement;if(item!=null)item.Margin=new Thickness(8,0,0,0);headerTrailing.Children.Add(element); }
            internal Button AddClose(string label) { var close=Quiet(label,()=>Window.Close(),"x");close.Tag="text:Done";AddTrailing(close);return close; }
            internal void AddAction(Button button) { footerFrame.Visibility=Visibility.Visible;button.Margin=new Thickness(8,0,0,0);FooterActions.Children.Add(button); }
            internal void AddFooterLeft(UIElement element) { footerFrame.Visibility=Visibility.Visible;DockPanel.SetDock(element,Dock.Left);Footer.Children.Insert(0,element); }
            internal void SetTitle(string title,string subtitle=null) {
                Window.Title=title;Heading.Text=T.Caps(title);
                if(subtitle!=null) { Subtitle.Text=subtitle;Subtitle.Visibility=subtitle.Length==0?Visibility.Collapsed:Visibility.Visible; }
            }
            internal void Add(UIElement element) { Body.Children.Add(element); }
        }
    }
}
