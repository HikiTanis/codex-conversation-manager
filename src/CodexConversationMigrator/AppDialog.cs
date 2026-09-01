using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexConversationMigrator;

internal enum AppDialogTone
{
	Info,
	Success,
	Warning,
	Error
}

internal static class AppDialog
{
	private static Action<Window> WindowCaptureForTest;

	internal static Window CreatePaginatedCompletionPreviewForTest()
	{
		Window preview = null;
		WindowCaptureForTest = window => preview = window;
		try
		{
			string warning = MainWindowController.BuildPaginatedImportWarning(2);
			Show(null,
				UiLanguage.IsEnglish ? "Import completed · verification required" : "迁移完成 · 需要验证",
				UiLanguage.IsEnglish ? "Indexing passed; verify paginated history in Codex" : "索引已通过，请验证分页历史",
				warning + "\n\n" + UiLanguage.T("现在重新打开 Codex，再打开迁入后的项目目录并实际打开对应对话。"),
				AppDialogTone.Warning,
				"完成");
		}
		finally
		{
			WindowCaptureForTest = null;
		}
		return preview ?? throw new InvalidOperationException("Unable to create paginated completion preview.");
	}

	public static void Show(Window owner, string title, string heading, string message, AppDialogTone tone, string closeText = "知道了")
	{
		ShowCore(owner, title, heading, message, tone, closeText, null);
	}

	public static bool Confirm(Window owner, string title, string heading, string message, AppDialogTone tone, string confirmText = "继续", string cancelText = "取消")
	{
		return ShowCore(owner, title, heading, message, tone, confirmText, cancelText);
	}

	public static MessageBoxResult ShowCompat(Window owner, string message, string title, MessageBoxButton buttons, MessageBoxImage image)
	{
		AppDialogTone tone = ResolveTone(title, image);
		if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
		{
			return Confirm(owner, title, title, message, tone, "继续", "取消") ? MessageBoxResult.Yes : MessageBoxResult.No;
		}
		Show(owner, title, title, message, tone, "知道了");
		return MessageBoxResult.OK;
	}

	private static AppDialogTone ResolveTone(string title, MessageBoxImage image)
	{
		if (image == MessageBoxImage.Hand || image == MessageBoxImage.Error || image == MessageBoxImage.Stop)
		{
			return AppDialogTone.Error;
		}
		if (image == MessageBoxImage.Exclamation || image == MessageBoxImage.Warning)
		{
			return AppDialogTone.Warning;
		}
		string value = title ?? string.Empty;
		if (value.IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0 ||
			value.IndexOf("完成", StringComparison.OrdinalIgnoreCase) >= 0 ||
			value.IndexOf("已恢复", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return AppDialogTone.Success;
		}
		return AppDialogTone.Info;
	}

	private static bool ShowCore(Window owner, string title, string heading, string message, AppDialogTone tone, string primaryText, string cancelText)
	{
		title = UiLanguage.T(title);
		heading = UiLanguage.T(heading);
		message = UiLanguage.T(message);
		primaryText = UiLanguage.T(primaryText);
		cancelText = UiLanguage.T(cancelText);
		Window dialog = DialogUi.CreateWindow(owner, title, 640.0, 420.0);
		dialog.MinHeight = 330.0;
		dialog.MaxHeight = Math.Max(420.0, (owner?.ActualHeight ?? 720.0) * 0.82);
		if (owner?.Icon != null)
		{
			dialog.Icon = owner.Icon;
		}

		(string accent, string surface, string glyph) = tone switch
		{
			AppDialogTone.Success => ("#0D9F76", "#EAF7F2", "✓"),
			AppDialogTone.Warning => ("#B97819", "#FFF6E8", "!"),
			AppDialogTone.Error => ("#C94E48", "#FFF0EE", "×"),
			_ => ("#397A68", "#EDF6F2", "i")
		};

		Grid root = DialogUi.CreateRoot();
		dialog.Content = root;
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		Grid hero = new Grid { Margin = new Thickness(0.0, 0.0, 0.0, 18.0) };
		hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		Border badge = new Border
		{
			Width = 44.0,
			Height = 44.0,
			CornerRadius = new CornerRadius(14.0),
			Background = DialogUi.Brush(surface),
			BorderBrush = DialogUi.Brush(accent),
			BorderThickness = new Thickness(1.0),
			Child = new TextBlock
			{
				Text = glyph,
				FontSize = 22.0,
				FontWeight = FontWeights.SemiBold,
				Foreground = DialogUi.Brush(accent),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		hero.Children.Add(badge);
		StackPanel headingPanel = new StackPanel { Margin = new Thickness(14.0, 0.0, 0.0, 0.0), VerticalAlignment = VerticalAlignment.Center };
		headingPanel.Children.Add(DialogUi.Text(heading, 21.0, FontWeights.SemiBold, "#20231F"));
		if (!string.Equals(title, heading, StringComparison.OrdinalIgnoreCase))
		{
			TextBlock caption = DialogUi.Text(title, 11.5, FontWeights.Normal, "#7A8079");
			caption.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
			headingPanel.Children.Add(caption);
		}
		Grid.SetColumn(headingPanel, 1);
		hero.Children.Add(headingPanel);
		root.Children.Add(hero);

		Border messageSurface = new Border
		{
			Background = DialogUi.Brush("#FFFEFC"),
			BorderBrush = DialogUi.Brush("#DDE0D9"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(13.0),
			Padding = new Thickness(16.0, 14.0, 16.0, 14.0)
		};
		TextBlock body = DialogUi.Text(message, 12.5, FontWeights.Normal, "#4F5650");
		body.TextWrapping = TextWrapping.Wrap;
		body.LineHeight = 20.0;
		messageSurface.Child = new ScrollViewer
		{
			Content = body,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		Grid.SetRow(messageSurface, 1);
		root.Children.Add(messageSurface);

		StackPanel buttons = DialogUi.ButtonBar();
		Grid.SetRow(buttons, 2);
		root.Children.Add(buttons);
		bool result = false;
		if (!string.IsNullOrWhiteSpace(cancelText))
		{
			Button cancel = DialogUi.Button(cancelText, primary: false);
			cancel.IsCancel = true;
			cancel.Click += delegate { dialog.DialogResult = false; };
			buttons.Children.Add(cancel);
		}
		Button primary = DialogUi.Button(primaryText, primary: true);
		primary.IsDefault = true;
		primary.Click += delegate
		{
			result = true;
			dialog.DialogResult = true;
		};
		buttons.Children.Add(primary);

		if (WindowCaptureForTest != null)
		{
			WindowCaptureForTest(dialog);
			return result;
		}
		dialog.ShowDialog();
		return result;
	}
}
