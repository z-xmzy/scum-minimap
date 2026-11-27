using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace ScumMiniMap {
    internal enum WpfTabStyle { Segmented, Rail, Strip }

    // One interface theme. Colours stay as #AARRGGBB strings so the same values feed XAML templates, brushes and map pens.
    internal sealed class WpfThemeSpec {
        internal string Id,NameKey,DescriptionKey;
        // Surfaces: Ground = window background, Panel = translucent overlay panel, PanelSolid = opaque panel/dialog body.
        internal string Ground,Panel,PanelSolid,Raised,Hover,Popup,Input,Line,Line2,Ink,Muted,Dim;
        internal string Accent,Accent2,AccentInk,AccentSoft,Danger,Warning,Success,Info;
        // Map presentation.
        internal string RouteCasing,RouteGlow,MarkerFill,MarkerStroke,LabelHalo;
        internal double PanelRadius,ControlRadius,ChipRadius,Chamfer;
        internal double SwitchWidth,SwitchHeight,SwitchRadius,KnobSize,KnobRadius;
        internal string KnobOff,KnobOn,TrackOff;
        internal double ThumbWidth,ThumbHeight,ThumbRadius,TrackHeight;
        internal string ThumbFill;
        // Layout personality.
        internal bool Uppercase,Mirror,CompassTape,KeyHints,StatusBar,GradientAccent,RoundMarkers;
        internal WpfTabStyle Tabs;
        internal string UiFontSpec,DisplayFontSpec,MonoFontSpec;
        internal FontWeight DisplayWeight=FontWeights.Bold;
        readonly Dictionary<string,Brush> brushes=new Dictionary<string,Brush>(StringComparer.OrdinalIgnoreCase);
        FontFamily ui,display,mono;Brush accentFill;
        internal Color Colour(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        internal Brush Brush(string hex) {
            Brush brush;if(brushes.TryGetValue(hex,out brush))return brush;
            brush=new SolidColorBrush(Colour(hex));brush.Freeze();brushes[hex]=brush;return brush;
        }
        internal Brush Alpha(string hex,byte alpha) { var c=Colour(hex);return Brush(string.Format(CultureInfo.InvariantCulture,"#{0:X2}{1:X2}{2:X2}{3:X2}",alpha,c.R,c.G,c.B)); }
        // Primary actions: a two-stop gradient in the glass theme, otherwise the flat accent.
        internal Brush AccentFill { get {
            if(accentFill!=null)return accentFill;
            if(GradientAccent&&Accent2!=Accent) { var g=new LinearGradientBrush(Colour(Accent),Colour(Accent2),new Point(0,0),new Point(1,1));g.Freeze();accentFill=g; }
            else accentFill=Brush(Accent);
            return accentFill;
        } }
        internal FontFamily UiFont { get { return ui??(ui=WpfThemes.Font(UiFontSpec)); } }
        internal FontFamily DisplayFont { get { return display??(display=WpfThemes.Font(DisplayFontSpec)); } }
        internal FontFamily MonoFont { get { return mono??(mono=WpfThemes.Font(MonoFontSpec)); } }
        internal void ResetFonts() { ui=display=mono=null; }
        // Latin captions are uppercased in the tactical theme; other scripts are left unchanged by ToUpper.
        internal string Caps(string text) { return Uppercase&&text!=null?text.ToUpper(CultureInfo.CurrentCulture):text; }
    }

    internal static class WpfThemes {
        internal const string FileName="ui-theme.txt";
        internal static readonly string[] FontFiles={"Inter-Regular.otf","Inter-SemiBold.otf","Inter-Bold.otf","BarlowCondensed-SemiBold.ttf","BarlowCondensed-Bold.ttf","BarlowCondensed-ExtraBold.ttf"};
        const string InterUi="./#Inter, Segoe UI, Microsoft YaHei UI";
        const string SystemMono="Cascadia Mono, Consolas, Microsoft YaHei UI";

        // Modern shooter HUD: condensed display type, chamfered panels, blaze orange.
        internal static readonly WpfThemeSpec Tactical=new WpfThemeSpec {
            Id="tactical",NameKey="ThemeTactical",DescriptionKey="ThemeTacticalHelp",
            Ground="#0D0E0F",Panel="#F20D0E0F",PanelSolid="#121314",Raised="#1C1E20",Hover="#26292C",Popup="#16181A",Input="#0A0B0C",Line="#24FFFFFF",Line2="#4DFFFFFF",
            Ink="#F2F0EA",Muted="#A29F99",Dim="#64625E",Accent="#FF6B2C",Accent2="#FF6B2C",AccentInk="#140600",AccentSoft="#33FF6B2C",
            Danger="#FF3B47",Warning="#FFB020",Success="#5BE37D",Info="#8EC5FF",
            RouteCasing="#CC000000",RouteGlow=null,MarkerFill="#E00A0B0C",MarkerStroke="#8CF2F0EA",LabelHalo="#D9000000",
            PanelRadius=0,ControlRadius=2,ChipRadius=2,Chamfer=14,
            SwitchWidth=40,SwitchHeight=20,SwitchRadius=0,KnobSize=14,KnobRadius=0,KnobOff="#A29F99",KnobOn="#140600",TrackOff="#2EFFFFFF",
            ThumbWidth=8,ThumbHeight=18,ThumbRadius=0,TrackHeight=6,ThumbFill="#F2F0EA",
            Uppercase=true,CompassTape=true,KeyHints=true,RoundMarkers=false,Tabs=WpfTabStyle.Strip,
            UiFontSpec="Bahnschrift, Segoe UI, Microsoft YaHei UI",DisplayFontSpec="./#Barlow Condensed, Bahnschrift, Microsoft YaHei UI",MonoFontSpec="./#Barlow Condensed, Bahnschrift, Microsoft YaHei UI",
            DisplayWeight=FontWeights.ExtraBold
        };
        // Floating frosted panels, generous radii, blue-violet accent. WPF overlays cannot blur the game, so panels are denser.
        internal static readonly WpfThemeSpec Glass=new WpfThemeSpec {
            Id="glass",NameKey="ThemeGlass",DescriptionKey="ThemeGlassHelp",
            Ground="#10131A",Panel="#F2181B24",PanelSolid="#171A22",Raised="#16FFFFFF",Hover="#24FFFFFF",Popup="#1E222C",Input="#33000000",Line="#1CFFFFFF",Line2="#33FFFFFF",
            Ink="#F2F5FB",Muted="#9CA5BA",Dim="#68718A",Accent="#4C9BFF",Accent2="#8A6CFF",AccentInk="#FFFFFF",AccentSoft="#2E4C9BFF",
            Danger="#FF5E6C",Warning="#FFB547",Success="#3DDC97",Info="#7CC4FF",
            RouteCasing="#99081020",RouteGlow="#665A96FF",MarkerFill="#D9101218",MarkerStroke="#40FFFFFF",LabelHalo="#C0080A10",
            PanelRadius=20,ControlRadius=12,ChipRadius=99,Chamfer=0,
            SwitchWidth=42,SwitchHeight=25,SwitchRadius=12.5,KnobSize=21,KnobRadius=10.5,KnobOff="#FFFFFF",KnobOn="#FFFFFF",TrackOff="#30FFFFFF",
            ThumbWidth=20,ThumbHeight=20,ThumbRadius=10,TrackHeight=5,ThumbFill="#FFFFFF",
            GradientAccent=true,RoundMarkers=true,Tabs=WpfTabStyle.Segmented,
            UiFontSpec=InterUi,DisplayFontSpec=InterUi,MonoFontSpec=InterUi,DisplayWeight=FontWeights.SemiBold
        };
        // Linear/Raycast-style tool: solid near-black surfaces, hairlines, one violet accent, mono figures.
        internal static readonly WpfThemeSpec Minimal=new WpfThemeSpec {
            Id="minimal",NameKey="ThemeMinimal",DescriptionKey="ThemeMinimalHelp",
            Ground="#0B0B0D",Panel="#F70E0E11",PanelSolid="#0E0E11",Raised="#18181C",Hover="#202026",Popup="#141417",Input="#111114",Line="#24242B",Line2="#34343D",
            Ink="#EDEDF0",Muted="#9A9AA6",Dim="#62626E",Accent="#8B7CFF",Accent2="#8B7CFF",AccentInk="#FFFFFF",AccentSoft="#298B7CFF",
            Danger="#F2555A",Warning="#F5A524",Success="#3CCB7F",Info="#9DB8FF",
            RouteCasing="#E60B0B0D",RouteGlow=null,MarkerFill="#E60D0D10",MarkerStroke="#3A3A44",LabelHalo="#CC000000",
            PanelRadius=12,ControlRadius=8,ChipRadius=6,Chamfer=0,
            SwitchWidth=34,SwitchHeight=20,SwitchRadius=10,KnobSize=14,KnobRadius=7,KnobOff="#9A9AA6",KnobOn="#FFFFFF",TrackOff="#202026",
            ThumbWidth=14,ThumbHeight=14,ThumbRadius=7,TrackHeight=4,ThumbFill="#FFFFFF",
            Mirror=true,StatusBar=true,RoundMarkers=false,Tabs=WpfTabStyle.Rail,
            UiFontSpec=InterUi,DisplayFontSpec=InterUi,MonoFontSpec=SystemMono,DisplayWeight=FontWeights.SemiBold
        };
        internal static readonly WpfThemeSpec[] All={Tactical,Glass,Minimal};
        internal static WpfThemeSpec Default { get { return Tactical; } }
        internal static WpfThemeSpec Find(string id) { return All.FirstOrDefault(t=>string.Equals(t.Id,(id??"").Trim(),StringComparison.OrdinalIgnoreCase))??Default; }

        static Uri fontBase;
        // Bundled fonts live beside the profile so WPF can resolve "./#Family" names; system fonts remain the fallback.
        internal static void ExtractFonts(string folder) {
            try {
                Directory.CreateDirectory(folder);
                var assembly=typeof(WpfThemes).Assembly;
                foreach(string name in FontFiles) {
                    using(var stream=assembly.GetManifestResourceStream(name)) {
                        if(stream==null)continue;
                        string path=Path.Combine(folder,name);
                        if(File.Exists(path)&&new FileInfo(path).Length==stream.Length)continue;
                        using(var file=File.Create(path))stream.CopyTo(file);
                    }
                }
                fontBase=new Uri(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar);
                foreach(var theme in All)theme.ResetFonts();
            }catch(Exception error) { Program.LogException("ThemeFonts",error); }
        }
        internal static FontFamily Font(string spec) {
            if(fontBase!=null&&spec.Contains("./#"))return new FontFamily(fontBase,spec);
            return new FontFamily(string.Join(", ",spec.Split(',').Select(s=>s.Trim()).Where(s=>!s.StartsWith("./#",StringComparison.Ordinal))));
        }
        internal static WpfThemeSpec Load(string folder) {
            try { string path=Path.Combine(folder,FileName);if(File.Exists(path))return Find(File.ReadAllText(path,Encoding.UTF8)); }
            catch(Exception error) { Program.LogException("ThemeLoad",error); }
            return Default;
        }
        internal static bool Save(string folder,WpfThemeSpec theme) {
            try {
                Directory.CreateDirectory(folder);string path=Path.Combine(folder,FileName),temp=path+".tmp";
                File.WriteAllText(temp,theme.Id,new UTF8Encoding(false));
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                return true;
            }catch(Exception error) { Program.LogException("ThemeSave",error);return false; }
        }

        static string Num(double value) { return value.ToString("0.##",CultureInfo.InvariantCulture); }
        // Control templates shared by every window. Tokens are $Name$ placeholders filled from the theme.
        internal static ResourceDictionary Resources(WpfThemeSpec t) {
            var tokens=new Dictionary<string,string> {
                {"Ground",t.Ground},{"Panel",t.PanelSolid},{"Raised",t.Raised},{"Hover",t.Hover},{"Popup",t.Popup},{"Input",t.Input},{"Line",t.Line},{"Line2",t.Line2},
                {"Ink",t.Ink},{"Muted",t.Muted},{"Dim",t.Dim},{"Accent",t.Accent},{"AccentInk",t.AccentInk},{"AccentSoft",t.AccentSoft},
                {"CR",Num(t.ControlRadius)},{"PR",Num(Math.Min(t.PanelRadius,14))},
                {"SW",Num(t.SwitchWidth)},{"SH",Num(t.SwitchHeight)},{"SR",Num(t.SwitchRadius)},{"KS",Num(t.KnobSize)},{"KR",Num(t.KnobRadius)},
                {"KM",Num((t.SwitchHeight-t.KnobSize)/2)},{"KnobOff",t.KnobOff},{"KnobOn",t.KnobOn},{"TrackOff",t.TrackOff},
                {"TW",Num(t.ThumbWidth)},{"TH",Num(t.ThumbHeight)},{"TR",Num(t.ThumbRadius)},{"TrH",Num(t.TrackHeight)},{"TrR",Num(Math.Min(t.TrackHeight/2,t.ControlRadius))},{"ThumbFill",t.ThumbFill},
                {"AccentHalf","#80"+t.Accent.Substring(t.Accent.Length-6)},
                {"ChevData",t.Tabs==WpfTabStyle.Strip?"M 3,1.5 L 7.5,5 L 3,8.5 Z":"M 3.5,1.5 L 7,5 L 3.5,8.5"},
                {"ChevFill",t.Tabs==WpfTabStyle.Strip?t.Muted:"Transparent"},{"ChevStroke",t.Tabs==WpfTabStyle.Strip?"Transparent":t.Muted},
                {"BoxR",Num(Math.Min(6,t.ControlRadius/2))},{"BarVis",t.Chamfer>0?"Visible":"Collapsed"},
                {"SegR",t.Tabs==WpfTabStyle.Segmented?"10":t.Tabs==WpfTabStyle.Rail?"6":"0"},
                {"SegPad",t.Tabs==WpfTabStyle.Strip?"16,9,16,8":t.Tabs==WpfTabStyle.Segmented?"16,7":"12,7"},
                {"SegBorder",t.Tabs==WpfTabStyle.Strip?"0,0,0,3":"0"},
                {"SegOnBg",t.Tabs==WpfTabStyle.Strip?t.AccentSoft:t.Tabs==WpfTabStyle.Segmented?t.Accent:t.Hover},
                {"SegOnLine",t.Tabs==WpfTabStyle.Strip?t.Accent:"Transparent"},
                {"SegOnInk",t.Tabs==WpfTabStyle.Strip?t.Accent:t.Tabs==WpfTabStyle.Segmented?t.AccentInk:t.Ink}
            };
            var xaml=new StringBuilder(Template);
            foreach(var pair in tokens)xaml.Replace("$"+pair.Key+"$",pair.Value);
            return (ResourceDictionary)XamlReader.Parse(xaml.ToString());
        }

        const string Template=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='BorderBrush' Value='$Line$'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontSize' Value='14'/><Setter Property='FontWeight' Value='Medium'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='body' CornerRadius='$CR$' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='body' Property='Opacity' Value='.72'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='body' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='body' Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='Ghost' TargetType='{x:Type Button}'><Setter Property='Cursor' Value='Hand'/><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='$Muted$'/><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='0'/><Setter Property='FontSize' Value='13'/><Setter Property='FontWeight' Value='Medium'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='body' CornerRadius='$CR$' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='Background' Value='$Hover$'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='body' Property='Opacity' Value='.72'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='body' Property='Opacity' Value='.35'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='PoiTile' TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='$Muted$'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Border x:Name='body' MinHeight='60' Margin='3' Padding='9,8' CornerRadius='$CR$' Background='$Raised$' BorderBrush='$Line$' BorderThickness='1' SnapsToDevicePixels='True'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='body' Property='Background' Value='$AccentSoft$'/><Setter TargetName='body' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='body' Property='BorderBrush' Value='$AccentHalf$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Foreground' Value='$Accent$'/></Trigger><Trigger Property='IsChecked' Value='{x:Null}'><Setter Property='Foreground' Value='$Ink$'/></Trigger></Style.Triggers></Style>
<Style TargetType='{x:Type TextBox}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='Background' Value='$Input$'/><Setter Property='BorderBrush' Value='$Line$'/><Setter Property='BorderThickness' Value='1'/><Setter Property='CaretBrush' Value='$Accent$'/><Setter Property='SelectionBrush' Value='$Accent$'/><Setter Property='Padding' Value='12,9'/><Setter Property='FontSize' Value='15'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TextBox}'><Border x:Name='frame' CornerRadius='$CR$' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ScrollViewer x:Name='PART_ContentHost' Padding='{TemplateBinding Padding}' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type PasswordBox}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='Background' Value='$Input$'/><Setter Property='BorderBrush' Value='$Line$'/><Setter Property='BorderThickness' Value='1'/><Setter Property='CaretBrush' Value='$Accent$'/><Setter Property='Padding' Value='12,9'/><Setter Property='FontSize' Value='15'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type PasswordBox}'><Border x:Name='frame' CornerRadius='$CR$' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ScrollViewer x:Name='PART_ContentHost' Padding='{TemplateBinding Padding}' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='14'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Grid MinHeight='40' Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions><ContentPresenter VerticalAlignment='Center' Margin='0,0,12,0'/><Border x:Name='pill' Grid.Column='1' Width='$SW$' Height='$SH$' CornerRadius='$SR$' Background='$TrackOff$' BorderBrush='Transparent' BorderThickness='0' VerticalAlignment='Center'><Border x:Name='knob' Width='$KS$' Height='$KS$' CornerRadius='$KR$' Margin='$KM$' Background='$KnobOff$' HorizontalAlignment='Left'/></Border></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='pill' Property='Background' Value='$Accent$'/><Setter TargetName='knob' Property='Background' Value='$KnobOn$'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='knob' Property='HorizontalAlignment' Value='Center'/><Setter TargetName='pill' Property='Background' Value='$AccentSoft$'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='pill' Property='BorderBrush' Value='$Line2$'/><Setter TargetName='pill' Property='BorderThickness' Value='1'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='pill' Property='BorderBrush' Value='$Ink$'/><Setter TargetName='pill' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type RadioButton}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='14'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RadioButton}'><Border x:Name='body' MinHeight='36' Padding='12,6' CornerRadius='$CR$' Background='$Raised$' BorderBrush='$Line$' BorderThickness='1'><ContentPresenter VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='body' Property='Background' Value='$AccentSoft$'/><Setter TargetName='body' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='body' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='body' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ListBox}'><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='$Ink$'/><Setter Property='BorderThickness' Value='0'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/></Style>
<Style TargetType='{x:Type ListBoxItem}'><Setter Property='Padding' Value='12,10'/><Setter Property='Margin' Value='0,2'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='row' CornerRadius='$CR$' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='row' Property='Background' Value='$Raised$'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/><Setter TargetName='row' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='BorderBrush' Value='$Accent$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='SettingsNav' TargetType='{x:Type ListBoxItem}'><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='row' CornerRadius='$CR$' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='row' Property='Background' Value='$Raised$'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='BorderBrush' Value='$Accent$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='SettingsTab' TargetType='{x:Type ListBoxItem}'><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='row' Background='Transparent' BorderBrush='Transparent' BorderThickness='0,0,0,3' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='$AccentSoft$'/><Setter TargetName='row' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBoxItem}'><Setter Property='Padding' Value='12,8'/><Setter Property='Foreground' Value='$Ink$'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'><Border x:Name='row' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='$AccentSoft$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBox}'><Setter Property='FontSize' Value='14'/><Setter Property='MinHeight' Value='36'/><Setter Property='Foreground' Value='$Ink$'/><Setter Property='Background' Value='$Raised$'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'><Grid><Border x:Name='outline' Background='$Raised$' BorderBrush='$Line$' BorderThickness='1' CornerRadius='$CR$'/><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='Transparent' Padding='12,8'><Path Data='M 1.5,3.5 L 5,7 L 8.5,3.5' Stroke='$Muted$' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Width='10' Height='10' HorizontalAlignment='Right' VerticalAlignment='Center'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='12,6,32,6' VerticalAlignment='Center' IsHitTestVisible='False'/><Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border Background='$Popup$' BorderBrush='$Line2$' BorderThickness='1' CornerRadius='$CR$' Margin='0,4,0,0' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='300' Padding='4'><ScrollViewer><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='outline' Property='BorderBrush' Value='$Line2$'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='outline' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='outline' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type Slider}'><Setter Property='MinHeight' Value='28'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Slider}'><Grid><Border x:Name='focus' BorderBrush='Transparent' BorderThickness='2' CornerRadius='$CR$' IsHitTestVisible='False'/><Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' IsDirectionReversed='{TemplateBinding IsDirectionReversed}'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge'><RepeatButton.Template><ControlTemplate><Border Height='$TrH$' CornerRadius='$TrR$' Background='$Accent$'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge'><RepeatButton.Template><ControlTemplate><Border Height='$TrH$' CornerRadius='$TrR$' Background='$TrackOff$'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width='$TW$' Height='$TH$'><Thumb.Template><ControlTemplate><Border Background='$ThumbFill$' CornerRadius='$TR$' BorderBrush='$Ground$' BorderThickness='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate><Border Background='$Line2$' CornerRadius='3' Margin='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Height' Value='8'/><Setter Property='MinHeight' Value='8'/><Setter Property='Width' Value='Auto'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type Thumb}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='Transparent'><Border Width='3' Height='18' CornerRadius='1' Background='$Line2$' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type Expander}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Expander}'><Border Background='{TemplateBinding Background}' CornerRadius='$CR$' Padding='{TemplateBinding Padding}'><StackPanel><ToggleButton x:Name='toggle' Foreground='{TemplateBinding Foreground}' Cursor='Hand' IsChecked='{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}' HorizontalContentAlignment='Stretch'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border x:Name='focus' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' CornerRadius='$CR$' Padding='2,7'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition/></Grid.ColumnDefinitions><Path x:Name='arrow' Data='$ChevData$' Fill='$ChevFill$' Stroke='$ChevStroke$' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Width='10' Height='10' HorizontalAlignment='Left' VerticalAlignment='Center' RenderTransformOrigin='0.5,0.5'/><ContentPresenter Grid.Column='1'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='arrow' Property='RenderTransform'><Setter.Value><RotateTransform Angle='90'/></Setter.Value></Setter></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='focus' Property='BorderBrush' Value='$Accent$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template><ContentPresenter ContentSource='Header'/></ToggleButton><ContentPresenter x:Name='items' ContentSource='Content' Visibility='Collapsed'/></StackPanel></Border><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='items' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ToolTip}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='13'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'><Border Background='$Popup$' BorderBrush='$Line2$' BorderThickness='1' CornerRadius='$CR$' Padding='10,6'><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ContextMenu}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='14'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ContextMenu}'><Border Background='$Popup$' BorderBrush='$Line2$' BorderThickness='1' CornerRadius='$CR$' Padding='4'><StackPanel IsItemsHost='True'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='Check' TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='14'/><Setter Property='Cursor' Value='Hand'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Grid Background='Transparent' MinHeight='30'><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Border x:Name='box' Width='18' Height='18' CornerRadius='$BoxR$' BorderThickness='1.5' BorderBrush='$Line2$' Background='$Input$' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' Margin='0,1,10,1'><Grid><Path x:Name='tick' Data='M 3,7.6 L 6.2,10.6 L 12,4.4' Stroke='$AccentInk$' StrokeThickness='2' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Visibility='Collapsed'/><Border x:Name='dash' Width='8' Height='2' Background='$AccentInk$' Visibility='Collapsed'/></Grid></Border><ContentPresenter Grid.Column='1' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='$Accent$'/><Setter TargetName='box' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='box' Property='Background' Value='$Accent$'/><Setter TargetName='box' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='dash' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='$Ink$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='CheckCard' TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='15'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='16,14'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'><Border x:Name='card' Background='$Raised$' BorderBrush='$Line$' BorderThickness='1' CornerRadius='$CR$' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Border x:Name='bar' Width='3' Margin='-16,-14,0,-14' HorizontalAlignment='Left' Background='$Accent$' Visibility='Collapsed'/><Border x:Name='box' Width='20' Height='20' CornerRadius='$BoxR$' BorderThickness='1.5' BorderBrush='$Line2$' Background='$Input$' VerticalAlignment='Top' Margin='0,1,14,0'><Path x:Name='tick' Data='M 3.4,8.6 L 7,12 L 13.6,5' Stroke='$AccentInk$' StrokeThickness='2.2' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' Visibility='Collapsed'/></Border><ContentPresenter Grid.Column='1' VerticalAlignment='Center'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='$Line2$'/><Setter TargetName='box' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='$Accent$'/><Setter TargetName='box' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='tick' Property='Visibility' Value='Visible'/><Setter TargetName='card' Property='BorderBrush' Value='$AccentHalf$'/><Setter TargetName='bar' Property='Visibility' Value='$BarVis$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='ChoiceCard' TargetType='{x:Type RadioButton}'><Setter Property='Foreground' Value='$Ink$'/><Setter Property='FontSize' Value='15'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='16,13'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RadioButton}'><Border x:Name='card' Background='$Raised$' BorderBrush='$Line$' BorderThickness='1' CornerRadius='$CR$' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Grid Width='20' Height='20' VerticalAlignment='Top' Margin='0,1,14,0'><Ellipse x:Name='ring' Stroke='$Line2$' StrokeThickness='1.5' Fill='$Input$'/><Ellipse x:Name='dot' Width='8' Height='8' Fill='$Accent$' Visibility='Collapsed'/></Grid><ContentPresenter Grid.Column='1' VerticalAlignment='Center'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='$Line2$'/><Setter TargetName='ring' Property='Stroke' Value='$Accent$'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='ring' Property='Stroke' Value='$Accent$'/><Setter TargetName='dot' Property='Visibility' Value='Visible'/><Setter TargetName='card' Property='BorderBrush' Value='$Accent$'/><Setter TargetName='card' Property='Background' Value='$AccentSoft$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='$Accent$'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style x:Key='Segment' TargetType='{x:Type ListBoxItem}'><Setter Property='Cursor' Value='Hand'/><Setter Property='Foreground' Value='$Muted$'/><Setter Property='FontSize' Value='14'/><Setter Property='Padding' Value='$SegPad$'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='row' Background='Transparent' BorderBrush='Transparent' BorderThickness='$SegBorder$' CornerRadius='$SegR$' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='row' Property='Background' Value='$Hover$'/><Setter Property='Foreground' Value='$Ink$'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='$SegOnBg$'/><Setter TargetName='row' Property='BorderBrush' Value='$SegOnLine$'/><Setter Property='Foreground' Value='$SegOnInk$'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='BorderBrush' Value='$Accent$'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
    }

    // Native title bars follow the theme: dark mode everywhere, and on Windows 11 the caption, text and
    // border take the theme's ground, ink and hairline colours. Older systems ignore what they lack.
    internal static class ThemedCaption {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        internal static void Apply(IntPtr handle) { Apply(handle,WpfFieldTheme.Current); }
        internal static void Apply(IntPtr handle,WpfThemeSpec t) {
            if(handle==IntPtr.Zero||t==null)return;
            try {
                int dark=1;if(DwmSetWindowAttribute(handle,20,ref dark,4)!=0)DwmSetWindowAttribute(handle,19,ref dark,4);
                int caption=ColorRef(t.Ground,t.Ground),text=ColorRef(t.Ink,t.Ground),border=ColorRef(t.Chamfer>0?t.Accent:t.Line2,t.Ground);
                DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);DwmSetWindowAttribute(handle,34,ref border,4);
            }
            catch(DllNotFoundException) {}
            catch(EntryPointNotFoundException) {}
        }
        // COLORREF (0x00BBGGRR) of a possibly translucent colour flattened onto the ground colour.
        static int ColorRef(string hex,string ground) {
            var c=(Color)ColorConverter.ConvertFromString(hex);var g=(Color)ColorConverter.ConvertFromString(ground);double a=c.A/255.0;
            int r=(int)Math.Round(c.R*a+g.R*(1-a)),gr=(int)Math.Round(c.G*a+g.G*(1-a)),b=(int)Math.Round(c.B*a+g.B*(1-a));
            return r|(gr<<8)|(b<<16);
        }
        internal static System.Drawing.Color Flatten(string hex,string ground) { int value=ColorRef(hex,ground);return System.Drawing.Color.FromArgb(value&0xFF,(value>>8)&0xFF,(value>>16)&0xFF); }
    }

    // Panel surface with optional chamfered corners (tactical) or rounded corners (glass/minimal).
    // Public because control templates parsed at runtime may reference it.
    public sealed class ChamferBorder:System.Windows.Controls.Decorator {
        public static readonly DependencyProperty BackgroundProperty=DependencyProperty.Register("Background",typeof(Brush),typeof(ChamferBorder),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty BorderBrushProperty=DependencyProperty.Register("BorderBrush",typeof(Brush),typeof(ChamferBorder),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty AccentBrushProperty=DependencyProperty.Register("AccentBrush",typeof(Brush),typeof(ChamferBorder),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush Background { get { return (Brush)GetValue(BackgroundProperty); } set { SetValue(BackgroundProperty,value); } }
        public Brush BorderBrush { get { return (Brush)GetValue(BorderBrushProperty); } set { SetValue(BorderBrushProperty,value); } }
        // Optional accent bar on the left edge (tactical objective cards).
        public Brush AccentBrush { get { return (Brush)GetValue(AccentBrushProperty); } set { SetValue(AccentBrushProperty,value); } }
        public double Cut { get; set; }
        public double Radius { get; set; }
        public double BorderThickness { get; set; }
        public double AccentWidth { get; set; }
        public Thickness Padding { get; set; }
        public bool Hatch { get; set; }
        public ChamferBorder() { BorderThickness=1; }
        internal Geometry Outline(Size size) {
            double w=Math.Max(0,size.Width),h=Math.Max(0,size.Height),c=Math.Min(Cut,Math.Min(w,h)/2);
            if(c<=0)return new RectangleGeometry(new Rect(0,0,w,h),Math.Min(Radius,Math.Min(w,h)/2),Math.Min(Radius,Math.Min(w,h)/2));
            var g=new StreamGeometry();
            using(var s=g.Open()) {
                s.BeginFigure(new Point(0,0),true,true);s.LineTo(new Point(w-c,0),true,false);s.LineTo(new Point(w,c),true,false);
                s.LineTo(new Point(w,h),true,false);s.LineTo(new Point(c,h),true,false);s.LineTo(new Point(0,h-c),true,false);
            }
            g.Freeze();return g;
        }
        protected override Size MeasureOverride(Size constraint) {
            var p=Padding;double extraW=p.Left+p.Right+AccentWidth,extraH=p.Top+p.Bottom;
            if(Child==null)return new Size(extraW,extraH);
            Child.Measure(new Size(Math.Max(0,constraint.Width-extraW),Math.Max(0,constraint.Height-extraH)));
            return new Size(Child.DesiredSize.Width+extraW,Child.DesiredSize.Height+extraH);
        }
        protected override Size ArrangeOverride(Size arrange) {
            var p=Padding;
            if(Child!=null)Child.Arrange(new Rect(p.Left+AccentWidth,p.Top,Math.Max(0,arrange.Width-p.Left-p.Right-AccentWidth),Math.Max(0,arrange.Height-p.Top-p.Bottom)));
            Clip=Outline(arrange);
            return arrange;
        }
        static Brush hatch;
        protected override void OnRender(DrawingContext dc) {
            var size=new Size(ActualWidth,ActualHeight);var outline=Outline(size);
            dc.DrawGeometry(Background,null,outline);
            if(Hatch) {
                if(hatch==null) {
                    var tile=new DrawingGroup();using(var t=tile.Open()) { t.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,8,8));t.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(7,255,255,255)),2),new Point(0,8),new Point(8,0)); }
                    var brush=new DrawingBrush(tile) { TileMode=TileMode.Tile,Viewport=new Rect(0,0,8,8),ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.None };brush.Freeze();hatch=brush;
                }
                dc.DrawGeometry(hatch,null,outline);
            }
            if(AccentBrush!=null&&AccentWidth>0)dc.DrawRectangle(AccentBrush,null,new Rect(0,0,AccentWidth,size.Height));
            if(BorderBrush!=null&&BorderThickness>0) {
                double inset=BorderThickness/2;var inner=Outline(new Size(Math.Max(0,size.Width-BorderThickness),Math.Max(0,size.Height-BorderThickness))).Clone();
                inner.Transform=new TranslateTransform(inset,inset);
                dc.DrawGeometry(null,new Pen(BorderBrush,BorderThickness),inner);
            }
        }
    }
}
