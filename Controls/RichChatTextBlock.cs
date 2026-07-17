using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TiHiY.StreamControlCenter.Models;

namespace TiHiY.StreamControlCenter.Controls;

/// <summary>
/// Renders a chat message as one wrapping text flow. Unicode emoji stay in text and
/// use the Windows emoji font fallback; platform emotes are inserted as inline images.
/// </summary>
public sealed class RichChatTextBlock : TextBlock
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(ChatMessage), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged));

    public static readonly DependencyProperty IncludeUserProperty = DependencyProperty.Register(
        nameof(IncludeUser), typeof(bool), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged));

    public static readonly DependencyProperty UserBrushProperty = DependencyProperty.Register(
        nameof(UserBrush), typeof(Brush), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty MessageBrushProperty = DependencyProperty.Register(
        nameof(MessageBrush), typeof(Brush), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
        nameof(HighlightBrush), typeof(Brush), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(Brushes.Gold, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty EmoteSizeProperty = DependencyProperty.Register(
        nameof(EmoteSize), typeof(double), typeof(RichChatTextBlock),
        new FrameworkPropertyMetadata(22d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnVisualPropertyChanged));

    public ChatMessage? Message
    {
        get => (ChatMessage?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IncludeUser
    {
        get => (bool)GetValue(IncludeUserProperty);
        set => SetValue(IncludeUserProperty, value);
    }

    public Brush UserBrush
    {
        get => (Brush)GetValue(UserBrushProperty);
        set => SetValue(UserBrushProperty, value);
    }

    public Brush MessageBrush
    {
        get => (Brush)GetValue(MessageBrushProperty);
        set => SetValue(MessageBrushProperty, value);
    }

    public Brush HighlightBrush
    {
        get => (Brush)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public double EmoteSize
    {
        get => (double)GetValue(EmoteSizeProperty);
        set => SetValue(EmoteSizeProperty, value);
    }

    public RichChatTextBlock()
    {
        TextWrapping = TextWrapping.Wrap;
        FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
        Loaded += (_, _) => RebuildInlines();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RichChatTextBlock)d).RebuildInlines();

    private void RebuildInlines()
    {
        Inlines.Clear();
        var message = Message;
        if (message is null) return;

        if (IncludeUser)
        {
            Inlines.Add(new Run(message.User + ": ")
            {
                Foreground = UserBrush,
                FontWeight = FontWeights.Bold
            });
        }

        var text = message.Text ?? string.Empty;
        var emotes = message.Emotes
            .Where(x => x.Start >= 0 && x.End >= x.Start && x.End < text.Length && !string.IsNullOrWhiteSpace(x.ImageUrl))
            .OrderBy(x => x.Start)
            .ThenByDescending(x => x.Length)
            .ToList();

        var cursor = 0;
        foreach (var emote in emotes)
        {
            if (emote.Start < cursor) continue;
            AddText(text[cursor..emote.Start], message);
            AddEmote(emote);
            cursor = emote.End + 1;
        }

        if (cursor < text.Length) AddText(text[cursor..], message);
    }

    private void AddText(string value, ChatMessage message)
    {
        if (value.Length == 0) return;
        Inlines.Add(new Run(value)
        {
            Foreground = message.IsHighlighted ? HighlightBrush : MessageBrush,
            FontWeight = message.IsHighlighted ? FontWeights.Bold : FontWeights.Normal
        });
    }

    private void AddEmote(ChatEmote emote)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(emote.ImageUrl, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnDemand;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.EndInit();

            var image = new Image
            {
                Source = bitmap,
                Width = EmoteSize,
                Height = EmoteSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = string.IsNullOrWhiteSpace(emote.Name) ? emote.Id : emote.Name,
                Margin = new Thickness(1, 0, 1, -3)
            };
            Inlines.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center });
        }
        catch
        {
            AddText(string.IsNullOrWhiteSpace(emote.Name) ? "□" : emote.Name, Message!);
        }
    }
}
