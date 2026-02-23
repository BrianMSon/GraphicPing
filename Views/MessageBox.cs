using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace GraphicPing.Views;

public static class MessageBox
{
    public enum MessageBoxButtons { Ok, YesNo }
    public enum MessageBoxResult { Ok, Yes, No }

    public static async Task<MessageBoxResult> Show(Window parent, string message, string title,
        MessageBoxButtons buttons = MessageBoxButtons.Ok)
    {
        var result = MessageBoxResult.No;
        var isDark = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;

        var bgColor = isDark ? "#1E2030" : "#F0F4F8";
        var titleColor = isDark ? "#CDD6F4" : "#1E3A5F";
        var msgColor = isDark ? "#A6ADC8" : "#444444";
        var sepColor = isDark ? "#3B3F5C" : "#E0E0E0";
        var noBtnBg = isDark ? "#363850" : "#E8EDF2";
        var noBtnFg = isDark ? "#CDD6F4" : "#333333";
        var noBtnBorder = isDark ? "#4A4E6A" : "#C0C0C0";

        var win = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse(titleColor)),
            Margin = new Thickness(20, 16, 20, 0)
        };

        var msgText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse(msgColor)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 12, 20, 0)
        };

        var separator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse(sepColor)),
            Margin = new Thickness(0, 18, 0, 0)
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(20, 12, 20, 16)
        };

        if (buttons == MessageBoxButtons.YesNo)
        {
            var noBtn = new Button
            {
                Content = "No",
                Width = 80, Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse(noBtnFg)),
                Background = new SolidColorBrush(Color.Parse(noBtnBg)),
                BorderBrush = new SolidColorBrush(Color.Parse(noBtnBorder)),
                BorderThickness = new Thickness(1),
            };

            var yesBtn = new Button
            {
                Content = "Yes",
                Width = 80, Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse("#3574C4")),
            };

            noBtn.Click += (_, _) => { result = MessageBoxResult.No; win.Close(); };
            yesBtn.Click += (_, _) => { result = MessageBoxResult.Yes; win.Close(); };
            btnPanel.Children.Add(noBtn);
            btnPanel.Children.Add(yesBtn);
        }
        else
        {
            var okBtn = new Button
            {
                Content = "OK",
                Width = 80, Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.Parse("#3574C4")),
            };
            okBtn.Click += (_, _) => { result = MessageBoxResult.Ok; win.Close(); };
            btnPanel.Children.Add(okBtn);
        }

        var content = new StackPanel
        {
            Background = new SolidColorBrush(Color.Parse(bgColor))
        };
        content.Children.Add(titleText);
        content.Children.Add(msgText);
        content.Children.Add(separator);
        content.Children.Add(btnPanel);

        win.Content = content;
        await win.ShowDialog(parent);
        return result;
    }

    public static async Task<string?> ShowInput(Window parent, string message, string title,
        string defaultValue = "", string? newButtonValue = null)
    {
        string? result = null;
        var isDark = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;

        var bgColor = isDark ? "#1E2030" : "#F0F4F8";
        var titleColor = isDark ? "#CDD6F4" : "#1E3A5F";
        var msgColor = isDark ? "#A6ADC8" : "#444444";
        var sepColor = isDark ? "#3B3F5C" : "#E0E0E0";
        var secBg = isDark ? "#363850" : "#E8EDF2";
        var secFg = isDark ? "#CDD6F4" : "#333333";
        var secBorder = isDark ? "#4A4E6A" : "#C0C0C0";

        var win = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse(titleColor)),
            Margin = new Thickness(20, 16, 20, 0)
        };

        var msgText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse(msgColor)),
            Margin = new Thickness(20, 10, 20, 0)
        };

        var inputRow = new DockPanel { Margin = new Thickness(20, 8, 20, 0) };
        var inputBox = new TextBox { Text = defaultValue, FontSize = 13 };

        if (newButtonValue != null)
        {
            var newBtn = new Button
            {
                Content = "New",
                Width = 50, Height = 30,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse(secFg)),
                Background = new SolidColorBrush(Color.Parse(secBg)),
                BorderBrush = new SolidColorBrush(Color.Parse(secBorder)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(6, 0, 0, 0),
            };
            newBtn.Click += (_, _) =>
            {
                inputBox.Text = newButtonValue;
                inputBox.SelectAll();
                inputBox.Focus();
            };
            DockPanel.SetDock(newBtn, Dock.Right);
            inputRow.Children.Add(newBtn);
        }
        inputRow.Children.Add(inputBox);

        var separator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse(sepColor)),
            Margin = new Thickness(0, 14, 0, 0)
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(20, 12, 20, 16)
        };

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Width = 80, Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse(secFg)),
            Background = new SolidColorBrush(Color.Parse(secBg)),
            BorderBrush = new SolidColorBrush(Color.Parse(secBorder)),
            BorderThickness = new Thickness(1),
        };

        var saveBtn = new Button
        {
            Content = "Save",
            Width = 80, Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = 12,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#3574C4")),
        };

        cancelBtn.Click += (_, _) => { result = null; win.Close(); };
        saveBtn.Click += (_, _) => { result = inputBox.Text?.Trim(); win.Close(); };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(saveBtn);

        var content = new StackPanel
        {
            Background = new SolidColorBrush(Color.Parse(bgColor))
        };
        content.Children.Add(titleText);
        content.Children.Add(msgText);
        content.Children.Add(inputRow);
        content.Children.Add(separator);
        content.Children.Add(btnPanel);

        win.Content = content;
        await win.ShowDialog(parent);
        return result;
    }
}
