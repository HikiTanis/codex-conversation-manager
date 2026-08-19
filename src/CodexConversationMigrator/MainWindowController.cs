using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace CodexConversationMigrator;

internal sealed class MainWindowController
{
	private sealed class BackupProjectSelection
	{
		public ProjectGroup Project { get; set; }

		public List<SessionInfo> Sessions { get; set; }
	}

	private sealed class ImportProjectContext
	{
		public PackProject Project { get; set; }

		public string TargetPath { get; set; }

		public List<string> BundlePaths { get; } = new List<string>();

		public List<ConversationImportPlan> ImportPlans { get; } = new List<ConversationImportPlan>();

		public string ProjectArchivePath { get; set; }

		public ProjectRestorePlan Plan { get; set; }

		public ProjectRestoreResult RestoreResult { get; set; }
	}

	private readonly Window window;

	private readonly System.Windows.Controls.TextBox cctPathBox;

	private readonly System.Windows.Controls.TextBox backupFolderBox;

	private readonly TextBlock cctStatusText;

	private readonly System.Windows.Controls.RadioButton projectBackupModeRadio;

	private readonly System.Windows.Controls.RadioButton conversationBackupModeRadio;

	private readonly TextBlock backupModeHelpText;

	private readonly FrameworkElement projectSelectionTools;

	private readonly FrameworkElement sessionSelectionTools;

	private readonly TextBlock projectPaneTitle;

	private readonly TextBlock projectPaneSubtitle;

	private readonly TextBlock sessionModeHint;

	private readonly TextBlock selectionHelpText;

	private readonly System.Windows.Controls.ListBox projectList;

	private readonly System.Windows.Controls.ListBox sessionList;

	private readonly System.Windows.Controls.TextBox searchBox;

	private readonly System.Windows.Controls.RadioButton mainSessionsTabRadio;

	private readonly System.Windows.Controls.RadioButton subagentSessionsTabRadio;

	private readonly System.Windows.Controls.CheckBox fullFidelityCheck;

	private readonly TextBlock projectTitleText;

	private readonly TextBlock projectPathText;

	private readonly TextBlock projectMetaText;

	private readonly TextBlock projectSizeText;

	private readonly TextBlock selectedCountText;

	private readonly TextBlock emptySessionsText;

	private readonly TextBlock statusText;

	private readonly Ellipse statusDot;

	private readonly System.Windows.Controls.ProgressBar busyProgress;

	private readonly Grid backupPage;

	private readonly Grid importPage;

	private readonly Grid importWorkflowGrid;

	private readonly Border importActionBar;

	private readonly Border backupTabIndicator;

	private readonly Border importTabIndicator;

	private readonly System.Windows.Controls.Button languageButton;

	private readonly System.Windows.Controls.Button backupTabButton;

	private readonly System.Windows.Controls.Button importTabButton;

	private readonly System.Windows.Controls.Button refreshButton;

	private readonly System.Windows.Controls.Button trashButton;

	private readonly System.Windows.Controls.Button browseCctButton;

	private readonly System.Windows.Controls.Button browseBackupFolderButton;

	private readonly System.Windows.Controls.Button selectAllProjectsButton;

	private readonly System.Windows.Controls.Button clearProjectsButton;

	private readonly System.Windows.Controls.Button toggleSessionSelectionButton;

	private readonly System.Windows.Controls.Button deleteSelectedSessionsButton;

	private readonly System.Windows.Controls.Button copyProjectPathButton;

	private readonly System.Windows.Controls.Button backupSelectedButton;

	private readonly System.Windows.Controls.Button backupProjectFilesButton;

	private readonly System.Windows.Controls.Button inspectButton;

	private readonly System.Windows.Controls.Button importButton;

	private readonly System.Windows.Controls.Button browsePackageButton;

	private readonly System.Windows.Controls.Button browseTargetButton;

	private readonly Border importProgressPanel;

	private readonly TextBlock importStageText;

	private readonly TextBlock importStageDetailText;

	private readonly TextBlock importElapsedText;

	private readonly System.Windows.Controls.ProgressBar importStageProgress;

	private readonly System.Windows.Controls.TextBox packagePathBox;

	private readonly System.Windows.Controls.TextBox targetPathBox;

	private readonly TextBlock targetPathLabel;

	private readonly TextBlock targetPathHelpText;

	private readonly System.Windows.Controls.CheckBox mapPathCheck;

	private readonly Border projectRestorePanel;

	private readonly System.Windows.Controls.CheckBox restoreProjectFilesCheck;

	private readonly System.Windows.Controls.ComboBox projectConflictCombo;

	private readonly TextBlock projectRestoreHelpText;

	private readonly System.Windows.Controls.RadioButton mergeModeRadio;


	private readonly System.Windows.Controls.RadioButton copyModeRadio;

	private readonly TextBlock importModeHelpText;

	private readonly TextBlock packageSummaryText;

	private readonly TextBlock packageProjectText;

	private readonly System.Windows.Controls.TextBox importLog;

	private readonly Grid conversationOverlay;

	private readonly TextBlock conversationTitleText;

	private readonly TextBlock conversationMetaText;

	private readonly System.Windows.Controls.ListBox conversationList;

	private readonly Canvas conversationCanvas;

	private readonly FrameworkElement conversationDialogHost;

	private readonly Border conversationHeader;

	private readonly System.Windows.Controls.Button conversationMinimizeButton;

	private readonly System.Windows.Controls.Button conversationMaximizeButton;

	private readonly System.Windows.Controls.Button conversationCloseButton;

	private readonly System.Windows.Controls.Button copyThreadIdButton;

	private readonly FrameworkElement maximizeGlyph;

	private readonly FrameworkElement restoreGlyph;

	private readonly FrameworkElement conversationMaximizeGlyph;

	private readonly FrameworkElement conversationRestoreGlyph;

	private List<ProjectGroup> projects = new List<ProjectGroup>();

	private ProjectGroup selectedProject;

	private ICollectionView sessionView;

	private PackManifest loadedManifest;

	private bool loadedIsRawBundle;

	private string loadedPackagePath = string.Empty;

	private bool isBusy;

	private DateTime importStartedAt;


	private bool projectBackupMode = true;

	private bool showSubagentSessions;

	private string previewedThreadId = string.Empty;

	private bool conversationDialogInitialized;

	private bool conversationDialogMaximized;

	private bool conversationHeaderDragging;

	private Point conversationHeaderDragStart;

	private double conversationHeaderStartLeft;

	private double conversationHeaderStartTop;

	private double conversationRestoreLeft;

	private double conversationRestoreTop;

	private double conversationRestoreWidth = 940.0;

	private double conversationRestoreHeight = 650.0;

	private readonly TaskCompletionSource<bool> initialLoadCompletion = new TaskCompletionSource<bool>();

	public Window Window => window;

	public Task InitialLoadTask => initialLoadCompletion.Task;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	public MainWindowController(string preferredCct)
	{
		string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexConversationMigrator.xaml");
		if (!File.Exists(text))
		{
			throw new FileNotFoundException("界面文件不存在。", text);
		}
		string localizedXaml = UiLanguage.LoadXaml(text);
		window = (Window)XamlReader.Parse(localizedXaml);
		window.Tag = this;
		cctPathBox = Find<System.Windows.Controls.TextBox>("CctPathBox");
		backupFolderBox = Find<System.Windows.Controls.TextBox>("BackupFolderBox");
		cctStatusText = Find<TextBlock>("CctStatusText");
		projectBackupModeRadio = Find<System.Windows.Controls.RadioButton>("ProjectBackupModeRadio");
		conversationBackupModeRadio = Find<System.Windows.Controls.RadioButton>("ConversationBackupModeRadio");
		backupModeHelpText = Find<TextBlock>("BackupModeHelpText");
		projectSelectionTools = Find<FrameworkElement>("ProjectSelectionTools");
		sessionSelectionTools = Find<FrameworkElement>("SessionSelectionTools");
		projectPaneTitle = Find<TextBlock>("ProjectPaneTitle");
		projectPaneSubtitle = Find<TextBlock>("ProjectPaneSubtitle");
		sessionModeHint = Find<TextBlock>("SessionModeHint");
		selectionHelpText = Find<TextBlock>("SelectionHelpText");
		projectList = Find<System.Windows.Controls.ListBox>("ProjectList");
		sessionList = Find<System.Windows.Controls.ListBox>("SessionList");
		searchBox = Find<System.Windows.Controls.TextBox>("SearchBox");
		mainSessionsTabRadio = Find<System.Windows.Controls.RadioButton>("MainSessionsTabRadio");
		subagentSessionsTabRadio = Find<System.Windows.Controls.RadioButton>("SubagentSessionsTabRadio");
		fullFidelityCheck = Find<System.Windows.Controls.CheckBox>("FullFidelityCheck");
		projectTitleText = Find<TextBlock>("ProjectTitleText");
		projectPathText = Find<TextBlock>("ProjectPathText");
		projectMetaText = Find<TextBlock>("ProjectMetaText");
		projectSizeText = Find<TextBlock>("ProjectSizeText");
		selectedCountText = Find<TextBlock>("SelectedCountText");
		emptySessionsText = Find<TextBlock>("EmptySessionsText");
		statusText = Find<TextBlock>("StatusText");
		statusDot = Find<Ellipse>("StatusDot");
		busyProgress = Find<System.Windows.Controls.ProgressBar>("BusyProgress");
		backupPage = Find<Grid>("BackupPage");
		importPage = Find<Grid>("ImportPage");
		importWorkflowGrid = Find<Grid>("ImportWorkflowGrid");
		importActionBar = Find<Border>("ImportActionBar");
		backupTabIndicator = Find<Border>("BackupTabIndicator");
		importTabIndicator = Find<Border>("ImportTabIndicator");
		backupTabButton = Find<System.Windows.Controls.Button>("BackupTabButton");
		importTabButton = Find<System.Windows.Controls.Button>("ImportTabButton");
		refreshButton = Find<System.Windows.Controls.Button>("RefreshButton");
		trashButton = Find<System.Windows.Controls.Button>("TrashButton");
		browseCctButton = Find<System.Windows.Controls.Button>("BrowseCctButton");
		browseBackupFolderButton = Find<System.Windows.Controls.Button>("BrowseBackupFolderButton");
		selectAllProjectsButton = Find<System.Windows.Controls.Button>("SelectAllProjectsButton");
		languageButton = Find<System.Windows.Controls.Button>("LanguageButton");
		clearProjectsButton = Find<System.Windows.Controls.Button>("ClearProjectsButton");
		toggleSessionSelectionButton = Find<System.Windows.Controls.Button>("ToggleSessionSelectionButton");
		deleteSelectedSessionsButton = Find<System.Windows.Controls.Button>("DeleteSelectedSessionsButton");
		copyProjectPathButton = Find<System.Windows.Controls.Button>("CopyProjectPathButton");
		backupSelectedButton = Find<System.Windows.Controls.Button>("BackupSelectedButton");
		backupProjectFilesButton = Find<System.Windows.Controls.Button>("BackupProjectFilesButton");
		inspectButton = Find<System.Windows.Controls.Button>("InspectButton");
		importButton = Find<System.Windows.Controls.Button>("ImportButton");
		packagePathBox = Find<System.Windows.Controls.TextBox>("PackagePathBox");
		browsePackageButton = Find<System.Windows.Controls.Button>("BrowsePackageButton");
		browseTargetButton = Find<System.Windows.Controls.Button>("BrowseTargetButton");
		importProgressPanel = Find<Border>("ImportProgressPanel");
		importStageText = Find<TextBlock>("ImportStageText");
		importStageDetailText = Find<TextBlock>("ImportStageDetailText");
		importElapsedText = Find<TextBlock>("ImportElapsedText");
		importStageProgress = Find<System.Windows.Controls.ProgressBar>("ImportStageProgress");
		targetPathBox = Find<System.Windows.Controls.TextBox>("TargetPathBox");
		targetPathLabel = Find<TextBlock>("TargetPathLabel");
		targetPathHelpText = Find<TextBlock>("TargetPathHelpText");
		mapPathCheck = Find<System.Windows.Controls.CheckBox>("MapPathCheck");
		projectRestorePanel = Find<Border>("ProjectRestorePanel");
		restoreProjectFilesCheck = Find<System.Windows.Controls.CheckBox>("RestoreProjectFilesCheck");
		projectConflictCombo = Find<System.Windows.Controls.ComboBox>("ProjectConflictCombo");
		projectRestoreHelpText = Find<TextBlock>("ProjectRestoreHelpText");
		mergeModeRadio = Find<System.Windows.Controls.RadioButton>("MergeModeRadio");
		copyModeRadio = Find<System.Windows.Controls.RadioButton>("CopyModeRadio");
		importModeHelpText = Find<TextBlock>("ImportModeHelpText");
		packageSummaryText = Find<TextBlock>("PackageSummaryText");
		packageProjectText = Find<TextBlock>("PackageProjectText");
		importLog = Find<System.Windows.Controls.TextBox>("ImportLog");
		conversationOverlay = Find<Grid>("ConversationOverlay");
		conversationTitleText = Find<TextBlock>("ConversationTitleText");
		conversationMetaText = Find<TextBlock>("ConversationMetaText");
		conversationList = Find<System.Windows.Controls.ListBox>("ConversationList");
		conversationCanvas = Find<Canvas>("ConversationCanvas");
		conversationDialogHost = Find<FrameworkElement>("ConversationDialogHost");
		conversationHeader = Find<Border>("ConversationHeader");
		conversationMinimizeButton = Find<System.Windows.Controls.Button>("ConversationMinimizeButton");
		conversationMaximizeButton = Find<System.Windows.Controls.Button>("ConversationMaximizeButton");
		conversationCloseButton = Find<System.Windows.Controls.Button>("ConversationCloseButton");
		copyThreadIdButton = Find<System.Windows.Controls.Button>("CopyThreadIdButton");
		maximizeGlyph = Find<FrameworkElement>("MaximizeGlyph");
		restoreGlyph = Find<FrameworkElement>("RestoreGlyph");
		conversationMaximizeGlyph = Find<FrameworkElement>("ConversationMaximizeGlyph");
		conversationRestoreGlyph = Find<FrameworkElement>("ConversationRestoreGlyph");
		cctPathBox.Text = CctRunner.ResolveCctPath(preferredCct);
		backupFolderBox.Text = DefaultBackupFolder();
		languageButton.Content = UiLanguage.IsEnglish ? "中文" : "EN";
		languageButton.ToolTip = UiLanguage.IsEnglish ? "Switch to Chinese" : "切换到英文";
		WireEvents();
		ShowBackupPage();
	}

	public bool SelectProjectForTest(string displayName)
	{
		ProjectGroup projectGroup = string.IsNullOrWhiteSpace(displayName)
			? projects.FirstOrDefault((ProjectGroup x) => x.MainCount > 0 && x.InternalCount > 0) ?? projects.FirstOrDefault((ProjectGroup x) => x.MainCount > 0) ?? projects.FirstOrDefault()
			: projects.FirstOrDefault((ProjectGroup x) => string.Equals(x.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
		if (projectGroup == null)
		{
			return false;
		}
		projectList.SelectedItem = projectGroup;
		projectList.ScrollIntoView(projectGroup);
		return ReferenceEquals(selectedProject, projectGroup) && string.Equals(projectTitleText.Text, projectGroup.DisplayName, StringComparison.OrdinalIgnoreCase);
	}

	public bool ShowSubagentViewForTest(string displayName)
	{
		if (!SelectProjectForTest(displayName))
		{
			return false;
		}
		subagentSessionsTabRadio.IsChecked = true;
		UpdateSessionTypeView();
		List<SessionInfo> visible = sessionView?.Cast<object>().OfType<SessionInfo>().ToList() ?? new List<SessionInfo>();
		return visible.Count > 0 && visible.All((SessionInfo session) => session.IsSubagent) && visible.All((SessionInfo session) => !string.IsNullOrWhiteSpace(session.DisplayPath) && session.SizeBytes > 0L);
	}

	public bool ShowMainSessionViewForTest(string displayName)
	{
		if (!SelectProjectForTest(displayName))
		{
			return false;
		}
		mainSessionsTabRadio.IsChecked = true;
		UpdateSessionTypeView();
		List<SessionInfo> visible = sessionView?.Cast<object>().OfType<SessionInfo>().ToList() ?? new List<SessionInfo>();
		return visible.Count > 0 && visible.All((SessionInfo session) => !session.IsSubagent);
	}

	public bool TestMainSelectionToggleForTest()
	{
		return !showSubagentSessions && TestCurrentSessionSelectionToggleForTest();
	}

	public bool TestSubagentSelectionToggleForTest()
	{
		return showSubagentSessions && TestCurrentSessionSelectionToggleForTest();
	}

	private bool TestCurrentSessionSelectionToggleForTest()
	{
		List<SessionInfo> sessions = CurrentSessionTypeItems();
		if (sessions.Count == 0)
		{
			return false;
		}
		foreach (SessionInfo session in sessions)
		{
			session.IsSelected = false;
		}
		UpdateSessionSelectionControls();
		ToggleSessionSelection();
		bool allSelected = sessions.All((SessionInfo session) => session.IsSelected) && string.Equals(Convert.ToString(toggleSessionSelectionButton.Content), UiLanguage.T("全不选"), StringComparison.Ordinal);
		ToggleSessionSelection();
		bool noneSelected = sessions.All((SessionInfo session) => !session.IsSelected) && string.Equals(Convert.ToString(toggleSessionSelectionButton.Content), UiLanguage.T("全选"), StringComparison.Ordinal);
		sessions[0].IsSelected = true;
		UpdateSessionSelectionControls();
		bool selectedDeleteReady = deleteSelectedSessionsButton.IsEnabled && (Convert.ToString(deleteSelectedSessionsButton.Content) ?? string.Empty).IndexOf("1", StringComparison.Ordinal) >= 0;
		return allSelected && noneSelected && selectedDeleteReady;
	}

	public async Task<bool> ShowFirstSubagentConversationForTest(string displayName)
	{
		if (!ShowSubagentViewForTest(displayName))
		{
			return false;
		}
		sessionList.UpdateLayout();
		SessionInfo session = sessionView.Cast<object>().OfType<SessionInfo>().FirstOrDefault();
		if (session == null)
		{
			return false;
		}
		sessionList.ScrollIntoView(session);
		sessionList.UpdateLayout();
		ListBoxItem item = sessionList.ItemContainerGenerator.ContainerFromItem(session) as ListBoxItem;
		System.Windows.Controls.Button viewButton = FindNamedButton(item, "ViewSessionButton");
		if (viewButton == null)
		{
			return false;
		}
		viewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, viewButton));
		for (int i = 0; i < 40; i++)
		{
			if (conversationOverlay.Visibility == Visibility.Visible && conversationList.ItemsSource != null && (conversationMetaText.Text ?? string.Empty).IndexOf(UiLanguage.IsEnglish ? "Loading" : "正在", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return true;
			}
			await Task.Delay(50);
		}
		return false;
	}

	public async Task<bool> WaitForSelectedProjectStorageForTest()
	{
		for (int i = 0; i < 80; i++)
		{
			if (selectedProject != null && selectedProject.StorageScanStarted && (selectedProject.ProjectStorageSummary ?? string.Empty).IndexOf(UiLanguage.IsEnglish ? "measuring" : "正在统计", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return true;
			}
			await Task.Delay(50);
		}
		return false;
	}

	public void ShowImportForTest()
	{
		ShowImportPage();
	}

	public void ShowImportProgressForTest()
	{
		ShowImportPage();
		BeginImportProgress(dryRun: false);
		UpdateImportStage("3 / 4 · 导入对话", "正在导入第 2/4 个对话包；文件处理在后台执行，界面仍可响应。");
	}

	public bool TestImportLayoutForTest()
	{
		window.UpdateLayout();
		projectConflictCombo.BringIntoView();
		window.UpdateLayout();
		projectConflictCombo.ApplyTemplate();
		importWorkflowGrid.UpdateLayout();
		Point buttonBottom = importButton.TranslatePoint(new Point(0.0, importButton.ActualHeight), importActionBar);
		bool actionButtonsFit = importActionBar.ActualHeight >= 72.0 &&
			inspectButton.ActualHeight >= 37.0 &&
			importButton.ActualHeight >= 37.0 &&
			buttonBottom.Y <= importActionBar.ActualHeight - importActionBar.Padding.Bottom + 0.5;
		bool customConflictField = projectConflictCombo.ActualHeight >= 39.0 &&
			projectConflictCombo.Template != null &&
			projectConflictCombo.Template.FindName("FieldSurface", projectConflictCombo) != null;
		Popup conflictPopup = projectConflictCombo.Template?.FindName("PART_Popup", projectConflictCombo) as Popup;
		projectConflictCombo.IsDropDownOpen = true;
		window.UpdateLayout();
		bool popupWorks = conflictPopup != null && conflictPopup.IsOpen;
		projectConflictCombo.IsDropDownOpen = false;
		if (!actionButtonsFit || !customConflictField || !popupWorks)
		{
			throw new InvalidOperationException($"导入布局诊断：ActionBar={importActionBar.ActualHeight:0.##}, Inspect={inspectButton.ActualHeight:0.##}, Import={importButton.ActualHeight:0.##}, ButtonBottom={buttonBottom.Y:0.##}, PaddingBottom={importActionBar.Padding.Bottom:0.##}, Combo={projectConflictCombo.ActualHeight:0.##}, CustomTemplate={customConflictField}, Popup={popupWorks}");
		}
		return true;
	}

	public bool SelectProjectBackupForTest(string displayName)
	{
		projectBackupModeRadio.IsChecked = true;
		UpdateBackupMode();
		if (!SelectProjectForTest(displayName) || selectedProject == null)
		{
			return false;
		}
		selectedProject.IsBatchSelected = true;
		UpdateSelectedCount();
		return true;
	}

	public bool SelectConversationBackupForTest(string displayName)
	{
		conversationBackupModeRadio.IsChecked = true;
		UpdateBackupMode();
		if (!SelectProjectForTest(displayName))
		{
			return false;
		}
		SessionInfo session = ((sessionView == null) ? null : sessionView.Cast<object>().OfType<SessionInfo>().FirstOrDefault((SessionInfo x) => !x.IsSubagent));
		if (session == null)
		{
			return false;
		}
		session.IsSelected = true;
		UpdateSelectedCount();
		return true;
	}

	public void ShowProjectRestoreForTest()
	{
		loadedManifest = new PackManifest
		{
			schema = 4,
			mode = "batch_projects_with_files",
			projects = new List<PackProject>
			{
				new PackProject
				{
					project_key = "project-001",
					source_project = "D:\\OldComputer\\Projects\\example-app",
					source_project_name = "example-app",
					target_folder = "example-app",
					project_payload = new ProjectPayloadInfo
					{
						archive_file = "projects/project-001/project-files.zip",
						file_count = 128,
						directory_count = 24,
						uncompressed_bytes = 12582912L,
						sha256 = new string('0', 64)
					}
				},
				new PackProject
				{
					project_key = "project-002",
					source_project = "D:\\OldComputer\\Projects\\data-service",
					source_project_name = "data-service",
					target_folder = "data-service",
					project_payload = new ProjectPayloadInfo
					{
						archive_file = "projects/project-002/project-files.zip",
						file_count = 76,
						directory_count = 15,
						uncompressed_bytes = 7340032L,
						sha256 = new string('1', 64)
					}
				}
			}
		};
		packageSummaryText.Text = UiLanguage.T("2 个项目 + 对话完整包 · 11 个主对话");
		packageProjectText.Text = UiLanguage.T("项目：example-app、data-service\n项目文件：204 个（19 MB）\n创建时间：2026-08-04 15:20");
		targetPathBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Codex 项目迁入-20260804");
		restoreProjectFilesCheck.IsChecked = true;
		projectConflictCombo.SelectedIndex = 0;
		UpdateProjectRestoreControls();
		projectConflictCombo.BringIntoView();
	}

	public bool TestImportModeHelpForTest()
	{
		copyModeRadio.IsChecked = true;
		bool copyHelp = (importModeHelpText.Text ?? string.Empty).Contains(UiLanguage.IsEnglish ? "new Thread ID" : "全新 Thread ID");
		mergeModeRadio.IsChecked = true;
		bool mergeHelp = (importModeHelpText.Text ?? string.Empty).Contains(UiLanguage.IsEnglish ? "original ID" : "原始编号");
		return copyHelp && mergeHelp;
	}


	public async Task<bool> ShowFirstConversationForTest(string projectName)
	{
		if (!SelectProjectForTest(projectName) || selectedProject == null)
		{
			return false;
		}
		sessionList.UpdateLayout();
		SessionInfo session = ((sessionView == null) ? null : sessionView.Cast<object>().OfType<SessionInfo>().FirstOrDefault((SessionInfo x) => !x.IsSubagent));
		if (session == null)
		{
			return false;
		}
		sessionList.ScrollIntoView(session);
		sessionList.UpdateLayout();
		ListBoxItem item = sessionList.ItemContainerGenerator.ContainerFromItem(session) as ListBoxItem;
		System.Windows.Controls.Button viewButton = FindNamedButton(item, "ViewSessionButton");
		if (viewButton == null)
		{
			return false;
		}
		viewButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, viewButton));
		for (int i = 0; i < 40; i++)
		{
			if (conversationOverlay.Visibility == Visibility.Visible && conversationList.ItemsSource != null && (conversationMetaText.Text ?? string.Empty).IndexOf(UiLanguage.IsEnglish ? "Loading" : "正在", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return true;
			}
			await Task.Delay(50);
		}
		return false;
	}

	public bool ResizeConversationDialogForTest()
	{
		if (conversationOverlay.Visibility != Visibility.Visible)
		{
			return false;
		}
		InitializeConversationDialog();
		double actualWidth = conversationDialogHost.ActualWidth;
		double actualHeight = conversationDialogHost.ActualHeight;
		Thumb thumb = Find<Thumb>("ConversationResizeBottomRight");
		DragDeltaEventArgs e = new DragDeltaEventArgs(64.0, 42.0);
		e.RoutedEvent = Thumb.DragDeltaEvent;
		DragDeltaEventArgs e2 = e;
		thumb.RaiseEvent(e2);
		conversationDialogHost.UpdateLayout();
		if (conversationDialogHost.ActualWidth > actualWidth + 40.0)
		{
			return conversationDialogHost.ActualHeight > actualHeight + 25.0;
		}
		return false;
	}

	public bool MaximizeConversationForTest()
	{
		if (conversationOverlay.Visibility != Visibility.Visible)
		{
			return false;
		}
		if (!conversationDialogMaximized)
		{
			ToggleConversationDialogMaximize();
		}
		return conversationDialogMaximized && conversationRestoreGlyph.Visibility == Visibility.Visible;
	}

	private static System.Windows.Controls.Button FindNamedButton(DependencyObject root, string name)
	{
		if (root == null)
		{
			return null;
		}
		if (root is System.Windows.Controls.Button button && string.Equals(button.Name, name, StringComparison.Ordinal))
		{
			return button;
		}
		int num = 0;
		try
		{
			num = VisualTreeHelper.GetChildrenCount(root);
		}
		catch
		{
		}
		for (int i = 0; i < num; i++)
		{
			System.Windows.Controls.Button button2 = FindNamedButton(VisualTreeHelper.GetChild(root, i), name);
			if (button2 != null)
			{
				return button2;
			}
		}
		return null;
	}

	private T Find<T>(string name) where T : class
	{
		if (!(window.FindName(name) is T result))
		{
			throw new InvalidOperationException("界面缺少控件：" + name);
		}
		return result;
	}

	private void WireEvents()
	{
		Find<System.Windows.Controls.Button>("CloseButton").Click += delegate
		{
			window.Close();
		};
		Find<System.Windows.Controls.Button>("MinimizeButton").Click += delegate
		{
			window.WindowState = WindowState.Minimized;
		};
		Find<System.Windows.Controls.Button>("MaximizeButton").Click += delegate
		{
			ToggleMaximize();
		};
		window.StateChanged += delegate
		{
			UpdateMaximizeGlyph();
		};
		window.SourceInitialized += delegate
		{
			PreferRoundedWindow();
		};
		backupTabButton.Click += delegate
		{
			ShowBackupPage();
		};
		importTabButton.Click += delegate
		{
			ShowImportPage();
		};
		refreshButton.Click += async delegate
		{
			await RefreshDataAsync();
		};
		trashButton.Click += async delegate
		{
			await ShowTrashManagerAsync();
		};
		languageButton.Click += delegate
		{
			SwitchLanguage();
		};
		browseCctButton.Click += async delegate
		{
			await BrowseCctAsync();
		};
		browseBackupFolderButton.Click += delegate
		{
			BrowseBackupFolder();
		};
		projectBackupModeRadio.Checked += delegate
		{
			UpdateBackupMode();
		};
		conversationBackupModeRadio.Checked += delegate
		{
			UpdateBackupMode();
		};
		selectAllProjectsButton.Click += delegate
		{
			SetProjectSelection(value: true);
		};
		clearProjectsButton.Click += delegate
		{
			SetProjectSelection(value: false);
		};
		projectList.SelectionChanged += ProjectListSelectionChanged;
		projectList.PreviewMouseLeftButtonDown += ProjectListPreviewMouseLeftButtonDown;
		sessionList.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(SessionActionButtonClick), handledEventsToo: true);
		searchBox.TextChanged += delegate
		{
			RefreshSessionView();
		};
		mainSessionsTabRadio.Checked += delegate
		{
			showSubagentSessions = false;
			UpdateSessionTypeView();
		};
		subagentSessionsTabRadio.Checked += delegate
		{
			showSubagentSessions = true;
			UpdateSessionTypeView();
		};
		toggleSessionSelectionButton.Click += delegate
		{
			ToggleSessionSelection();
		};
		deleteSelectedSessionsButton.Click += async delegate
		{
			await DeleteSelectedSessionsAsync();
		};
		copyProjectPathButton.Click += delegate
		{
			CopySelectedProjectPath();
		};
		backupSelectedButton.Click += async delegate
		{
			await BackupSelectedAsync();
		};
		backupProjectFilesButton.Click += async delegate
		{
			await BackupProjectWithFilesAsync();
		};
		browsePackageButton.Click += async delegate
		{
			await BrowsePackageAsync();
		};
		browseTargetButton.Click += delegate
		{
			BrowseTarget();
		};
		inspectButton.Click += async delegate
		{
			await ImportPackageAsync(dryRun: true);
		};
		importButton.Click += async delegate
		{
			await ImportPackageAsync(dryRun: false);
		};
		mergeModeRadio.Checked += delegate
		{
			UpdateImportModeHelp();
		};
		copyModeRadio.Checked += delegate
		{
			UpdateImportModeHelp();
		};
		restoreProjectFilesCheck.Checked += delegate
		{
			UpdateProjectRestoreControls();
		};
		restoreProjectFilesCheck.Unchecked += delegate
		{
			UpdateProjectRestoreControls();
		};
		projectConflictCombo.SelectionChanged += delegate
		{
			UpdateProjectRestoreControls();
		};
		UpdateImportModeHelp();
		UpdateProjectRestoreControls();
		conversationMinimizeButton.Click += delegate
		{
			window.WindowState = WindowState.Minimized;
		};
		conversationMaximizeButton.Click += delegate
		{
			ToggleConversationDialogMaximize();
		};
		conversationCloseButton.Click += delegate
		{
			HideConversation();
		};
		conversationHeader.MouseLeftButtonDown += ConversationHeaderMouseLeftButtonDown;
		conversationHeader.MouseMove += ConversationHeaderMouseMove;
		conversationHeader.MouseLeftButtonUp += ConversationHeaderMouseLeftButtonUp;
		conversationHeader.LostMouseCapture += delegate
		{
			conversationHeaderDragging = false;
		};
		conversationCanvas.SizeChanged += delegate
		{
			EnsureConversationDialogFits();
		};
		WireConversationResizeThumb("ConversationResizeLeft", -1, 0);
		WireConversationResizeThumb("ConversationResizeRight", 1, 0);
		WireConversationResizeThumb("ConversationResizeTop", 0, -1);
		WireConversationResizeThumb("ConversationResizeBottom", 0, 1);
		WireConversationResizeThumb("ConversationResizeTopLeft", -1, -1);
		WireConversationResizeThumb("ConversationResizeTopRight", 1, -1);
		WireConversationResizeThumb("ConversationResizeBottomLeft", -1, 1);
		WireConversationResizeThumb("ConversationResizeBottomRight", 1, 1);
		copyThreadIdButton.Click += delegate
		{
			CopyPreviewedThreadId();
		};
		UpdateBackupMode();
		UpdateSessionTypeView();
		UpdateConversationMaximizeGlyph();
		window.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape && conversationOverlay.Visibility == Visibility.Visible)
			{
				HideConversation();
				e.Handled = true;
			}
		};
		window.Loaded += async delegate
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				await RefreshDataAsync();
			}
			finally
			{
				initialLoadCompletion.TrySetResult(result: true);
			}
		};
	}

	private void PreferRoundedWindow()
	{
		try
		{
			IntPtr handle = new WindowInteropHelper(window).Handle;
			int value = 2;
			DwmSetWindowAttribute(handle, 33, ref value, 4);
		}
		catch
		{
		}
	}
	private void SwitchLanguage()
	{
		if (isBusy)
		{
			return;
		}
		AppLanguage previous = UiLanguage.Current;
		UiLanguage.SetAndSave(UiLanguage.IsEnglish ? AppLanguage.Chinese : AppLanguage.English);
		try
		{
			MainWindowController replacementController = new MainWindowController(cctPathBox.Text);
			Window replacement = replacementController.Window;
			replacement.Width = window.ActualWidth > 0.0 ? window.ActualWidth : window.Width;
			replacement.Height = window.ActualHeight > 0.0 ? window.ActualHeight : window.Height;
			if (window.WindowState == WindowState.Normal)
			{
				replacement.WindowStartupLocation = WindowStartupLocation.Manual;
				replacement.Left = window.Left;
				replacement.Top = window.Top;
			}
			replacementController.backupFolderBox.Text = backupFolderBox.Text;
			replacementController.packagePathBox.Text = packagePathBox.Text;
			replacementController.targetPathBox.Text = targetPathBox.Text;
			replacementController.searchBox.Text = searchBox.Text;
			if (importPage.Visibility == Visibility.Visible)
			{
				replacementController.ShowImportPage();
			}
			System.Windows.Application.Current.MainWindow = replacement;
			replacement.Show();
			if (window.WindowState == WindowState.Maximized)
			{
				replacement.WindowState = WindowState.Maximized;
			}
			window.Close();
		}
		catch (Exception ex)
		{
			UiLanguage.SetAndSave(previous);
			AppDialog.Show(window, "切换语言失败", "无法重新加载界面", ex.Message, AppDialogTone.Error);
		}
	}

	private void ToggleMaximize()
	{
		window.WindowState = ((window.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void UpdateMaximizeGlyph()
	{
		bool maximized = window.WindowState == WindowState.Maximized;
		maximizeGlyph.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
		restoreGlyph.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
		Find<System.Windows.Controls.Button>("MaximizeButton").ToolTip = UiLanguage.T(maximized ? "还原" : "最大化");
	}

	private void WireConversationResizeThumb(string name, int horizontalEdge, int verticalEdge)
	{
		Thumb thumb = Find<Thumb>(name);
		thumb.DragStarted += delegate
		{
			conversationDialogMaximized = false;
			UpdateConversationMaximizeGlyph();
		};
		thumb.DragDelta += delegate(object sender, DragDeltaEventArgs e)
		{
			ResizeConversationDialog(horizontalEdge, verticalEdge, e.HorizontalChange, e.VerticalChange);
		};
	}

	private void ConversationHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left && ResolveActionButton(e.OriginalSource as DependencyObject) == null)
		{
			if (e.ClickCount == 2)
			{
				ToggleConversationDialogMaximize();
				e.Handled = true;
			}
			else if (!conversationDialogMaximized)
			{
				conversationHeaderDragging = true;
				conversationHeaderDragStart = e.GetPosition(conversationCanvas);
				conversationHeaderStartLeft = DialogLeft();
				conversationHeaderStartTop = DialogTop();
				conversationHeader.CaptureMouse();
				e.Handled = true;
			}
		}
	}

	private void ConversationHeaderMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (conversationHeaderDragging && e.LeftButton == MouseButtonState.Pressed)
		{
			Point position = e.GetPosition(conversationCanvas);
			double value = conversationHeaderStartLeft + position.X - conversationHeaderDragStart.X;
			double value2 = conversationHeaderStartTop + position.Y - conversationHeaderDragStart.Y;
			Canvas.SetLeft(conversationDialogHost, Clamp(value, 0.0, Math.Max(0.0, conversationCanvas.ActualWidth - conversationDialogHost.ActualWidth)));
			Canvas.SetTop(conversationDialogHost, Clamp(value2, 0.0, Math.Max(0.0, conversationCanvas.ActualHeight - conversationDialogHost.ActualHeight)));
		}
	}

	private void ConversationHeaderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (conversationHeaderDragging)
		{
			conversationHeaderDragging = false;
			conversationHeader.ReleaseMouseCapture();
			e.Handled = true;
		}
	}

	private void InitializeConversationDialog()
	{
		conversationOverlay.UpdateLayout();
		if (!(conversationCanvas.ActualWidth <= 0.0) && !(conversationCanvas.ActualHeight <= 0.0))
		{
			if (!conversationDialogInitialized)
			{
				conversationDialogHost.Width = Math.Min(940.0, Math.Max(conversationDialogHost.MinWidth, conversationCanvas.ActualWidth - 56.0));
				conversationDialogHost.Height = Math.Min(650.0, Math.Max(conversationDialogHost.MinHeight, conversationCanvas.ActualHeight - 56.0));
				CenterConversationDialog();
				conversationDialogInitialized = true;
			}
			EnsureConversationDialogFits();
		}
	}

	private void ToggleConversationDialogMaximize()
	{
		InitializeConversationDialog();
		if (!conversationDialogMaximized)
		{
			conversationRestoreLeft = DialogLeft();
			conversationRestoreTop = DialogTop();
			conversationRestoreWidth = conversationDialogHost.ActualWidth;
			conversationRestoreHeight = conversationDialogHost.ActualHeight;
			conversationDialogMaximized = true;
		}
		else
		{
			conversationDialogMaximized = false;
			conversationDialogHost.Width = conversationRestoreWidth;
			conversationDialogHost.Height = conversationRestoreHeight;
			Canvas.SetLeft(conversationDialogHost, conversationRestoreLeft);
			Canvas.SetTop(conversationDialogHost, conversationRestoreTop);
		}
		UpdateConversationMaximizeGlyph();
		EnsureConversationDialogFits();
	}

	private void UpdateConversationMaximizeGlyph()
	{
		conversationMaximizeGlyph.Visibility = conversationDialogMaximized ? Visibility.Collapsed : Visibility.Visible;
		conversationRestoreGlyph.Visibility = conversationDialogMaximized ? Visibility.Visible : Visibility.Collapsed;
		conversationMaximizeButton.ToolTip = UiLanguage.T(conversationDialogMaximized ? "还原预览框" : "放大预览框");
	}

	private void ShrinkConversationDialog()
	{
		InitializeConversationDialog();
		conversationDialogMaximized = false;
		conversationDialogHost.Width = Math.Min(720.0, Math.Max(conversationDialogHost.MinWidth, conversationCanvas.ActualWidth - 36.0));
		conversationDialogHost.Height = Math.Min(480.0, Math.Max(conversationDialogHost.MinHeight, conversationCanvas.ActualHeight - 36.0));
		UpdateConversationMaximizeGlyph();
		CenterConversationDialog();
	}

	private void ResizeConversationDialog(int horizontalEdge, int verticalEdge, double horizontalChange, double verticalChange)
	{
		InitializeConversationDialog();
		double num = DialogLeft();
		double num2 = DialogTop();
		double num3 = conversationDialogHost.ActualWidth;
		double num4 = conversationDialogHost.ActualHeight;
		if (horizontalEdge < 0)
		{
			double num5 = num + num3;
			double num6 = Clamp(num + horizontalChange, 4.0, num5 - conversationDialogHost.MinWidth);
			num = num6;
			num3 = num5 - num6;
		}
		else if (horizontalEdge > 0)
		{
			num3 = Clamp(num3 + horizontalChange, conversationDialogHost.MinWidth, Math.Max(conversationDialogHost.MinWidth, conversationCanvas.ActualWidth - num - 4.0));
		}
		if (verticalEdge < 0)
		{
			double num7 = num2 + num4;
			double num8 = Clamp(num2 + verticalChange, 4.0, num7 - conversationDialogHost.MinHeight);
			num2 = num8;
			num4 = num7 - num8;
		}
		else if (verticalEdge > 0)
		{
			num4 = Clamp(num4 + verticalChange, conversationDialogHost.MinHeight, Math.Max(conversationDialogHost.MinHeight, conversationCanvas.ActualHeight - num2 - 4.0));
		}
		conversationDialogHost.Width = num3;
		conversationDialogHost.Height = num4;
		Canvas.SetLeft(conversationDialogHost, num);
		Canvas.SetTop(conversationDialogHost, num2);
	}

	private void EnsureConversationDialogFits()
	{
		if (conversationDialogInitialized && !(conversationCanvas.ActualWidth <= 0.0) && !(conversationCanvas.ActualHeight <= 0.0))
		{
			if (conversationDialogMaximized)
			{
				conversationDialogHost.Width = Math.Max(conversationDialogHost.MinWidth, conversationCanvas.ActualWidth - 36.0);
				conversationDialogHost.Height = Math.Max(conversationDialogHost.MinHeight, conversationCanvas.ActualHeight - 36.0);
				Canvas.SetLeft(conversationDialogHost, 18.0);
				Canvas.SetTop(conversationDialogHost, 18.0);
			}
			else
			{
				conversationDialogHost.Width = Math.Min(conversationDialogHost.Width, Math.Max(conversationDialogHost.MinWidth, conversationCanvas.ActualWidth - 8.0));
				conversationDialogHost.Height = Math.Min(conversationDialogHost.Height, Math.Max(conversationDialogHost.MinHeight, conversationCanvas.ActualHeight - 8.0));
				Canvas.SetLeft(conversationDialogHost, Clamp(DialogLeft(), 4.0, Math.Max(4.0, conversationCanvas.ActualWidth - conversationDialogHost.Width - 4.0)));
				Canvas.SetTop(conversationDialogHost, Clamp(DialogTop(), 4.0, Math.Max(4.0, conversationCanvas.ActualHeight - conversationDialogHost.Height - 4.0)));
			}
		}
	}

	private void CenterConversationDialog()
	{
		Canvas.SetLeft(conversationDialogHost, Math.Max(0.0, (conversationCanvas.ActualWidth - conversationDialogHost.Width) / 2.0));
		Canvas.SetTop(conversationDialogHost, Math.Max(0.0, (conversationCanvas.ActualHeight - conversationDialogHost.Height) / 2.0));
	}

	private double DialogLeft()
	{
		double left = Canvas.GetLeft(conversationDialogHost);
		if (!double.IsNaN(left))
		{
			return left;
		}
		return 0.0;
	}

	private double DialogTop()
	{
		double top = Canvas.GetTop(conversationDialogHost);
		if (!double.IsNaN(top))
		{
			return top;
		}
		return 0.0;
	}

	private static double Clamp(double value, double minimum, double maximum)
	{
		if (maximum < minimum)
		{
			maximum = minimum;
		}
		return Math.Max(minimum, Math.Min(maximum, value));
	}

	private void ShowBackupPage()
	{
		backupPage.Visibility = Visibility.Visible;
		importPage.Visibility = Visibility.Collapsed;
		backupTabIndicator.Visibility = Visibility.Visible;
		importTabIndicator.Visibility = Visibility.Collapsed;
		backupTabButton.Foreground = Brush("#171916");
		importTabButton.Foreground = Brush("#747973");
		UpdateBackupMode();
	}

	private void ShowImportPage()
	{
		backupPage.Visibility = Visibility.Collapsed;
		importPage.Visibility = Visibility.Visible;
		backupTabIndicator.Visibility = Visibility.Collapsed;
		importTabIndicator.Visibility = Visibility.Visible;
		backupTabButton.Foreground = Brush("#747973");
		importTabButton.Foreground = Brush("#171916");
	}

	private async Task RefreshDataAsync()
	{
		if (isBusy)
		{
			return;
		}
		string path = CctRunner.ResolveCctPath(cctPathBox.Text.Trim());
		if (string.IsNullOrWhiteSpace(path))
		{
			cctStatusText.Text = UiLanguage.T("未找到");
			SetStatus("没有找到 cct.exe，请点击“浏览”选择。", error: true);
			AppDialog.ShowCompat(window, "没有找到 cct.exe，请点击“浏览”选择它。", "需要 cct.exe", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		cctPathBox.Text = path;
		SetBusy(busy: true, "正在读取 Codex 本地索引与会话……");
		try
		{
			string codexHome = CodexCatalog.ResolveCodexHome();
			List<DbThread> orphanedThreads = new List<DbThread>();
			string orphanDetectionError = string.Empty;
			try
			{
				orphanedThreads = await Task.Run(() => ConversationIndexMaintenance.FindOrphanedThreads(codexHome));
			}
			catch (Exception detectionError)
			{
				orphanDetectionError = detectionError.Message;
				AppendLog("检测侧边栏失效项失败：" + detectionError.Message);
			}
			LegacyCctBackupMigrationResult legacyBackups = new LegacyCctBackupMigrationResult();
			if (Environment.GetEnvironmentVariable("CODEX_MIGRATOR_SKIP_LEGACY_CCT_MAINTENANCE") != "1")
			{
				try
				{
					legacyBackups = await Task.Run(() => CctBackupMaintenance.MoveLegacyBackupsToTrash(codexHome));
				}
				catch (Exception cleanupError)
				{
					AppendLog("整理旧版 cct 临时快照失败：" + cleanupError.Message);
				}
			}
			Task<CctResult> versionTask = CctRunner.RunAsync(path, new string[1] { "--version" }, null);
			Task<CctResult> listTask = CctRunner.RunAsync(path, new string[5] { "list", "--json", "--include-archived", "--codex-home", codexHome }, null);
			await Task.WhenAll<CctResult>(versionTask, listTask);
			CctResult list = listTask.Result;
			if (list.ExitCode != 0)
			{
				throw new InvalidOperationException(CctRunner.FirstUseful(list));
			}
			List<SessionInfo> raw = CctRunner.ParseSessions(list.StdOut);
			CatalogResult catalog = await Task.Run(() => CodexCatalog.Build(raw));
			projects = catalog.Projects;
			foreach (ProjectGroup project in projects)
			{
				project.PropertyChanged += ProjectPropertyChanged;
				foreach (SessionInfo session in project.Sessions)
				{
					session.PropertyChanged += SessionPropertyChanged;
				}
			}
			projectList.ItemsSource = projects;
			if (projects.Count > 0)
			{
				projectList.SelectedIndex = 0;
			}
			else
			{
				selectedProject = null;
				sessionList.ItemsSource = null;
			}
			UpdateSelectedCount();
			string version = (versionTask.Result.StdOut ?? string.Empty).Trim();
			cctStatusText.Text = (string.IsNullOrWhiteSpace(version) ? UiLanguage.T("cct 已连接") : version);
			string cleanupSummary = legacyBackups.MovedToTrashCount == 0 ? string.Empty : $" · 旧版快照已转入回收站 {legacyBackups.MovedToTrashCount} 个，清理重复 {legacyBackups.RedundantDeletedCount} 个";
			string orphanSummary = string.Empty;
			bool orphanError = !string.IsNullOrWhiteSpace(orphanDetectionError);
			if (orphanedThreads.Count > 0)
			{
				if (CodexDesktopProjectRegistry.IsDesktopRunning(codexHome))
				{
					orphanSummary = $" · 检测到侧边栏失效项 {orphanedThreads.Count} 个（完全退出 Codex 后点击刷新可处理）";
				}
				else
				{
					string itemPreview = string.Join("\n", orphanedThreads.Take(8).Select((DbThread thread) => "• " + (string.IsNullOrWhiteSpace(thread.Title) ? thread.Id : thread.Title + " · " + thread.Id)));
					if (orphanedThreads.Count > 8)
					{
						itemPreview += $"\n…另有 {orphanedThreads.Count - 8} 个";
					}
					MessageBoxResult repairAnswer = AppDialog.ShowCompat(window, $"检测到 {orphanedThreads.Count} 个侧边栏失效项：它们的索引仍存在，但对应会话文件已经不存在。\n\n{itemPreview}\n\n是否删除上面这些失效索引？操作前会自动备份索引；不会删除任何仍存在的会话文件。", "清理侧边栏失效项", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (repairAnswer == MessageBoxResult.Yes)
					{
						try
						{
							OrphanIndexRepairResult repair = await Task.Run(() => ConversationIndexMaintenance.RepairSelectedOrphans(codexHome, orphanedThreads.Select((DbThread thread) => thread.Id)));
							if (repair.DesktopRunning)
							{
								orphanSummary = " · Codex 已重新启动，未清理失效项";
								orphanError = true;
							}
							else
							{
								orphanSummary = $" · 已清理侧边栏失效项 {repair.RepairedCount} 个";
								AppendLog($"已清理侧边栏失效项 {repair.RepairedCount} 个。索引备份：{repair.IndexBackupPath}");
							}
						}
						catch (Exception repairError)
						{
							orphanSummary = " · 侧边栏失效项清理失败，详见操作记录";
							orphanError = true;
							AppendLog("清理侧边栏失效项失败：" + repairError.Message);
							AppDialog.ShowCompat(window, repairError.Message, "清理失败", MessageBoxButton.OK, MessageBoxImage.Warning);
						}
					}
					else
					{
						orphanSummary = $" · 已保留侧边栏失效项 {orphanedThreads.Count} 个";
					}
				}
			}
			else if (orphanError)
			{
				orphanSummary = " · 侧边栏失效项检测失败，详见操作记录";
			}
			SetStatus($"已载入 {projects.Count} 个项目 · {catalog.MainCount} 个主对话 · {catalog.InternalCount} 个子代理对话 · {catalog.Diagnostic}{cleanupSummary}{orphanSummary}", error: orphanError);
		}
		catch (Exception ex)
		{
			cctStatusText.Text = UiLanguage.T("读取失败");
			SetStatus("读取失败：" + ex.Message, error: true);
			AppDialog.ShowCompat(window, ex.Message, "读取会话失败", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			SetBusy(busy: false, null);
		}
	}

	private async void ProjectListSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		selectedProject = projectList.SelectedItem as ProjectGroup;
		if (selectedProject != null)
		{
			projectTitleText.Text = selectedProject.DisplayName;
			projectPathText.Text = selectedProject.ProjectPath;
			projectMetaText.Text = selectedProject.HeaderMeta;
			projectSizeText.Text = selectedProject.ProjectStorageSummary;
			sessionView = CollectionViewSource.GetDefaultView(selectedProject.Sessions);
			sessionView.Filter = SessionFilter;
			sessionList.ItemsSource = sessionView;
			UpdateSessionTypeView();
			UpdateSelectedCount();
			await EnsureProjectStorageMetricsAsync(selectedProject);
		}
	}

	private async Task EnsureProjectStorageMetricsAsync(ProjectGroup project)
	{
		if (project == null)
		{
			return;
		}
		if (!project.StorageScanStarted)
		{
			project.BeginStorageScan();
			if (ReferenceEquals(project, selectedProject))
			{
				projectSizeText.Text = project.ProjectStorageSummary;
			}
			ProjectStorageSummary summary = await Task.Run(() => ProjectStorageMetrics.Measure(project.ProjectPath));
			project.CompleteStorageScan(summary);
		}
		if (ReferenceEquals(project, selectedProject))
		{
			projectSizeText.Text = project.ProjectStorageSummary;
		}
	}

	private void CopySelectedProjectPath()
	{
		string path = selectedProject?.ProjectPath;
		if (string.IsNullOrWhiteSpace(path))
		{
			SetStatus("当前项目没有可复制的路径。", error: true);
			return;
		}
		try
		{
			System.Windows.Clipboard.SetText(path);
			SetStatus("已复制项目路径：" + path, error: false);
		}
		catch (Exception ex)
		{
			SetStatus("复制项目路径失败：" + ex.Message, error: true);
		}
	}

	private void ProjectListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		ListBoxItem listBoxItem = ((!(e.OriginalSource is DependencyObject element)) ? null : (ItemsControl.ContainerFromElement(projectList, element) as ListBoxItem));
		if (listBoxItem != null && !listBoxItem.IsSelected)
		{
			listBoxItem.IsSelected = true;
			projectList.ScrollIntoView(listBoxItem.DataContext);
		}
	}

	private bool SessionFilter(object item)
	{
		if (!(item is SessionInfo sessionInfo))
		{
			return false;
		}
		if (sessionInfo.IsSubagent != showSubagentSessions)
		{
			return false;
		}
		string text = (searchBox.Text ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return true;
		}
		return sessionInfo.DisplayTitle.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
			(sessionInfo.ThreadId ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
			(sessionInfo.DisplayPath ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
			(sessionInfo.ParentDisplayTitle ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void UpdateSessionTypeView()
	{
		showSubagentSessions = subagentSessionsTabRadio.IsChecked == true;
		int mainCount = selectedProject?.MainCount ?? 0;
		int subagentCount = selectedProject?.InternalCount ?? 0;
		mainSessionsTabRadio.Content = UiLanguage.IsEnglish ? "Main " + mainCount : "主对话 " + mainCount;
		subagentSessionsTabRadio.Content = UiLanguage.IsEnglish ? "Subagents " + subagentCount : "子代理 " + subagentCount;
		sessionList.Tag = showSubagentSessions ? "Subagent" : "Conversation";
		sessionSelectionTools.Visibility = Visibility.Visible;
		emptySessionsText.Text = UiLanguage.T(showSubagentSessions ? "此项目没有子代理对话" : "此项目没有符合条件的主对话");
		if (selectedProject != null)
		{
			sessionModeHint.Text = UiLanguage.T(showSubagentSessions ? "勾选要处理的子代理，再使用右侧“删除所选”。" : "勾选要处理的主对话，再使用右侧“删除所选”；项目目录保持不变。");
		}
		UpdateSessionSelectionControls();
		RefreshSessionView();
	}

	private void RefreshSessionView()
	{
		if (sessionView != null)
		{
			sessionView.Refresh();
			bool flag = sessionView.Cast<object>().Any();
			emptySessionsText.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			UpdateSelectedCount();
		}
	}

	private void SessionPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsSelected")
		{
			UpdateSelectedCount();
			UpdateSessionSelectionControls();
		}
	}

	private void ProjectPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsBatchSelected")
		{
			UpdateSelectedCount();
		}
		else if (e.PropertyName == "ProjectStorageSummary" && ReferenceEquals(sender, selectedProject))
		{
			projectSizeText.Text = selectedProject.ProjectStorageSummary;
		}
	}

	private void UpdateSelectedCount()
	{
		int selectedProjects = projects.Count((ProjectGroup project) => project.IsBatchSelected);
		int selectedConversations = projects.SelectMany((ProjectGroup project) => project.Sessions ?? new List<SessionInfo>()).Count((SessionInfo session) => !session.IsSubagent && session.IsSelected);
		if (projectBackupMode)
		{
			selectedCountText.Text = UiLanguage.T("已选 " + selectedProjects + " 个项目＋对话");
			selectionHelpText.Text = UiLanguage.T("生成 .codexproject：项目目录、主对话和子代理对话一起备份");
			backupProjectFilesButton.IsEnabled = !isBusy && selectedProjects > 0;
		}
		else
		{
			int selectedFromProjects = projects.Count((ProjectGroup project) => (project.Sessions ?? new List<SessionInfo>()).Any((SessionInfo session) => !session.IsSubagent && session.IsSelected));
			selectedCountText.Text = UiLanguage.T("已选 " + selectedConversations + " 个对话 · 来自 " + selectedFromProjects + " 个项目");
			selectionHelpText.Text = UiLanguage.T("生成 .codexchat：只备份勾选的主对话，不包含项目目录");
			backupSelectedButton.IsEnabled = !isBusy && selectedConversations > 0;
		}
	}

	private List<SessionInfo> CurrentSessionTypeItems()
	{
		return selectedProject?.Sessions.Where((SessionInfo session) => session.IsSubagent == showSubagentSessions).ToList() ?? new List<SessionInfo>();
	}

	private void UpdateSessionSelectionControls()
	{
		List<SessionInfo> sessions = CurrentSessionTypeItems();
		int selectedCount = sessions.Count((SessionInfo session) => session.IsSelected);
		bool allSelected = sessions.Count > 0 && selectedCount == sessions.Count;
		toggleSessionSelectionButton.Content = UiLanguage.T(allSelected ? "全不选" : "全选");
		toggleSessionSelectionButton.IsEnabled = !isBusy && sessions.Count > 0;
		deleteSelectedSessionsButton.Content = selectedCount > 0 ? UiLanguage.T("删除所选") + " (" + selectedCount + ")" : UiLanguage.T("删除所选");
		deleteSelectedSessionsButton.IsEnabled = !isBusy && selectedCount > 0;
		if (selectedProject != null)
		{
			string typeLabel = showSubagentSessions ? "子代理" : "主对话";
			sessionModeHint.Text = UiLanguage.T("勾选要处理的" + typeLabel + " · 已选 " + selectedCount + "/" + sessions.Count + " 个；全部选中后再次点击“全不选”。");
		}
	}

	private void ToggleSessionSelection()
	{
		List<SessionInfo> sessions = CurrentSessionTypeItems();
		bool selectAll = sessions.Count > 0 && sessions.Any((SessionInfo session) => !session.IsSelected);
		foreach (SessionInfo session in sessions)
		{
			session.IsSelected = selectAll;
		}
		UpdateSessionSelectionControls();
	}

	private void UpdateBackupMode()
	{
		bool newProjectMode = projectBackupModeRadio.IsChecked == true;
		if (newProjectMode != projectBackupMode)
		{
			if (newProjectMode)
			{
				foreach (SessionInfo session in projects.SelectMany((ProjectGroup project) => project.Sessions ?? new List<SessionInfo>()))
				{
					session.IsSelected = false;
				}
			}
			else
			{
				foreach (ProjectGroup project in projects)
				{
					project.IsBatchSelected = false;
				}
			}
		}
		projectBackupMode = newProjectMode;
		projectList.Tag = projectBackupMode ? "Project" : "Conversation";
		projectSelectionTools.Visibility = projectBackupMode ? Visibility.Visible : Visibility.Collapsed;
		backupProjectFilesButton.Visibility = projectBackupMode ? Visibility.Visible : Visibility.Collapsed;
		backupSelectedButton.Visibility = projectBackupMode ? Visibility.Collapsed : Visibility.Visible;
		projectPaneTitle.Text = UiLanguage.T(projectBackupMode ? "选择项目＋对话" : "选择仅对话");
		projectPaneSubtitle.Text = UiLanguage.T(projectBackupMode ? "生成 .codexproject，包含项目目录和全部关联对话" : "生成 .codexchat，只包含勾选的主对话");
		backupModeHelpText.Text = UiLanguage.T(projectBackupMode ? "项目＋对话备份：项目文件、主对话和子代理对话放在同一个文件里。" : "仅对话备份：可跨项目勾选主对话，不包含项目文件。");
		UpdateSessionTypeView();
		UpdateSelectedCount();
	}

	private void SetProjectSelection(bool value)
	{
		foreach (ProjectGroup project in projects)
		{
			if (project.CanBackupFiles)
			{
				project.IsBatchSelected = value;
			}
		}
		UpdateSelectedCount();
	}

	private void SetVisibleSelection(bool value)
	{
		if (sessionView == null)
		{
			return;
		}
		foreach (SessionInfo item in sessionView.Cast<object>().OfType<SessionInfo>())
		{
			if (item.CanSelect)
			{
				item.IsSelected = value;
			}
		}
		UpdateSelectedCount();
	}

	private async void SessionActionButtonClick(object sender, RoutedEventArgs e)
	{
		System.Windows.Controls.Button button = ResolveActionButton(e.OriginalSource as DependencyObject) ?? (e.Source as System.Windows.Controls.Button);
		if (button != null && button.CommandParameter is SessionInfo session)
		{
			if (string.Equals(button.Name, "ViewSessionButton", StringComparison.Ordinal))
			{
				e.Handled = true;
				await ShowConversationAsync(session);
			}
			else if (string.Equals(button.Name, "DeleteSessionButton", StringComparison.Ordinal))
			{
				e.Handled = true;
				await DeleteSessionAsync(session);
			}
		}
	}

	private static System.Windows.Controls.Button ResolveActionButton(DependencyObject source)
	{
		DependencyObject dependencyObject = source;
		while (dependencyObject != null)
		{
			if (dependencyObject is System.Windows.Controls.Button result)
			{
				return result;
			}
			DependencyObject dependencyObject2 = null;
			try
			{
				dependencyObject2 = VisualTreeHelper.GetParent(dependencyObject);
			}
			catch
			{
			}
			if (dependencyObject2 == null)
			{
				try
				{
					dependencyObject2 = LogicalTreeHelper.GetParent(dependencyObject);
				}
				catch
				{
				}
			}
			dependencyObject = dependencyObject2;
		}
		return null;
	}

	private async Task ShowConversationAsync(SessionInfo session)
	{
		if (session == null)
		{
			return;
		}
		previewedThreadId = session.ThreadId ?? string.Empty;
		conversationTitleText.Text = session.DisplayTitle;
		conversationMetaText.Text = UiLanguage.T("正在读取本地会话……") + " · " + session.ShortId;
		conversationList.ItemsSource = new ConversationMessage[1]
		{
			new ConversationMessage
			{
				RoleLabel = UiLanguage.T("提示"),
				Text = UiLanguage.T("正在整理对话内容……"),
				IsNotice = true
			}
		};
		conversationOverlay.Visibility = Visibility.Visible;
		InitializeConversationDialog();
		try
		{
			ConversationReadResult result = await Task.Run(() => ConversationReader.Read(session));
			conversationList.ItemsSource = result.Messages;
			int visibleCount = result.Messages.Count((ConversationMessage x) => !x.IsNotice);
			conversationMetaText.Text = UiLanguage.IsEnglish ? string.Format("{0} text messages{1} · Thread {2}", visibleCount, result.Truncated ? " · preview truncated" : string.Empty, session.ThreadId) : string.Format("{0} 条文本消息{1} · Thread {2}", visibleCount, result.Truncated ? " · 预览已截断" : string.Empty, session.ThreadId);
			if (result.Messages.Count > 0)
			{
				conversationList.ScrollIntoView(result.Messages[0]);
			}
		}
		catch (Exception ex)
		{
			conversationList.ItemsSource = new ConversationMessage[1]
			{
				new ConversationMessage
				{
					RoleLabel = UiLanguage.T("无法打开"),
					Text = ex.Message,
					IsNotice = true
				}
			};
			conversationMetaText.Text = UiLanguage.T("读取失败") + " · " + session.ShortId;
		}
	}

	private void HideConversation()
	{
		conversationOverlay.Visibility = Visibility.Collapsed;
		conversationList.ItemsSource = null;
		previewedThreadId = string.Empty;
	}

	private void CopyPreviewedThreadId()
	{
		if (string.IsNullOrWhiteSpace(previewedThreadId))
		{
			return;
		}
		try
		{
			System.Windows.Clipboard.SetText(previewedThreadId);
			SetStatus("已复制 Thread ID：" + previewedThreadId, error: false);
		}
		catch (Exception ex)
		{
			SetStatus("复制失败：" + ex.Message, error: true);
		}
	}

	private async Task DeleteSessionAsync(SessionInfo session)
	{
		if (isBusy || session == null)
		{
			return;
		}
		if (!EnsureCodexClosedForConversationWrite())
		{
			return;
		}
		bool deletingSubagent = session.IsSubagent;
		string projectPath = ConversationStorage.ResolveProjectPath(session, selectedProject);
		int relatedConversationCount = CountRelatedConversations(projectPath);
		DeleteOptions options = DeleteOptionsDialog.Show(window, session, projectPath, relatedConversationCount, allowProjectActions: !deletingSubagent);
		if (options == null)
		{
			return;
		}
		if (options.ProjectMode != ProjectDeleteMode.None)
		{
			try
			{
				projectPath = ConversationStorage.ValidateProjectPath(projectPath);
			}
			catch (Exception ex)
			{
				AppDialog.ShowCompat(window, ex.Message, "不能处理项目目录", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
		}
		string conversationSummary = options.ConversationMode == ConversationDeleteMode.MoveToTrash ? "会话：移入软件回收站（可恢复）" : "会话：永久删除（不可恢复）";
		string projectSummary = options.ProjectMode switch
		{
			ProjectDeleteMode.RecycleBin => "\n项目：移入 Windows 回收站",
			ProjectDeleteMode.Permanent => "\n项目：永久递归删除（不可恢复）",
			_ => "\n项目：保留不动"
		};
		MessageBoxResult answer = AppDialog.ShowCompat(window, "请确认本次操作：\n\n" + session.DisplayTitle + "\n\n" + conversationSummary + projectSummary + "\n\n本次操作会同步更新会话文件与 Codex 侧边栏索引。", deletingSubagent ? "确认删除子代理对话" : "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
		if (answer != MessageBoxResult.Yes)
		{
			return;
		}
		string projectId = ((selectedProject == null) ? string.Empty : selectedProject.ProjectId);
		SetBusy(busy: true, options.ConversationMode == ConversationDeleteMode.MoveToTrash ? "正在把会话移入软件回收站……" : "正在永久删除会话……");
		DeleteOperationResult operation = null;
		try
		{
			try
			{
				string selectedProjectPath = projectPath;
				operation = await Task.Run(delegate
				{
					DeletedSessionResult conversation = options.ConversationMode == ConversationDeleteMode.MoveToTrash ? ConversationStorage.MoveToTrash(session, selectedProjectPath) : ConversationStorage.DeletePermanently(session);
					DeleteOperationResult result = new DeleteOperationResult
					{
						Conversation = conversation,
						ProjectPath = selectedProjectPath,
						ProjectMode = options.ProjectMode
					};
					if (options.ProjectMode != ProjectDeleteMode.None)
					{
						try
						{
							ConversationStorage.DeleteProject(selectedProjectPath, options.ProjectMode);
							if (!conversation.PermanentlyDeleted)
							{
								try
								{
									ConversationStorage.MarkProjectHandled(conversation.BackupPath, selectedProjectPath, options.ProjectMode);
								}
								catch
								{
								}
							}
						}
						catch (Exception ex)
						{
							result.ProjectError = ex.Message;
						}
					}
					return result;
				});
			}
			catch (Exception ex)
			{
				SetStatus("删除失败：" + ex.Message, error: true);
				AppDialog.ShowCompat(window, ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
		}
		finally
		{
			SetBusy(busy: false, null);
		}
		HideConversation();
		await RefreshDataAsync();
		ProjectGroup previousProject = projects.FirstOrDefault((ProjectGroup x) => string.Equals(x.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
		if (previousProject != null)
		{
			projectList.SelectedItem = previousProject;
		}
		string completion = operation.Conversation.PermanentlyDeleted ? "会话已永久删除。" : "会话已移入软件回收站：\n" + operation.Conversation.BackupPath;
		if (operation.ProjectMode != ProjectDeleteMode.None)
		{
			completion += operation.ProjectSucceeded ? (operation.ProjectMode == ProjectDeleteMode.RecycleBin ? "\n\n项目已移入 Windows 回收站。" : "\n\n项目已永久删除。") : "\n\n会话操作已完成，但项目处理失败：\n" + operation.ProjectError;
		}
		completion += "\n\n重新打开 Codex 后，该会话不会再出现在侧边栏。";
		SetStatus(operation.ProjectSucceeded ? (operation.Conversation.PermanentlyDeleted ? "会话已永久删除。" : "会话已移入软件回收站。") : "会话已删除，但项目处理失败。", error: !operation.ProjectSucceeded);
		AppDialog.ShowCompat(window, completion, operation.ProjectSucceeded ? "删除完成" : "部分完成", MessageBoxButton.OK, operation.ProjectSucceeded ? MessageBoxImage.Asterisk : MessageBoxImage.Warning);
	}

	private async Task DeleteSelectedSessionsAsync()
	{
		if (isBusy || selectedProject == null)
		{
			return;
		}
		if (!EnsureCodexClosedForConversationWrite())
		{
			return;
		}
		bool deletingSubagents = showSubagentSessions;
		List<SessionInfo> selectedSessions = CurrentSessionTypeItems().Where((SessionInfo session) => session.IsSelected).ToList();
		string typeLabel = deletingSubagents ? "子代理" : "主对话";
		string otherTypeLabel = deletingSubagents ? "主对话" : "子代理";
		if (selectedSessions.Count == 0)
		{
			AppDialog.ShowCompat(window, "请先勾选一个或多个" + typeLabel + "。", "尚未选择" + typeLabel, MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		ProjectGroup sourceProject = selectedProject;
		DeleteOptions options = SessionBatchDeleteDialog.Show(window, sourceProject, selectedSessions);
		if (options == null)
		{
			return;
		}
		if (options.ConversationMode == ConversationDeleteMode.Permanent)
		{
			MessageBoxResult permanentAnswer = AppDialog.ShowCompat(window, "将永久删除已选择的 " + selectedSessions.Count + " 个" + typeLabel + "（" + TextHelpers.FormatBytes(selectedSessions.Sum((SessionInfo session) => session.SizeBytes)) + "）。\n\n未选择的" + typeLabel + "、全部" + otherTypeLabel + "和项目目录都不会删除。", "确认永久删除所选" + typeLabel, MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
			if (permanentAnswer != MessageBoxResult.Yes)
			{
				return;
			}
		}

		string projectId = sourceProject.ProjectId;
		SetBusy(busy: true, options.ConversationMode == ConversationDeleteMode.MoveToTrash ? "正在把所选" + typeLabel + "移入软件回收站……" : "正在永久删除所选" + typeLabel + "……");
		int completed = 0;
		List<string> errors = new List<string>();
		try
		{
			await Task.Run(delegate
			{
				foreach (SessionInfo session in selectedSessions)
				{
					try
					{
						if (options.ConversationMode == ConversationDeleteMode.MoveToTrash)
						{
							ConversationStorage.MoveToTrash(session, ConversationStorage.ResolveProjectPath(session, sourceProject));
						}
						else
						{
							ConversationStorage.DeletePermanently(session);
						}
						completed++;
					}
					catch (Exception ex)
					{
						errors.Add(session.ShortId + " · " + ex.Message);
					}
				}
			});
		}
		finally
		{
			SetBusy(busy: false, null);
		}

		HideConversation();
		await RefreshDataAsync();
		ProjectGroup previousProject = projects.FirstOrDefault((ProjectGroup project) => string.Equals(project.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
		if (previousProject != null)
		{
			projectList.SelectedItem = previousProject;
		}
		if (deletingSubagents)
		{
			subagentSessionsTabRadio.IsChecked = true;
		}
		else
		{
			mainSessionsTabRadio.IsChecked = true;
		}
		UpdateSessionTypeView();
		string action = options.ConversationMode == ConversationDeleteMode.MoveToTrash ? "移入软件回收站" : "永久删除";
		string resultText = "已" + action + " " + completed + " 个所选" + typeLabel + "。\n\n未选择的" + typeLabel + "、全部" + otherTypeLabel + "和项目目录保持不变。";
		if (errors.Count > 0)
		{
			resultText += "\n\n有 " + errors.Count + " 个处理失败：\n" + string.Join("\n", errors.Take(8));
		}
		resultText += "\n\n重新打开 Codex 后，已删除的会话不会再出现在侧边栏。";
		SetStatus(errors.Count == 0 ? "所选" + typeLabel + "已处理。" : "部分所选" + typeLabel + "处理失败。", error: errors.Count > 0);
		AppDialog.ShowCompat(window, resultText, errors.Count == 0 ? "所选" + typeLabel + "处理完成" : typeLabel + "部分处理完成", MessageBoxButton.OK, errors.Count == 0 ? MessageBoxImage.Asterisk : MessageBoxImage.Warning);
	}

	private int CountRelatedConversations(string projectPath)
	{
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			return 0;
		}
		return projects.SelectMany((ProjectGroup project) => project.Sessions).Where((SessionInfo item) => !item.IsSubagent && TextHelpers.IsWithin(item.Cwd, projectPath)).Select((SessionInfo item) => item.ThreadId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
	}

	private async Task ShowTrashManagerAsync()
	{
		if (isBusy)
		{
			return;
		}
		while (true)
		{
			List<TrashSessionInfo> items;
			try
			{
				items = await Task.Run(ConversationStorage.ReadTrash);
			}
			catch (Exception ex)
			{
				AppDialog.ShowCompat(window, ex.Message, "读取软件回收站失败", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			TrashActionRequest request = TrashManagerDialog.Show(window, items);
			if (request == null || request.Action == TrashAction.None || request.Item == null)
			{
				return;
			}
			if (request.Action == TrashAction.Restore)
			{
				if (!EnsureCodexClosedForConversationWrite())
				{
					continue;
				}
				MessageBoxResult answer = AppDialog.ShowCompat(window, "把这个会话恢复到原位置吗？\n\n" + request.Item.DisplayTitle + "\n\n" + request.Item.OriginalPath, "恢复会话", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (answer != MessageBoxResult.Yes)
				{
					continue;
				}
				SetBusy(busy: true, "正在恢复会话……");
				try
				{
					await Task.Run(() => ConversationStorage.Restore(request.Item));
					SetStatus("会话及侧边栏索引均已恢复。", error: false);
					await RefreshDataAsync();
					AppDialog.ShowCompat(window, "会话已恢复到原位置，侧边栏索引也已重新登记。\n\n重新打开 Codex 后即可查看。", "恢复完成", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				catch (Exception ex)
				{
					SetStatus("恢复失败：" + ex.Message, error: true);
					AppDialog.ShowCompat(window, ex.Message, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				finally
				{
					SetBusy(busy: false, null);
				}
				continue;
			}
			if (request.Action == TrashAction.DeletePermanently)
			{
				if (!EnsureCodexClosedForConversationWrite())
				{
					continue;
				}
				MessageBoxResult answer = AppDialog.ShowCompat(window, "永久删除这个回收站会话备份吗？\n\n" + request.Item.DisplayTitle + "\n\n删除后无法从本工具恢复。", "永久删除回收站备份", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
				if (answer != MessageBoxResult.Yes)
				{
					continue;
				}
				SetBusy(busy: true, "正在永久删除回收站备份……");
				try
				{
					await Task.Run(() => ConversationStorage.DeleteFromTrash(request.Item));
					SetStatus("回收站会话备份已永久删除。", error: false);
				}
				catch (Exception ex)
				{
					SetStatus("永久删除失败：" + ex.Message, error: true);
					AppDialog.ShowCompat(window, ex.Message, "永久删除失败", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				finally
				{
					SetBusy(busy: false, null);
				}
				continue;
			}
			if (request.Action == TrashAction.DeleteProject)
			{
				string validatedProjectPath;
				try
				{
					validatedProjectPath = ConversationStorage.ValidateProjectPath(request.Item.ProjectPath);
				}
				catch (Exception ex)
				{
					AppDialog.ShowCompat(window, ex.Message, "不能处理项目目录", MessageBoxButton.OK, MessageBoxImage.Warning);
					continue;
				}
				ProjectDeleteMode mode = ProjectDeleteOptionsDialog.Show(window, validatedProjectPath, request.Item.DisplayTitle);
				if (mode == ProjectDeleteMode.None)
				{
					continue;
				}
				string modeText = mode == ProjectDeleteMode.RecycleBin ? "移入 Windows 回收站" : "永久递归删除";
				MessageBoxResult answer = AppDialog.ShowCompat(window, "确定要" + modeText + "这个项目目录吗？\n\n" + validatedProjectPath, "确认处理项目目录", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
				if (answer != MessageBoxResult.Yes)
				{
					continue;
				}
				SetBusy(busy: true, "正在处理项目目录……");
				try
				{
					await Task.Run(delegate
					{
						ConversationStorage.DeleteProject(validatedProjectPath, mode);
						try
						{
							ConversationStorage.MarkProjectHandled(request.Item, mode);
						}
						catch
						{
						}
					});
					SetStatus(mode == ProjectDeleteMode.RecycleBin ? "项目已移入 Windows 回收站。" : "项目已永久删除。", error: false);
					await RefreshDataAsync();
				}
				catch (Exception ex)
				{
					SetStatus("项目处理失败：" + ex.Message, error: true);
					AppDialog.ShowCompat(window, ex.Message, "项目处理失败", MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				finally
				{
					SetBusy(busy: false, null);
				}
			}
		}
	}

	private bool EnsureCodexClosedForConversationWrite()
	{
		try
		{
			CodexDesktopProjectRegistry.EnsureImportCanWrite(CodexCatalog.ResolveCodexHome());
			return true;
		}
		catch (Exception ex)
		{
			SetStatus("请先完全退出 Codex，再执行会话删除或恢复。", error: true);
			AppDialog.ShowCompat(window, ex.Message, "请先退出 Codex", MessageBoxButton.OK, MessageBoxImage.Warning);
			return false;
		}
	}

	private async Task BrowseCctAsync()
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = UiLanguage.T("选择 cct.exe"),
			Filter = UiLanguage.T("cct.exe|cct.exe|可执行文件 (*.exe)|*.exe")
		};
		if (dialog.ShowDialog(window) == true)
		{
			cctPathBox.Text = dialog.FileName;
			await RefreshDataAsync();
		}
	}

	private async Task BackupSelectedAsync()
	{
		List<BackupProjectSelection> selections = projects.Select((ProjectGroup project) => new BackupProjectSelection
		{
			Project = project,
			Sessions = (from session in project.Sessions
				where !session.IsSubagent && session.IsSelected
				orderby session.UpdatedDate descending
				select session).ToList()
		}).Where((BackupProjectSelection selection) => selection.Sessions.Count > 0).ToList();
		int selectedCount = selections.Sum((BackupProjectSelection selection) => selection.Sessions.Count);
		if (selectedCount == 0)
		{
			AppDialog.ShowCompat(window, "请在右侧勾选一个或多个主对话。可以切换项目继续勾选，选择会保留。", "尚未选择对话", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (fullFidelityCheck.IsChecked != true)
		{
			MessageBoxResult messageBoxResult = AppDialog.ShowCompat(window, "为避开 cct 对重复 Thread ID 的误判，精准单对话备份会直接封装原始会话，因此必须完整保留内容。\n\n是否继续？", "精准备份会保留原文", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
			if (messageBoxResult != MessageBoxResult.Yes)
			{
				return;
			}
		}
		string prefix = selections.Count == 1 ? TextHelpers.SafeFileName(selections[0].Project.DisplayName) : selections.Count + "个项目";
		string name = prefix + "-仅对话-已选" + selectedCount + "个-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + BackupPackageFormat.ConversationExtension;
		if (TryGetBackupOutput(name, out string output))
		{
			if (selections.Count == 1)
			{
				await CreatePackAsync(selections[0].Project, selections[0].Sessions, wholeProject: false, includeProjectFiles: false, output);
			}
			else
			{
				await CreateBatchPackAsync(selections, includeProjectFiles: false, output);
			}
		}
	}

	private static string DefaultBackupFolder()
	{
		string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		if (string.IsNullOrWhiteSpace(documents))
		{
			documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		return System.IO.Path.Combine(documents, UiLanguage.IsEnglish ? "Codex Migration Packages" : "Codex 迁移包");
	}

	private void BrowseBackupFolder()
	{
		using FolderBrowserDialog dialog = new FolderBrowserDialog
		{
			Description = UiLanguage.T("选择迁移包保存文件夹"),
			ShowNewFolderButton = true
		};
		if (Directory.Exists(backupFolderBox.Text))
		{
			dialog.SelectedPath = backupFolderBox.Text;
		}
		if (dialog.ShowDialog() == DialogResult.OK)
		{
			backupFolderBox.Text = dialog.SelectedPath;
		}
	}

	private bool TryGetBackupOutput(string defaultName, out string output)
	{
		output = string.Empty;
		try
		{
			string folder = backupFolderBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(folder))
			{
				throw new InvalidOperationException("请先选择迁移包保存文件夹。");
			}
			folder = System.IO.Path.GetFullPath(folder);
			if (File.Exists(folder))
			{
				throw new IOException("备份位置是一个文件，不是文件夹：\n" + folder);
			}
			Directory.CreateDirectory(folder);
			backupFolderBox.Text = folder;
			string extension = System.IO.Path.GetExtension(defaultName);
			if (!string.Equals(extension, BackupPackageFormat.ConversationExtension, StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, BackupPackageFormat.ProjectExtension, StringComparison.OrdinalIgnoreCase))
			{
				extension = BackupPackageFormat.ConversationExtension;
			}
			string baseName = TextHelpers.SafeFileName(System.IO.Path.GetFileNameWithoutExtension(defaultName));
			string safeName = baseName + extension;
			string candidate = System.IO.Path.Combine(folder, safeName);
			int suffix = 2;
			while (File.Exists(candidate))
			{
				candidate = System.IO.Path.Combine(folder, baseName + "-" + suffix + extension);
				suffix++;
			}
			output = candidate;
			return true;
		}
		catch (Exception ex)
		{
			AppDialog.ShowCompat(window, ex.Message, "备份位置不可用", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
	}

	private async Task BackupProjectWithFilesAsync()
	{
		List<ProjectGroup> selectedProjects = projects.Where((ProjectGroup project) => project.IsBatchSelected).ToList();
		if (selectedProjects.Count == 0)
		{
			AppDialog.ShowCompat(window, "请先勾选左侧的一个或多个项目。", "尚未选择项目", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		List<ProjectGroup> unavailable = selectedProjects.Where((ProjectGroup project) => !project.CanBackupFiles).ToList();
		if (unavailable.Count > 0)
		{
			AppDialog.ShowCompat(window, "以下项目目录不存在，无法加入完整迁移包：\n\n" + string.Join("\n", unavailable.Select((ProjectGroup project) => project.DisplayName + "：" + project.ProjectPath)), "找不到项目目录", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		int conversationCount = selectedProjects.Sum((ProjectGroup project) => project.Sessions.Count);
		string projectListText = string.Join("\n", selectedProjects.Take(8).Select((ProjectGroup project) => "• " + project.DisplayName));
		if (selectedProjects.Count > 8)
		{
			projectListText += "\n• 另外 " + (selectedProjects.Count - 8) + " 个项目";
		}
		MessageBoxResult answer = AppDialog.ShowCompat(window, "将创建一个批量迁移包：\n\n" + projectListText + "\n\n共 " + selectedProjects.Count + " 个项目、" + conversationCount + " 条主对话/子代理对话。项目普通文件、空目录和全部对应对话都会进入同一个包；目录联接和符号链接不会跟随。\n\n迁移包可能包含源码、密钥、构建产物和大文件，请妥善保管。是否继续？", "确认迁移所选项目", MessageBoxButton.YesNo, MessageBoxImage.Warning);
		if (answer != MessageBoxResult.Yes)
		{
			return;
		}
		string prefix = selectedProjects.Count == 1 ? TextHelpers.SafeFileName(selectedProjects[0].DisplayName) : selectedProjects.Count + "个项目";
		string name = prefix + "-项目与对话-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + BackupPackageFormat.ProjectExtension;
		if (TryGetBackupOutput(name, out string output))
		{
			if (selectedProjects.Count == 1)
			{
				await CreatePackAsync(selectedProjects[0], selectedProjects[0].Sessions, wholeProject: true, includeProjectFiles: true, output);
			}
			else
			{
				await CreateBatchPackAsync(selectedProjects.Select((ProjectGroup project) => new BackupProjectSelection
				{
					Project = project,
					Sessions = project.Sessions
				}).ToList(), includeProjectFiles: true, output);
			}
		}
	}

	private async Task CreateBatchPackAsync(IList<BackupProjectSelection> selections, bool includeProjectFiles, string output)
	{
		string cct = CctRunner.ResolveCctPath(cctPathBox.Text.Trim());
		if (string.IsNullOrWhiteSpace(cct))
		{
			AppDialog.ShowCompat(window, "没有找到 cct.exe。", "无法备份", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (selections == null || selections.Count < 2)
		{
			throw new InvalidOperationException("批量迁移至少需要两个项目。");
		}
		string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-batch-pack-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(temp);
		SetBusy(busy: true, includeProjectFiles ? "正在创建多项目完整迁移包……" : "正在创建多项目对话包……");
		try
		{
			PackManifest manifest = new PackManifest
			{
				schema = 5,
				created_at = DateTimeOffset.Now.ToString("o"),
				mode = includeProjectFiles ? "batch_projects_with_files" : "batch_selected_conversations",
				source_project = string.Empty,
				source_project_name = selections.Count + " 个项目",
				includes_subagents = includeProjectFiles,
				cct_version = ((await CctRunner.RunAsync(cct, new string[1] { "--version" }, null)).StdOut ?? string.Empty).Trim(),
				bundles = new List<string>(),
				sessions = new List<PackSession>(),
				projects = new List<PackProject>()
			};
			HashSet<string> targetFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			int projectIndex = 0;
			foreach (BackupProjectSelection selection in selections)
			{
				projectIndex++;
				int currentProjectIndex = projectIndex;
				ProjectGroup project = selection.Project;
				string projectKey = "project-" + currentProjectIndex.ToString("000");
				string projectRelativeDirectory = "projects/" + projectKey;
				string projectTempDirectory = System.IO.Path.Combine(temp, "projects", projectKey);
				Directory.CreateDirectory(projectTempDirectory);
				PackProject packProject = new PackProject
				{
					project_key = projectKey,
					source_project = project.ProjectPath,
					source_project_name = project.DisplayName,
					target_folder = UniqueProjectFolder(project.DisplayName, currentProjectIndex, targetFolders),
					bundles = new List<string>()
				};
				if (includeProjectFiles)
				{
					SetStatus($"正在封装第 {currentProjectIndex}/{selections.Count} 个项目的对话：{project.DisplayName}", error: false);
					string bundleRelative = projectRelativeDirectory + "/project.codexbundle";
					string bundlePath = System.IO.Path.Combine(projectTempDirectory, "project.codexbundle");
					if (fullFidelityCheck.IsChecked == true)
					{
						await Task.Run(delegate
						{
							ExactBundleWriter.CreateBundle(selection.Sessions, bundlePath, delegate(int index, int count, SessionInfo item)
							{
								if (index == 1 || index == count || index % 20 == 0)
								{
									window.Dispatcher.BeginInvoke((Action)delegate
									{
										SetStatus($"项目 {currentProjectIndex}/{selections.Count} · 对话 {index}/{count}：{item.DisplayTitle}", error: false);
									});
								}
							});
						});
					}
					else
					{
						CctResult export = await CctRunner.RunAsync(cct, new string[9]
						{
							"export", "--project", project.ProjectPath, "--include-archived", "--codex-home", CodexCatalog.ResolveCodexHome(), "--redact", "-o", bundlePath
						}, null);
						if (export.ExitCode != 0)
						{
							throw new InvalidOperationException(project.DisplayName + "：" + CctRunner.FirstUseful(export));
						}
					}
					packProject.bundles.Add(bundleRelative);
					manifest.bundles.Add(bundleRelative);
					foreach (SessionInfo session in selection.Sessions)
					{
						PackSession packed = ToPackSession(session, bundleRelative);
						packed.project_key = projectKey;
						manifest.sessions.Add(packed);
					}
					SetStatus($"正在压缩第 {currentProjectIndex}/{selections.Count} 个项目文件：{project.DisplayName}", error: false);
					string archivePath = System.IO.Path.Combine(projectTempDirectory, "project-files.zip");
					ProjectPayloadInfo payload = await Task.Run(delegate
					{
						return ProjectPayloadService.CreateArchive(project.ProjectPath, archivePath, output, delegate(int index, int count, string relativePath)
						{
							if (index == 1 || index == count || index % 50 == 0)
							{
								window.Dispatcher.BeginInvoke((Action)delegate
								{
									SetStatus($"项目 {currentProjectIndex}/{selections.Count} · 文件 {index}/{count}：{relativePath}", error: false);
								});
							}
						});
					});
					payload.archive_file = projectRelativeDirectory + "/project-files.zip";
					packProject.project_payload = payload;
				}
				else
				{
					int sessionIndex = 0;
					foreach (SessionInfo session in selection.Sessions)
					{
						sessionIndex++;
						SetStatus($"项目 {currentProjectIndex}/{selections.Count} · 正在备份对话 {sessionIndex}/{selection.Sessions.Count}：{session.DisplayTitle}", error: false);
						string bundleFileName = sessionIndex.ToString("000") + "-" + session.ThreadId + ".codexbundle";
						string conversationsDirectory = System.IO.Path.Combine(projectTempDirectory, "conversations");
						Directory.CreateDirectory(conversationsDirectory);
						string bundleRelative = projectRelativeDirectory + "/conversations/" + bundleFileName;
						await Task.Run(() => ExactBundleWriter.CreateSingleSessionBundle(session, System.IO.Path.Combine(conversationsDirectory, bundleFileName)));
						packProject.bundles.Add(bundleRelative);
						manifest.bundles.Add(bundleRelative);
						PackSession packed = ToPackSession(session, bundleRelative);
						packed.project_key = projectKey;
						manifest.sessions.Add(packed);
					}
				}
				manifest.projects.Add(packProject);
			}
			File.WriteAllText(System.IO.Path.Combine(temp, "manifest.json"), CctRunner.NewSerializer().Serialize(manifest), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(output))
			{
				throw new IOException("备份文件在创建期间已存在，请重试：\n" + output);
			}
			ZipFile.CreateFromDirectory(temp, output, CompressionLevel.Optimal, includeBaseDirectory: false);
			long fileCount = manifest.projects.Where((PackProject project) => project.project_payload != null).Sum((PackProject project) => (long)project.project_payload.file_count);
			long bytes = manifest.projects.Where((PackProject project) => project.project_payload != null).Sum((PackProject project) => project.project_payload.uncompressed_bytes);
			SetStatus("批量迁移包创建完成：" + output, error: false);
			string payloadSummary = includeProjectFiles ? $"\n项目文件：{fileCount} 个（{ProjectPayloadService.FormatBytes(bytes)}）" : string.Empty;
			AppDialog.ShowCompat(window, $"备份完成！\n\n项目：{selections.Count} 个\n对话记录：{manifest.sessions.Count}{payloadSummary}\n\n已保存到：\n{output}", "批量备份成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			SetStatus("批量备份失败：" + ex.Message, error: true);
			AppDialog.ShowCompat(window, ex.Message, "批量备份失败", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			TryDeleteDirectory(temp);
			SetBusy(busy: false, null);
		}
	}

	private static string UniqueProjectFolder(string displayName, int index, ISet<string> used)
	{
		string baseName = TextHelpers.SafeFileName(displayName).Trim().TrimEnd('.', ' ');
		if (string.IsNullOrWhiteSpace(baseName))
		{
			baseName = "项目-" + index.ToString("000");
		}
		if (IsReservedProjectFolder(baseName))
		{
			baseName = "_" + baseName;
		}
		string candidate = baseName;
		int suffix = 2;
		while (!used.Add(candidate))
		{
			candidate = baseName + "-" + suffix;
			suffix++;
		}
		return candidate;
	}

	private async Task CreatePackAsync(ProjectGroup project, IList<SessionInfo> sessions, bool wholeProject, bool includeProjectFiles, string output)
	{
		string cct = CctRunner.ResolveCctPath(cctPathBox.Text.Trim());
		if (string.IsNullOrWhiteSpace(cct))
		{
			AppDialog.ShowCompat(window, "没有找到 cct.exe。", "无法备份", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-pack-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(temp);
		SetBusy(busy: true, "正在创建迁移包……");
		try
		{
			PackManifest manifest = new PackManifest
			{
				schema = (includeProjectFiles ? 3 : 2),
				created_at = DateTimeOffset.Now.ToString("o"),
				mode = (includeProjectFiles ? "project_with_files" : (wholeProject ? "project" : "selected")),
				source_project = project.ProjectPath,
				source_project_name = project.DisplayName,
				includes_subagents = wholeProject,
				cct_version = string.Empty,
				bundles = new List<string>(),
				sessions = new List<PackSession>(),
				project_payload = null
			};
			manifest.cct_version = ((await CctRunner.RunAsync(cct, new string[1] { "--version" }, null)).StdOut ?? string.Empty).Trim();
			string policy = ((fullFidelityCheck.IsChecked == true) ? "--allow-secrets" : "--redact");
			if (wholeProject)
			{
				SetStatus("正在完整备份项目（包含子代理对话）……", error: false);
				string bundleName = "project.codexbundle";
				string bundlePath = System.IO.Path.Combine(temp, bundleName);
				if (fullFidelityCheck.IsChecked == true)
				{
					await Task.Run(delegate
					{
						ExactBundleWriter.CreateBundle(project.Sessions, bundlePath, delegate(int index, int count, SessionInfo item)
						{
							window.Dispatcher.BeginInvoke((Action)delegate
							{
								SetStatus($"正在校验并封装第 {index}/{count} 条记录：{item.DisplayTitle}", error: false);
							});
						});
					});
				}
				else
				{
					CctResult export = await CctRunner.RunAsync(cct, new string[9]
					{
						"export",
						"--project",
						project.ProjectPath,
						"--include-archived",
						"--codex-home",
						CodexCatalog.ResolveCodexHome(),
						policy,
						"-o",
						bundlePath
					}, null);
					if (export.ExitCode != 0)
					{
						throw new InvalidOperationException(CctRunner.FirstUseful(export));
					}
				}
				manifest.bundles.Add(bundleName);
				foreach (SessionInfo session2 in sessions)
				{
					manifest.sessions.Add(ToPackSession(session2, bundleName));
				}
			}
			else
			{
				int current2 = 0;
				foreach (SessionInfo session in sessions)
				{
					current2++;
					SetStatus($"正在备份第 {current2}/{sessions.Count} 个对话：{session.DisplayTitle}", error: false);
					string bundleName2 = current2.ToString("000") + "-" + session.ThreadId + ".codexbundle";
					string bundlePath2 = System.IO.Path.Combine(temp, bundleName2);
					await Task.Run(delegate
					{
						ExactBundleWriter.CreateSingleSessionBundle(session, bundlePath2);
					});
					manifest.bundles.Add(bundleName2);
					manifest.sessions.Add(ToPackSession(session, bundleName2));
				}
			}
			if (includeProjectFiles)
			{
				SetStatus("正在扫描并压缩项目目录……", error: false);
				string projectArchivePath = System.IO.Path.Combine(temp, "project-files.zip");
				manifest.project_payload = await Task.Run(delegate
				{
					return ProjectPayloadService.CreateArchive(project.ProjectPath, projectArchivePath, output, delegate(int index, int count, string relativePath)
					{
						if (index == 1 || index == count || index % 25 == 0)
						{
							window.Dispatcher.BeginInvoke((Action)delegate
							{
								SetStatus($"正在压缩项目文件 {index}/{count}：{relativePath}", error: false);
							});
						}
					});
				});
				SetStatus($"项目文件封装完成：{manifest.project_payload.file_count} 个文件，{ProjectPayloadService.FormatBytes(manifest.project_payload.uncompressed_bytes)}", error: false);
			}
			File.WriteAllText(System.IO.Path.Combine(temp, "manifest.json"), CctRunner.NewSerializer().Serialize(manifest), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(output))
			{
				throw new IOException("备份文件在创建期间已存在，请重试：\n" + output);
			}
			ZipFile.CreateFromDirectory(temp, output, CompressionLevel.Optimal, includeBaseDirectory: false);
			SetStatus("迁移包创建完成：" + output, error: false);
			string projectFilesSummary = (manifest.project_payload == null) ? string.Empty : $"\n项目文件：{manifest.project_payload.file_count} 个（{ProjectPayloadService.FormatBytes(manifest.project_payload.uncompressed_bytes)}）" + ((manifest.project_payload.skipped_reparse_points > 0) ? $"\n跳过重解析点：{manifest.project_payload.skipped_reparse_points} 个" : string.Empty);
			AppDialog.ShowCompat(window, $"备份完成！\n\n项目：{project.DisplayName}\n对话记录：{sessions.Count}" + projectFilesSummary + $"\n文件：{output}", "备份成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			SetStatus("备份失败：" + ex.Message, error: true);
			AppDialog.ShowCompat(window, ex.Message, "备份失败", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			TryDeleteDirectory(temp);
			SetBusy(busy: false, null);
		}
	}

	private static PackSession ToPackSession(SessionInfo session, string bundleName)
	{
		PackSession packSession = new PackSession();
		packSession.thread_id = session.ThreadId;
		packSession.origin_thread_id = string.IsNullOrWhiteSpace(session.OriginThreadId) ? session.ThreadId : session.OriginThreadId;
		packSession.title = session.DisplayTitle;
		packSession.preview = session.Preview;
		packSession.source = session.Source;
		packSession.updated_at = session.UpdatedAt;
		packSession.archived = session.Archived;
		packSession.compressed = session.Compressed;
		packSession.is_subagent = session.IsSubagent;
		packSession.bundle_file = bundleName;
		return packSession;
	}

	private async Task BrowsePackageAsync()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.Title = UiLanguage.T("选择 Codex 迁移包");
		openFileDialog.Filter = BackupPackageFormat.OpenDialogFilter;
		Microsoft.Win32.OpenFileDialog openFileDialog2 = openFileDialog;
		if (openFileDialog2.ShowDialog(window) == true)
		{
			packagePathBox.Text = openFileDialog2.FileName;
			SetBusy(busy: true, "正在读取迁移包清单……");
			try
			{
				await Task.Yield();
				await LoadPackageSummaryAsync(openFileDialog2.FileName);
			}
			finally
			{
				SetBusy(busy: false, null);
			}
		}
	}

	private void BrowseTarget()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = UiLanguage.T("选择这台电脑上的项目目录");
		folderBrowserDialog.ShowNewFolderButton = true;
		if (Directory.Exists(targetPathBox.Text))
		{
			folderBrowserDialog.SelectedPath = targetPathBox.Text;
		}
		if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
		{
			targetPathBox.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private async Task LoadPackageSummaryAsync(string path)
	{
		loadedManifest = null;
		loadedIsRawBundle = false;
		loadedPackagePath = string.Empty;
		projectRestorePanel.Visibility = Visibility.Collapsed;
		targetPathLabel.Text = UiLanguage.T("2 · 选择这台电脑上的项目目录");
		targetPathHelpText.Text = UiLanguage.T("目录可以与旧电脑不同，导入时会自动重写对话里的 cwd。");
		try
		{
			if (BackupPackageFormat.IsFormalPackage(path))
			{
				loadedManifest = await Task.Run(delegate
				{
					using ZipArchive zipArchive = ZipFile.OpenRead(path);
					ZipArchiveEntry entry = zipArchive.GetEntry("manifest.json");
					if (entry == null)
					{
						throw new InvalidDataException("迁移包缺少 manifest.json。");
					}
					using StreamReader streamReader = new StreamReader(entry.Open(), Encoding.UTF8);
					return CctRunner.NewSerializer().Deserialize<PackManifest>(streamReader.ReadToEnd());
				});
				if (loadedManifest == null)
				{
					throw new InvalidDataException("迁移包清单格式无效。");
				}
				int num = ((loadedManifest.sessions != null) ? loadedManifest.sessions.Count((PackSession x) => (loadedManifest.schema < 2) ? (!string.Equals(x.source, "subagent", StringComparison.OrdinalIgnoreCase)) : (!x.is_subagent)) : 0);
				List<PackProject> packProjects = ManifestProjects(loadedManifest);
				int payloadProjectCount = packProjects.Count((PackProject project) => project.project_payload != null);
				long payloadFileCount = packProjects.Where((PackProject project) => project.project_payload != null).Sum((PackProject project) => (long)project.project_payload.file_count);
				long payloadBytes = packProjects.Where((PackProject project) => project.project_payload != null).Sum((PackProject project) => project.project_payload.uncompressed_bytes);
				if (packProjects.Count > 1)
				{
					packageSummaryText.Text = string.Format(UiLanguage.T(payloadProjectCount > 0 ? "多项目完整迁移包 · {0} 个项目 · {1} 个主对话" : "多项目对话包 · {0} 个项目 · {1} 个主对话"), packProjects.Count, num);
					string names = string.Join("、", packProjects.Take(4).Select((PackProject project) => project.source_project_name));
					if (packProjects.Count > 4)
					{
						names += "等";
					}
					string payloadLine = payloadProjectCount > 0 ? "\n项目文件：" + payloadFileCount + " 个（" + ProjectPayloadService.FormatBytes(payloadBytes) + "）" : string.Empty;
					packageProjectText.Text = UiLanguage.T("包含项目：" + names + payloadLine + "\n创建时间：" + loadedManifest.created_at);
					restoreProjectFilesCheck.IsChecked = payloadProjectCount > 0;
					projectConflictCombo.SelectedIndex = 0;
					targetPathBox.Text = SuggestProjectTarget(loadedManifest);
					targetPathLabel.Text = UiLanguage.T("2 · 选择新电脑上的项目总目录");
					targetPathHelpText.Text = UiLanguage.T("每个项目会放入这个总目录下的独立子文件夹，并分别重写对应对话的 cwd。");
				}
				else if (LoadedHasProjectPayload())
				{
					ProjectPayloadInfo payload = packProjects[0].project_payload;
					packageSummaryText.Text = string.Format(UiLanguage.T("项目 + 对话完整包 · {0} 个主对话"), num);
					packageProjectText.Text = UiLanguage.T("原项目：" + loadedManifest.source_project + "\n项目文件：" + payload.file_count + " 个（" + ProjectPayloadService.FormatBytes(payload.uncompressed_bytes) + "）\n创建时间：" + loadedManifest.created_at);
					restoreProjectFilesCheck.IsChecked = true;
					projectConflictCombo.SelectedIndex = 0;
					targetPathBox.Text = SuggestProjectTarget(loadedManifest);
				}
				else
				{
					packageSummaryText.Text = string.Format(UiLanguage.T("{0} · {1} 个主对话"), UiLanguage.T((loadedManifest.mode == "project") ? "全部对话备份" : "已选对话备份"), num);
					string sourceProject = packProjects.FirstOrDefault()?.source_project ?? loadedManifest.source_project;
					packageProjectText.Text = UiLanguage.T("原项目：" + sourceProject + "\n创建时间：" + loadedManifest.created_at);
					restoreProjectFilesCheck.IsChecked = false;
					if (Directory.Exists(sourceProject))
					{
						targetPathBox.Text = sourceProject;
					}
				}
			}
			else
			{
				loadedIsRawBundle = true;
				packageSummaryText.Text = UiLanguage.T("原始 .codexbundle");
				packageProjectText.Text = UiLanguage.T("导入时会把包内项目映射到选定目录。");
				restoreProjectFilesCheck.IsChecked = false;
			}
			AppendLog("已选择：" + path);
			loadedPackagePath = System.IO.Path.GetFullPath(path);
		}
		catch (Exception ex)
		{
			loadedManifest = null;
			loadedIsRawBundle = false;
			loadedPackagePath = string.Empty;
			packageSummaryText.Text = UiLanguage.T("无法读取迁移包");
			packageProjectText.Text = UiLanguage.T(ex.Message);
			AppendLog("读取失败：" + ex.Message);
		}
		UpdateProjectRestoreControls();
	}

	private bool LoadedHasProjectPayload()
	{
		return ManifestProjects(loadedManifest).Any((PackProject project) => project.project_payload != null && !string.IsNullOrWhiteSpace(project.project_payload.archive_file));
	}

	private static List<PackProject> ManifestProjects(PackManifest manifest)
	{
		if (manifest == null)
		{
			return new List<PackProject>();
		}
		if (manifest.schema >= 4 && manifest.projects != null && manifest.projects.Count > 0)
		{
			return manifest.projects.Where((PackProject project) => project != null).ToList();
		}
		return new List<PackProject>
		{
			new PackProject
			{
				project_key = "project-001",
				source_project = manifest.source_project,
				source_project_name = manifest.source_project_name,
				target_folder = TextHelpers.SafeFileName(manifest.source_project_name),
				bundles = manifest.bundles ?? new List<string>(),
				project_payload = manifest.project_payload
			}
		};
	}

	private bool ShouldRestoreProjectFiles()
	{
		return LoadedHasProjectPayload() && restoreProjectFilesCheck.IsChecked == true;
	}

	private ProjectFileConflictMode CurrentProjectFileConflictMode()
	{
		System.Windows.Controls.ComboBoxItem item = projectConflictCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
		string tag = item?.Tag as string;
		if (string.Equals(tag, "skip", StringComparison.OrdinalIgnoreCase))
		{
			return ProjectFileConflictMode.SkipExisting;
		}
		if (string.Equals(tag, "overwrite", StringComparison.OrdinalIgnoreCase))
		{
			return ProjectFileConflictMode.OverwriteWithBackup;
		}
		return ProjectFileConflictMode.RequireEmpty;
	}

	private static string ProjectConflictModeText(ProjectFileConflictMode mode)
	{
		return mode switch
		{
			ProjectFileConflictMode.SkipExisting => "保留现有同名文件，只补充缺失文件",
			ProjectFileConflictMode.OverwriteWithBackup => "覆盖同名文件，并先创建恢复备份",
			_ => "要求目标目录为空"
		};
	}

	private void UpdateProjectRestoreControls()
	{
		bool hasPayload = LoadedHasProjectPayload();
		int projectCount = ManifestProjects(loadedManifest).Count;
		projectRestorePanel.Visibility = hasPayload ? Visibility.Visible : Visibility.Collapsed;
		restoreProjectFilesCheck.IsEnabled = hasPayload && !isBusy;
		bool restore = hasPayload && restoreProjectFilesCheck.IsChecked == true;
		projectConflictCombo.IsEnabled = restore && !isBusy;
		if (restore)
		{
			mapPathCheck.IsChecked = true;
			mapPathCheck.IsEnabled = false;
			ProjectFileConflictMode mode = CurrentProjectFileConflictMode();
			projectRestoreHelpText.Text = UiLanguage.T(mode switch
			{
				ProjectFileConflictMode.SkipExisting => (projectCount > 1 ? "每个项目只补充缺失文件；已有同名文件保持不变。" : "只补充目标目录缺失的项目文件；已有同名文件保持不变。") + " 会话仍导入 C 盘 Codex 目录。",
				ProjectFileConflictMode.OverwriteWithBackup => (projectCount > 1 ? "各项目的同名文件会被覆盖" : "同名项目文件会被覆盖") + "；覆盖前自动备份到 C 盘 Codex 配置目录。会话仍导入 C 盘。",
				_ => (projectCount > 1 ? "每个项目的目标子目录必须为空" : "目标目录必须为空") + "；项目文件还原后，会话仍导入 C 盘 Codex 目录。"
			});
		}
		else
		{
			mapPathCheck.IsEnabled = !isBusy;
			projectRestoreHelpText.Text = UiLanguage.T("已选择只导入包内对话，不还原项目文件。");
		}
	}

	private static string SuggestProjectTarget(PackManifest manifest)
	{
		string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		if (string.IsNullOrWhiteSpace(documents) || !Directory.Exists(documents))
		{
			documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		List<PackProject> packProjects = ManifestProjects(manifest);
		string name = packProjects.Count > 1 ? (UiLanguage.IsEnglish ? "Codex Imported Projects-" : "Codex 项目迁入-") + DateTime.Now.ToString("yyyyMMdd") : TextHelpers.SafeFileName(!string.IsNullOrWhiteSpace(packProjects.FirstOrDefault()?.source_project_name) ? packProjects[0].source_project_name : packProjects.FirstOrDefault()?.project_payload?.root_name);
		string candidate = System.IO.Path.Combine(documents, name);
		if (!Directory.Exists(candidate) || !Directory.EnumerateFileSystemEntries(candidate).Any())
		{
			return candidate;
		}
		string baseCandidate = candidate + (UiLanguage.IsEnglish ? "-imported" : "-迁入");
		candidate = baseCandidate;
		int suffix = 2;
		while (Directory.Exists(candidate) && Directory.EnumerateFileSystemEntries(candidate).Any())
		{
			candidate = baseCandidate + "-" + suffix;
			suffix++;
		}
		return candidate;
	}

	private string CurrentConflictMode()
	{
		if (copyModeRadio.IsChecked == true)
		{
			return "copy";
		}
		return "merge";
	}

	private void UpdateImportModeHelp()
	{
		if (copyModeRadio.IsChecked == true)
		{
			importModeHelpText.Text = UiLanguage.T("独立复制 · 每次都为主对话和子代理对话生成全新 Thread ID，并同步重写父子关系；原始编号只用于记录来源，两份文件互不共用。");
		}
		else
		{
			importModeHelpText.Text = UiLanguage.T("推荐 · 先按目标项目 + 原始编号查找已有对话：找到后合并；首次迁入则生成新 Thread ID，并把原始编号保存在会话中供下次识别。");
		}
	}


	internal static List<string> BuildImportConflictArguments(string mode)
	{
		return new List<string> { "--merge" };
	}


	private string ConflictModeConfirmation(string mode)
	{
		if (string.Equals(mode, "copy", StringComparison.OrdinalIgnoreCase))
		{
			return "独立复制：所有导入对话都生成全新 Thread ID，主对话与子代理的父子编号一起更新；原会话和复制后的会话使用不同文件，删除任意一份不会影响另一份。";
		}
		return "智能合并：只在目标项目内按原始编号查找；找到对应对话就合并，找不到则生成新 Thread ID。新编号会继续保存原始编号，便于以后再次备份和合并。";
	}


	internal static List<string> BuildProjectTargetPaths(IList<PackProject> packProjects, string selectedTarget)
	{
		if (packProjects == null || packProjects.Count == 0)
		{
			throw new InvalidDataException("迁移包没有项目清单。");
		}
		if (string.IsNullOrWhiteSpace(selectedTarget))
		{
			throw new InvalidOperationException(packProjects.Count > 1 ? "请选择新电脑上的项目总目录。" : "请选择新电脑上的项目目录。");
		}
		string root = System.IO.Path.GetFullPath(TextHelpers.StripExtendedPrefix(selectedTarget));
		HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> targets = new List<string>();
		for (int index = 0; index < packProjects.Count; index++)
		{
			PackProject project = packProjects[index];
			if (project == null || string.IsNullOrWhiteSpace(project.project_key) || !keys.Add(project.project_key))
			{
				throw new InvalidDataException("迁移包包含空白或重复的项目标识。");
			}
			if (packProjects.Count == 1)
			{
				targets.Add(root);
				continue;
			}
			string folder = ValidateProjectTargetFolder(project.target_folder, project.source_project_name, index + 1);
			if (!folders.Add(folder))
			{
				throw new InvalidDataException("迁移包包含重复的项目目标文件夹：" + folder);
			}
			string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, folder));
			string prefix = root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
			if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("项目目标文件夹越界：" + folder);
			}
			targets.Add(candidate);
		}
		return targets;
	}

	private static string ValidateProjectTargetFolder(string value, string fallback, int index)
	{
		string folder = string.IsNullOrWhiteSpace(value) ? TextHelpers.SafeFileName(fallback) : value.Trim();
		folder = folder.TrimEnd('.', ' ');
		if (string.IsNullOrWhiteSpace(folder))
		{
			folder = "项目-" + index.ToString("000");
		}
		if (folder == "." || folder == ".." || System.IO.Path.IsPathRooted(folder) || folder.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || !string.Equals(System.IO.Path.GetFileName(folder), folder, StringComparison.Ordinal) || IsReservedProjectFolder(folder))
		{
			throw new InvalidDataException("迁移包包含无效的项目目标文件夹：" + folder);
		}
		return folder;
	}

	private static bool IsReservedProjectFolder(string folder)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(folder).ToUpperInvariant();
		if (name == "CON" || name == "PRN" || name == "AUX" || name == "NUL")
		{
			return true;
		}
		return name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) && name[3] >= '1' && name[3] <= '9';
	}

	private static string ResolveExtractedPackageFile(string extractedRoot, string relativePath, string description)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
		{
			throw new InvalidDataException("迁移包包含空白的" + description + "路径。");
		}
		string root = System.IO.Path.GetFullPath(extractedRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
		string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(extractedRoot, relativePath));
		if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("迁移包包含越界的" + description + "路径：" + relativePath);
		}
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException("迁移包内缺少" + description + "：" + relativePath, fullPath);
		}
		return fullPath;
	}

	private static List<ImportProjectContext> BuildImportContexts(PackManifest manifest, string extractedRoot, string selectedTarget, bool mapProjectPath, bool restoreProjectFiles)
	{
		List<PackProject> packProjects = ManifestProjects(manifest);
		List<string> targetPaths = mapProjectPath ? BuildProjectTargetPaths(packProjects, selectedTarget) : Enumerable.Repeat(string.Empty, packProjects.Count).ToList();
		HashSet<string> assignedBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> bundleOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		List<ImportProjectContext> contexts = new List<ImportProjectContext>();
		for (int index = 0; index < packProjects.Count; index++)
		{
			PackProject project = packProjects[index];
			ImportProjectContext context = new ImportProjectContext
			{
				Project = project,
				TargetPath = targetPaths[index]
			};
			foreach (string bundleName in project.bundles ?? new List<string>())
			{
				if (!assignedBundles.Add(bundleName))
				{
					throw new InvalidDataException("迁移包把同一个对话包分配给了多个项目：" + bundleName);
				}
				bundleOwners[bundleName] = project.project_key;
				context.BundlePaths.Add(ResolveExtractedPackageFile(extractedRoot, bundleName, "对话包"));
			}
			if (restoreProjectFiles && project.project_payload != null)
			{
				context.ProjectArchivePath = ProjectPayloadService.ResolvePayloadArchivePath(extractedRoot, project.project_payload);
				if (!File.Exists(context.ProjectArchivePath))
				{
					throw new FileNotFoundException("迁移包内缺少项目文件载荷：" + project.project_payload.archive_file, context.ProjectArchivePath);
				}
			}
			contexts.Add(context);
		}
		HashSet<string> declaredBundles = new HashSet<string>(manifest.bundles ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		if ((manifest.bundles ?? new List<string>()).Count != declaredBundles.Count)
		{
			throw new InvalidDataException("迁移包的总对话包清单包含重复路径。");
		}
		if (manifest.schema >= 4 && !declaredBundles.SetEquals(assignedBundles))
		{
			throw new InvalidDataException("迁移包的总对话包清单与各项目清单不一致。");
		}
		if (assignedBundles.Count == 0)
		{
			throw new InvalidDataException("迁移包没有可导入的对话包。");
		}
		if (manifest.schema >= 4)
		{
			foreach (PackSession session in manifest.sessions ?? new List<PackSession>())
			{
				if (session == null || string.IsNullOrWhiteSpace(session.bundle_file) || !bundleOwners.TryGetValue(session.bundle_file, out string owner) || !string.Equals(owner, session.project_key, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("迁移包的对话与项目归属清单不一致：" + (session?.thread_id ?? "未知对话"));
				}
			}
		}
		return contexts;
	}

	private async Task ImportPackageAsync(bool dryRun)
	{
		if (isBusy)
		{
			return;
		}
		string cct = CctRunner.ResolveCctPath(cctPathBox.Text.Trim());
		if (string.IsNullOrWhiteSpace(cct))
		{
			AppDialog.Show(window, "缺少运行组件", "需要 cct.exe", "请先通过“运行组件…”选择 cct.exe，然后再检查或导入迁移包。", AppDialogTone.Warning, "返回设置");
			return;
		}
		string package = packagePathBox.Text.Trim();
		if (!File.Exists(package))
		{
			AppDialog.Show(window, "找不到备份文件", "请选择有效文件", "文件路径不存在或已经移动。请选择 .codexchat、.codexproject；旧版 .codexpack 和 .codexbundle 仍可导入。", AppDialogTone.Warning, "重新选择");
			return;
		}
		if (string.IsNullOrWhiteSpace(loadedPackagePath) || !string.Equals(TextHelpers.CanonicalPath(loadedPackagePath), TextHelpers.CanonicalPath(package), StringComparison.OrdinalIgnoreCase))
		{
			await LoadPackageSummaryAsync(package);
			if (BackupPackageFormat.IsFormalPackage(package) && loadedManifest == null)
			{
				AppDialog.Show(window, "迁移包无效", "无法读取迁移清单", "该文件没有可用的 manifest.json，或清单格式不受支持。详细原因已写入右侧操作记录。", AppDialogTone.Error);
				return;
			}
		}
		string target = targetPathBox.Text.Trim();
		bool restoreProjectFiles = ShouldRestoreProjectFiles();
		bool mapProjectPath = restoreProjectFiles || mapPathCheck.IsChecked == true;
		List<PackProject> previewProjects = loadedManifest == null ? new List<PackProject>
		{
			new PackProject { project_key = "raw-project", source_project_name = "原始对话包", bundles = new List<string>() }
		} : ManifestProjects(loadedManifest);
		List<string> previewTargets = null;
		try
		{
			if (mapProjectPath)
			{
				previewTargets = BuildProjectTargetPaths(previewProjects, target);
				if (!restoreProjectFiles)
				{
					List<string> missingTargets = previewTargets.Where((string path) => !Directory.Exists(path)).ToList();
					if (missingTargets.Count > 0)
					{
						throw new DirectoryNotFoundException("只导入对话时，目标项目目录必须已经存在：\n\n" + string.Join("\n", missingTargets));
					}
				}
			}
		}
		catch (Exception ex)
		{
			AppDialog.Show(window, "目标目录无效", "无法使用这个项目位置", ex.Message, AppDialogTone.Warning, "修改目录");
			return;
		}
		string codexHome = CodexCatalog.ResolveCodexHome();
		if (!dryRun)
		{
			try
			{
				CodexDesktopProjectRegistry.EnsureImportCanWrite(codexHome);
			}
			catch (Exception ex)
			{
				AppDialog.Show(window, "请先退出 Codex", "Codex 桌面端仍在运行", ex.Message, AppDialogTone.Warning, "我先退出 Codex");
				return;
			}
		}
		string conflictMode = CurrentConflictMode();
		ProjectFileConflictMode projectConflictMode = CurrentProjectFileConflictMode();
		if (!dryRun)
		{
			string targetLabel = previewProjects.Count > 1 ? "项目总目录" : "项目目录";
			string projectAction = restoreProjectFiles ? ("\n\n" + previewProjects.Count + " 个项目的文件将还原到" + targetLabel + "：\n" + target + "\n项目文件冲突策略：" + ProjectConflictModeText(projectConflictMode)) : "\n\n本次不还原项目文件。";
			bool answer = AppDialog.Confirm(window, "确认导入", "确认项目与对话的去向", "对话将导入本机 C 盘 Codex 目录；项目文件进入你选择的位置。\n\n对话冲突策略：\n" + ConflictModeConfirmation(conflictMode) + projectAction, restoreProjectFiles && projectConflictMode == ProjectFileConflictMode.OverwriteWithBackup ? AppDialogTone.Warning : AppDialogTone.Info, "开始导入");
			if (!answer)
			{
				return;
			}
		}
		BeginImportProgress(dryRun);
		SetBusy(busy: true, dryRun ? "正在检查迁移包……" : "正在恢复项目与对话……");
		await Task.Yield();
		string rewriteTemp = null;
		UpdateImportStage("1 / 4 · 读取迁移包", "正在解压清单并验证对话包结构。界面仍可响应，请勿重复点击。");
		string temp = null;
		CctBackupTransaction cctBackupTransaction = null;
		List<ImportProjectContext> contexts = new List<ImportProjectContext>();
		try
		{
			List<string> bundlePaths = new List<string>();
			if (loadedIsRawBundle || !BackupPackageFormat.IsFormalPackage(package))
			{
				ImportProjectContext rawContext = new ImportProjectContext
				{
					Project = previewProjects[0],
					TargetPath = mapProjectPath ? previewTargets[0] : string.Empty
				};
				rawContext.BundlePaths.Add(package);
				contexts.Add(rawContext);
				bundlePaths.Add(package);
			}
			else
			{
				temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-import-" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(temp);
				await Task.Run(() => ExtractZipSafely(package, temp));
				contexts = BuildImportContexts(loadedManifest, temp, target, mapProjectPath, restoreProjectFiles);
				bundlePaths.AddRange(contexts.SelectMany((ImportProjectContext context) => context.BundlePaths));
			}
			await Task.Run(() => TargetedThreadIndexer.ValidateBundles(bundlePaths));
			UpdateImportStage("2 / 4 · 检查项目文件", restoreProjectFiles ? "正在核对项目载荷、目标目录和同名文件冲突。" : "本次只处理对话，正在核对目标项目目录。");
			importLog.Clear();
			AppendLog(dryRun ? "开始安全检查（不会写入项目或会话）" : "开始正式导入");
			if (restoreProjectFiles)
			{
				int inspectIndex = 0;
				foreach (ImportProjectContext context in contexts.Where((ImportProjectContext item) => item.Project.project_payload != null))
				{
					inspectIndex++;
					SetStatus($"正在校验项目载荷 {inspectIndex}/{contexts.Count}：{context.Project.source_project_name}", error: false);
					context.Plan = await Task.Run(() => ProjectPayloadService.InspectArchive(context.ProjectArchivePath, context.Project.project_payload, context.TargetPath, projectConflictMode));
					context.TargetPath = context.Plan.TargetPath;
					AppendLog($"项目校验通过：{context.Project.source_project_name} · {context.Plan.FileCount} 个文件 · 新增 {context.Plan.NewFileCount} · 同名 {context.Plan.ExistingFileCount}\n目标：{context.TargetPath}");
				}
			}
			rewriteTemp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-lineage-import-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(rewriteTemp);
			ConversationImportPlanner importPlanner = await Task.Run(() => new ConversationImportPlanner(codexHome));
			bool independentCopy = string.Equals(conflictMode, "copy", StringComparison.OrdinalIgnoreCase);
			int planIndex = 0;
			foreach (ImportProjectContext context in contexts)
			{
				foreach (string sourceBundle in context.BundlePaths)
				{
					planIndex++;
					string rewrittenBundle = System.IO.Path.Combine(rewriteTemp, planIndex.ToString("000") + ".codexbundle");
					ConversationImportPlan plan = await Task.Run(() => importPlanner.CreatePlan(sourceBundle, rewrittenBundle, context.TargetPath, independentCopy));
					context.ImportPlans.Add(plan);
				}
			}
			List<ConversationImportPlan> importPlans = contexts.SelectMany(context => context.ImportPlans).ToList();
			List<string> effectiveBundlePaths = importPlans.Select(plan => plan.EffectiveBundlePath).ToList();
			Dictionary<string, string> targetByEffectiveBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (ConversationImportPlan plan in importPlans)
			{
				targetByEffectiveBundle[System.IO.Path.GetFullPath(plan.EffectiveBundlePath)] = plan.TargetPath;
			}
			int lineageMatchedCount = importPlans.Sum(plan => plan.MatchedCount);
			int lineageCreatedCount = importPlans.Sum(plan => plan.CreatedCount);
			if (independentCopy)
			{
				AppendLog("独立复制计划完成：将为 " + lineageCreatedCount + " 个对话生成全新 Thread ID；原始编号保留用于识别来源。");
			}
			else
			{
				AppendLog("智能合并计划完成：按目标项目 + 原始编号匹配 " + lineageMatchedCount + " 个；首次迁入并生成新 Thread ID " + lineageCreatedCount + " 个。");
			}
			HashSet<string> filesBeforeImport = await Task.Run(() => dryRun ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : TargetedThreadIndexer.SnapshotSessionFiles(codexHome));
			if (!dryRun)
			{
				cctBackupTransaction = await Task.Run(() => CctBackupTransaction.Begin(codexHome));
			}
			if (!dryRun && restoreProjectFiles)
			{
				List<ImportProjectContext> payloadContexts = contexts.Where((ImportProjectContext item) => item.Project.project_payload != null).ToList();
				for (int index = 0; index < payloadContexts.Count; index++)
				{
					ImportProjectContext context = payloadContexts[index];
					SetStatus($"正在还原项目 {index + 1}/{payloadContexts.Count}：{context.Project.source_project_name}", error: false);
					context.RestoreResult = await Task.Run(() => ProjectPayloadService.RestoreArchive(context.ProjectArchivePath, context.Project.project_payload, context.TargetPath, projectConflictMode));
					AppendLog($"项目还原完成：{context.Project.source_project_name} · 新增 {context.RestoreResult.CreatedFileCount} · 覆盖 {context.RestoreResult.OverwrittenFileCount} · 跳过 {context.RestoreResult.SkippedFileCount}\n目标：{context.RestoreResult.TargetPath}");
					if (!string.IsNullOrWhiteSpace(context.RestoreResult.BackupPath))
					{
						AppendLog("被覆盖项目文件的备份：" + context.RestoreResult.BackupPath);
					}
				}
			}
			UpdateImportStage("3 / 4 · " + (dryRun ? "模拟导入对话" : "导入对话"), $"共 {bundlePaths.Count} 个对话包；逐包处理时界面仍可响应。");
			int current = 0;
			foreach (ImportProjectContext context in contexts)
			{
				foreach (ConversationImportPlan plan in context.ImportPlans)
				{
					string bundle = plan.EffectiveBundlePath;
					current++;
					SetStatus(string.Format("{0}第 {1}/{2} 个对话包 · {3}", dryRun ? "检查" : "导入", current, bundlePaths.Count, context.Project.source_project_name), error: false);
					List<string> args = new List<string> { "import", bundle, "--codex-home", codexHome };
					string workDir = null;
					if (mapProjectPath)
					{
						string sourceProjectPath = context.Project.source_project;
						if (string.IsNullOrWhiteSpace(sourceProjectPath) && context.Project.project_payload != null)
						{
							sourceProjectPath = context.Project.project_payload.source_path;
						}
						if (!CctImportPathMapping.AddArguments(args, sourceProjectPath, context.TargetPath, out workDir))
						{
							AppendLog("源项目与目标项目相同，已跳过 cwd 映射；对话仍按当前项目路径导入。");
						}
					}
					args.AddRange(BuildImportConflictArguments(conflictMode));
					if (dryRun)
					{
						args.Add("--dry-run");
					}
					CctResult import = await CctRunner.RunAsync(cct, args, workDir);
					AppendLog("\n> " + import.CommandLine);
					if (!string.IsNullOrWhiteSpace(import.StdOut))
					{
						AppendLog(import.StdOut.TrimEnd());
					}
					if (!string.IsNullOrWhiteSpace(import.StdErr))
					{
						AppendLog(import.StdErr.TrimEnd());
					}
					if (import.ExitCode != 0)
					{
						throw new InvalidOperationException("cct 返回错误，详见操作记录。");
					}
				}
			}
			if (dryRun)
			{
				long inspectedFiles = contexts.Where((ImportProjectContext context) => context.Plan != null).Sum((ImportProjectContext context) => (long)context.Plan.FileCount);
				long inspectedBytes = contexts.Where((ImportProjectContext context) => context.Plan != null).Sum((ImportProjectContext context) => context.Plan.UncompressedBytes);
				int existingFiles = contexts.Where((ImportProjectContext context) => context.Plan != null).Sum((ImportProjectContext context) => context.Plan.ExistingFileCount);
				string projectCheck = restoreProjectFiles ? $"\n\n项目检查：{contexts.Count} 个项目、{inspectedFiles} 个文件（{ProjectPayloadService.FormatBytes(inspectedBytes)}），同名 {existingFiles} 个。" : string.Empty;
				SetStatus("安全检查完成，没有写入项目或会话。", error: false);
				AppDialog.Show(window, "检查完成", "迁移包可以导入", "本次检查没有写入项目文件或会话。" + projectCheck + $"\n\n对话包：{effectiveBundlePaths.Count} 个通过\n按原始编号匹配：{lineageMatchedCount} 个\n将生成全新编号：{lineageCreatedCount} 个\n\n确认右侧操作记录后即可正式导入。", AppDialogTone.Success, "返回导入页");
				return;
			}
			AppendLog("正在备份 Codex 索引与桌面项目状态，并只登记本次导入的会话……");
			UpdateImportStage("4 / 4 · 验证项目归属", "正在核对索引路径、会话文件、子代理父子关系和桌面侧栏项目归属。");
			Dictionary<string, string> titleHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (PackSession session in loadedManifest?.sessions ?? new List<PackSession>())
			{
				if (session != null && !string.IsNullOrWhiteSpace(session.thread_id))
				{
					titleHints[session.thread_id] = session.title;
				}
			}
			foreach (ConversationImportPlan plan in importPlans)
			{
				foreach (KeyValuePair<string, string> pair in plan.IdMap)
				{
					if (titleHints.TryGetValue(pair.Key, out string title))
					{
						titleHints[pair.Value] = title;
					}
				}
			}
			Dictionary<string, string> indexTargets = mapProjectPath ? targetByEffectiveBundle : null;
			TargetedIndexResult indexResult = await Task.Run(() => indexTargets == null ? TargetedThreadIndexer.IndexImportedSessions(codexHome, effectiveBundlePaths, filesBeforeImport, copiesOnly: false, null, titleHints) : TargetedThreadIndexer.IndexImportedSessionsMapped(codexHome, effectiveBundlePaths, filesBeforeImport, copiesOnly: false, indexTargets, titleHints));
			AppendLog($"索引路径验证：{indexResult.VisibilityVerifiedCount}/{indexResult.IndexedCount} 条通过；项目路径已使用原生索引格式。");
			if (indexResult.DesktopStateFound)
			{
				AppendLog($"桌面项目归属：{indexResult.DesktopAssignmentVerifiedCount}/{indexResult.DesktopAssignmentExpectedCount} 条主对话通过；已登记 {indexResult.DesktopProjectCount} 个项目。");
				if (!string.IsNullOrWhiteSpace(indexResult.DesktopStateBackupPath))
				{
					AppendLog("桌面项目状态备份：" + indexResult.DesktopStateBackupPath);
				}
			}
			else
			{
				AppendLog("未检测到 Codex 桌面项目状态文件；已按 CLI 环境跳过桌面侧栏项目归属。");
			}
			UpdateImportStage("4 / 4 · 验证通过", indexResult.DesktopStateFound ? $"{indexResult.VisibilityVerifiedCount}/{indexResult.IndexedCount} 条索引路径、{indexResult.DesktopAssignmentVerifiedCount}/{indexResult.DesktopAssignmentExpectedCount} 条桌面项目归属均已核验。" : $"{indexResult.VisibilityVerifiedCount}/{indexResult.IndexedCount} 条会话索引路径已核验。");
			AppendLog($"定点索引完成：新增 {indexResult.InsertedCount} 条，更新 {indexResult.UpdatedCount} 条；全局回填状态未修改。");
			int removedCctBackups = cctBackupTransaction == null ? 0 : await Task.Run(() => cctBackupTransaction.CommitAndDeleteTemporaryBackups());
			if (removedCctBackups > 0)
			{
				AppendLog("导入验证完成，已清理 cct 临时安全快照 " + removedCctBackups + " 个；不会留下 .cct-bak。");
			}
			if (!string.IsNullOrWhiteSpace(indexResult.BackupPath))
			{
				AppendLog("索引备份：" + indexResult.BackupPath);
			}
			List<ImportProjectContext> restored = contexts.Where((ImportProjectContext context) => context.RestoreResult != null).ToList();
			int createdFiles = restored.Sum((ImportProjectContext context) => context.RestoreResult.CreatedFileCount);
			int overwrittenFiles = restored.Sum((ImportProjectContext context) => context.RestoreResult.OverwrittenFileCount);
			int skippedFiles = restored.Sum((ImportProjectContext context) => context.RestoreResult.SkippedFileCount);
			string projectSuccess = restored.Count == 0 ? string.Empty : $"已还原 {restored.Count} 个项目到：\n{target}\n新增 {createdFiles} 个文件，覆盖 {overwrittenFiles} 个，跳过 {skippedFiles} 个。\n\n";
			string desktopSuccess = indexResult.DesktopStateFound ? $"\n桌面项目归属：{indexResult.DesktopAssignmentVerifiedCount}/{indexResult.DesktopAssignmentExpectedCount} 条主对话通过" : "\n桌面项目归属：未检测到桌面状态文件，已跳过";
			string desktopBackup = string.IsNullOrWhiteSpace(indexResult.DesktopStateBackupPath) ? string.Empty : "\n\n桌面项目状态备份：\n" + indexResult.DesktopStateBackupPath;
			SetStatus(restored.Count == 0 ? "对话导入完成。" : "项目与对话迁移完成。", error: false);
			AppDialog.Show(window, "迁移完成", "索引与桌面项目归属均已验证", projectSuccess + $"对话已导入 C 盘 Codex 目录，并分别关联到对应项目。\n\n新增索引：{indexResult.InsertedCount} 条\n更新索引：{indexResult.UpdatedCount} 条\n索引路径验证：{indexResult.VisibilityVerifiedCount}/{indexResult.IndexedCount} 条通过" + desktopSuccess + "\n\n现在重新打开 Codex，再打开迁入后的项目目录；对应主对话应直接出现在该项目侧栏中。" + (string.IsNullOrWhiteSpace(indexResult.BackupPath) ? string.Empty : "\n\n索引备份：\n" + indexResult.BackupPath) + desktopBackup, AppDialogTone.Success, "完成");
		}
		catch (OperationCanceledException ex)
		{
			await RollbackCctImportAsync(cctBackupTransaction);
			AppendLog("\n" + ex.Message);
			SetStatus("操作已取消，没有继续导入。", error: false);
		}
		catch (Exception ex)
		{
			await RollbackCctImportAsync(cctBackupTransaction);
			List<string> restoredTargets = contexts.Where((ImportProjectContext context) => context.RestoreResult != null).Select((ImportProjectContext context) => context.TargetPath).ToList();
			if (restoredTargets.Count > 0)
			{
				AppendLog("\n注意：以下项目文件已还原，但后续会话导入未完成：\n" + string.Join("\n", restoredTargets) + "\n修复问题后可以重新导入同一迁移包。");
			}
			AppendLog("\n失败：" + ex.Message);
			SetStatus("操作失败：" + ex.Message, error: true);
			AppDialog.Show(window, dryRun ? "检查失败" : "导入失败", dryRun ? "迁移包没有通过检查" : "导入没有完成", ex.Message + "\n\n项目文件若已还原，右侧操作记录会明确列出；修复问题后可以重新导入同一迁移包。", AppDialogTone.Error, "查看记录");
		}
		finally
		{
			if (rewriteTemp != null)
			{
				await Task.Run(() => TryDeleteDirectory(rewriteTemp));
			}
			if (temp != null)
			{
				await Task.Run(() => TryDeleteDirectory(temp));
			}
			EndImportProgress();
			SetBusy(busy: false, null);
		}
	}

	private async Task RollbackCctImportAsync(CctBackupTransaction transaction)
	{
		if (transaction == null)
		{
			return;
		}
		try
		{
			CctBackupRollbackResult rollback = await Task.Run(() => transaction.RollbackAndDeleteTemporaryBackups());
			if (rollback.RestoredCount > 0 || rollback.DeletedCount > 0)
			{
				AppendLog($"导入未完成：已从 cct 临时快照恢复 {rollback.RestoredCount} 个原会话，并清理 {rollback.DeletedCount} 个 .cct-bak 文件。");
			}
		}
		catch (Exception cleanupError)
		{
			AppendLog("警告：清理 cct 临时快照失败：" + cleanupError.Message);
		}
	}

	private async Task ImportPackageLegacyAsync(bool dryRun)
	{
		if (isBusy)
		{
			return;
		}
		string cct = CctRunner.ResolveCctPath(cctPathBox.Text.Trim());
		if (string.IsNullOrWhiteSpace(cct))
		{
			AppDialog.ShowCompat(window, "请先在“备份对话”页选择 cct.exe。", "缺少 cct.exe", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string package = packagePathBox.Text.Trim();
		if (!File.Exists(package))
		{
			AppDialog.ShowCompat(window, "请选择有效的迁移包。", "找不到文件", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (string.IsNullOrWhiteSpace(loadedPackagePath) || !string.Equals(TextHelpers.CanonicalPath(loadedPackagePath), TextHelpers.CanonicalPath(package), StringComparison.OrdinalIgnoreCase))
		{
			await LoadPackageSummaryAsync(package);
			if (BackupPackageFormat.IsFormalPackage(package) && loadedManifest == null)
			{
				AppDialog.ShowCompat(window, "无法读取迁移包清单，请查看操作记录。", "迁移包无效", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
		}
		string target = targetPathBox.Text.Trim();
		bool restoreProjectFiles = ShouldRestoreProjectFiles();
		bool mapProjectPath = restoreProjectFiles || mapPathCheck.IsChecked == true;
		if (mapProjectPath && !restoreProjectFiles && !Directory.Exists(target))
		{
			AppDialog.ShowCompat(window, "请选择这台电脑上已经存在的项目目录。", "目标目录无效", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string conflictMode = CurrentConflictMode();
		ProjectFileConflictMode projectConflictMode = CurrentProjectFileConflictMode();
		if (!dryRun)
		{
			string projectAction = restoreProjectFiles ? ("\n\n项目文件将还原到：\n" + target + "\n项目文件冲突策略：" + ProjectConflictModeText(projectConflictMode)) : "\n\n本次不还原项目文件。";
			MessageBoxResult messageBoxResult = AppDialog.ShowCompat(window, "即将执行迁移。\n\n会话将导入本机 C 盘 Codex 目录。\n当前会话冲突策略：\n" + ConflictModeConfirmation(conflictMode) + projectAction + "\n\n继续吗？", "确认导入", MessageBoxButton.YesNo, restoreProjectFiles && projectConflictMode == ProjectFileConflictMode.OverwriteWithBackup ? MessageBoxImage.Warning : MessageBoxImage.Question);
			if (messageBoxResult != MessageBoxResult.Yes)
			{
				return;
			}
		}
		SetBusy(busy: true, dryRun ? "正在检查项目与对话迁移包……" : "正在迁移项目与对话……");
		string temp = null;
		ProjectRestoreResult projectRestore = null;
		try
		{
			try
			{
				List<string> bundlePaths = new List<string>();
				string projectArchivePath = null;
				if (loadedIsRawBundle || !BackupPackageFormat.IsFormalPackage(package))
				{
					bundlePaths.Add(package);
				}
				else
				{
					if (loadedManifest == null)
					{
						await LoadPackageSummaryAsync(package);
					}
					if (loadedManifest == null)
					{
						throw new InvalidDataException("无法读取迁移包清单。");
					}
					temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-import-" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(temp);
					ExtractZipSafely(package, temp);
					foreach (string item in loadedManifest.bundles ?? new List<string>())
					{
						string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(temp, item));
						string value = System.IO.Path.GetFullPath(temp).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
						if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
						{
							throw new InvalidDataException("迁移包包含不安全路径。");
						}
						if (!File.Exists(fullPath))
						{
							throw new FileNotFoundException("迁移包内缺少文件：" + item);
						}
						bundlePaths.Add(fullPath);
					}
					if (restoreProjectFiles)
					{
						projectArchivePath = ProjectPayloadService.ResolvePayloadArchivePath(temp, loadedManifest.project_payload);
						if (!File.Exists(projectArchivePath))
						{
							throw new FileNotFoundException("迁移包内缺少项目文件载荷：" + loadedManifest.project_payload.archive_file, projectArchivePath);
						}
					}
				}
				TargetedThreadIndexer.ValidateBundles(bundlePaths);
				importLog.Clear();
				AppendLog(dryRun ? "开始安全检查（不会写入）" : "开始正式导入");
				ProjectRestorePlan projectPlan = null;
				if (restoreProjectFiles)
				{
					SetStatus("正在校验项目载荷和目标目录……", error: false);
					projectPlan = await Task.Run(() => ProjectPayloadService.InspectArchive(projectArchivePath, loadedManifest.project_payload, target, projectConflictMode));
					target = projectPlan.TargetPath;
					AppendLog($"项目载荷校验通过：{projectPlan.FileCount} 个文件，{ProjectPayloadService.FormatBytes(projectPlan.UncompressedBytes)}；新增 {projectPlan.NewFileCount} 个，同名 {projectPlan.ExistingFileCount} 个。");
				}
				string codexHome = CodexCatalog.ResolveCodexHome();
				Dictionary<string, string> indexedCwds = (mapProjectPath ? CodexCatalog.ReadIndexedThreadCwds() : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
				int pathMismatchCount = 0;
				Dictionary<string, HashSet<string>> mismatchByBundle = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
				foreach (string item2 in bundlePaths)
				{
					HashSet<string> hashSet = (mismatchByBundle[item2] = ((mapProjectPath && indexedCwds.Count > 0) ? BundleFreshIdRewriter.FindIndexedPathMismatches(item2, indexedCwds, target) : new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
					pathMismatchCount += hashSet.Count;
				}
				if (!dryRun && pathMismatchCount > 0 && string.Equals(conflictMode, "merge", StringComparison.OrdinalIgnoreCase))
				{
					MessageBoxResult messageBoxResult2 = AppDialog.ShowCompat(window, $"检测到 {pathMismatchCount} 个任务的原 ID 仍登记在旧项目路径。\n\n仅使用“智能合并”时，cwd 路径变化会被 cct 当成冲突。本次需要改为“迁移包为准”：覆盖同 ID 的本机旧文件，同时自动保留备份。\n\n是否继续？", "路径迁移需要保留原 ID", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (messageBoxResult2 != MessageBoxResult.Yes)
					{
						throw new OperationCanceledException("已取消导入；你也可以手动选择“迁移包为准”后重试。");
					}
				}
				DuplicateCleanupResult duplicateCleanup = new DuplicateCleanupResult();
				if (!dryRun && mapProjectPath)
				{
					duplicateCleanup = MigrationDuplicateCleaner.MoveVerifiedLegacyCopies(bundlePaths, codexHome, target);
					if (duplicateCleanup.MovedCount > 0)
					{
						AppendLog($"已将旧版迁移产生的 {duplicateCleanup.MovedCount} 个内容完全相同副本移到可恢复目录：\n{duplicateCleanup.TrashDirectory}");
					}
				}
				HashSet<string> filesBeforeImport = (dryRun ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : TargetedThreadIndexer.SnapshotSessionFiles(codexHome));
				if (!dryRun && restoreProjectFiles)
				{
					SetStatus("正在把项目文件还原到指定目录……", error: false);
					projectRestore = await Task.Run(() => ProjectPayloadService.RestoreArchive(projectArchivePath, loadedManifest.project_payload, target, projectConflictMode));
					AppendLog($"项目文件还原完成：新增 {projectRestore.CreatedFileCount} 个，覆盖 {projectRestore.OverwrittenFileCount} 个，跳过 {projectRestore.SkippedFileCount} 个。\n目标目录：{projectRestore.TargetPath}");
					if (!string.IsNullOrWhiteSpace(projectRestore.BackupPath))
					{
						AppendLog("被覆盖项目文件的备份：" + projectRestore.BackupPath);
					}
				}
				int current3 = 0;
				foreach (string bundle in bundlePaths)
				{
					current3++;
					SetStatus(string.Format("{0}第 {1}/{2} 个对话包……", dryRun ? "检查" : "导入", current3, bundlePaths.Count), error: false);
					HashSet<string> mismatched = mismatchByBundle[bundle];
					if (mismatched.Count > 0)
					{
						AppendLog(string.Format(string.Equals(conflictMode, "copy", StringComparison.OrdinalIgnoreCase) ? "检测到 {0} 个同 ID 任务登记在旧项目路径；本次将为迁入版本生成新 ID，并只登记新任务。" : "检测到 {0} 个同 ID 任务登记在旧项目路径；本次将保留原任务 ID，并只更新这些任务的索引。", mismatched.Count));
					}
					List<string> args = new List<string> { "import", bundle, "--codex-home", codexHome };
					string workDir = null;
					if (mapProjectPath)
					{
						string sourceProjectPath = loadedManifest?.source_project;
						if (string.IsNullOrWhiteSpace(sourceProjectPath) && loadedManifest?.project_payload != null)
						{
							sourceProjectPath = loadedManifest.project_payload.source_path;
						}
						if (!CctImportPathMapping.AddArguments(args, sourceProjectPath, target, out workDir))
						{
							AppendLog("源项目与目标项目相同，已跳过 cwd 映射。");
						}
					}
					string effectiveMode = conflictMode;
					if (mismatched.Count > 0 && string.Equals(conflictMode, "merge", StringComparison.OrdinalIgnoreCase))
					{
						effectiveMode = "replace";
						AppendLog(dryRun ? "安全检查将按“迁移包为准”模拟路径迁移。" : "路径迁移已切换为“迁移包为准”，本机旧文件将由 cct 自动备份。");
					}
					args.AddRange(BuildImportConflictArguments(effectiveMode));
					if (dryRun)
					{
						args.Add("--dry-run");
					}
					CctResult import = await CctRunner.RunAsync(cct, args, workDir);
					AppendLog("\n> " + import.CommandLine);
					if (!string.IsNullOrWhiteSpace(import.StdOut))
					{
						AppendLog(import.StdOut.TrimEnd());
					}
					if (!string.IsNullOrWhiteSpace(import.StdErr))
					{
						AppendLog(import.StdErr.TrimEnd());
					}
					if (import.ExitCode != 0)
					{
						throw new InvalidOperationException("cct 返回错误，详见操作记录。");
					}
				}
				if (dryRun)
				{
					SetStatus((pathMismatchCount > 0) ? string.Format(string.Equals(conflictMode, "copy", StringComparison.OrdinalIgnoreCase) ? "安全检查完成：{0} 个冲突版本将生成新 ID。" : "安全检查完成：{0} 个任务将保留原 ID并迁移路径。", pathMismatchCount) : "安全检查完成，没有写入项目或会话。", error: false);
					string projectCheck = (projectPlan == null) ? string.Empty : $"\n\n项目文件检查：{projectPlan.FileCount} 个文件（{ProjectPayloadService.FormatBytes(projectPlan.UncompressedBytes)}），新增 {projectPlan.NewFileCount} 个，同名 {projectPlan.ExistingFileCount} 个。";
					string conversationCheck = (pathMismatchCount > 0) ? string.Format("\n\n检测到 {0} 个任务仍登记在旧项目。" + (string.Equals(conflictMode, "copy", StringComparison.OrdinalIgnoreCase) ? "正式导入时会为迁入版本生成新 ID，保留本机旧文件，并只登记新任务。" : "正式导入时会保留原 ID、备份本机旧文件，并只更新本次任务索引。"), pathMismatchCount) : "\n\n对话包检查通过。";
					AppDialog.ShowCompat(window, "检查完成，没有写入项目文件或会话。" + projectCheck + conversationCheck + "\n\n确认操作记录后即可正式导入。", "检查完成", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					return;
				}
				AppendLog("正在备份 Codex 索引，并只登记本次导入的会话……");
				Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (PackSession item3 in (loadedManifest == null) ? new List<PackSession>() : (loadedManifest.sessions ?? new List<PackSession>()))
				{
					if (item3 != null && !string.IsNullOrWhiteSpace(item3.thread_id))
					{
						dictionary[item3.thread_id] = item3.title;
					}
				}
				TargetedIndexResult targetedIndexResult = TargetedThreadIndexer.IndexImportedSessions(codexHome, bundlePaths, filesBeforeImport, string.Equals(conflictMode, "copy", StringComparison.OrdinalIgnoreCase), mapProjectPath ? target : null, dictionary);
				AppendLog($"定点索引完成：新增 {targetedIndexResult.InsertedCount} 条，更新 {targetedIndexResult.UpdatedCount} 条；全局回填状态未修改。");
				if (!string.IsNullOrWhiteSpace(targetedIndexResult.BackupPath))
				{
					AppendLog("索引备份：" + targetedIndexResult.BackupPath);
				}
				SetStatus(projectRestore == null ? "导入完成，本次会话已完成定点登记。" : "项目与对话迁移完成。", error: false);
				string text = ((duplicateCleanup.MovedCount > 0) ? $"\n\n另外，已将旧版产生的 {duplicateCleanup.MovedCount} 个重复副本移入可恢复目录：\n{duplicateCleanup.TrashDirectory}" : string.Empty);
				string projectSuccess = (projectRestore == null) ? string.Empty : $"项目文件已还原到：\n{projectRestore.TargetPath}\n新增 {projectRestore.CreatedFileCount} 个，覆盖 {projectRestore.OverwrittenFileCount} 个，跳过 {projectRestore.SkippedFileCount} 个。\n\n" + (string.IsNullOrWhiteSpace(projectRestore.BackupPath) ? string.Empty : ("项目覆盖备份：\n" + projectRestore.BackupPath + "\n\n"));
				AppDialog.ShowCompat(window, projectSuccess + $"对话已导入 C 盘 Codex 目录并完成定点索引。\n\n新增索引 {targetedIndexResult.InsertedCount} 条，更新索引 {targetedIndexResult.UpdatedCount} 条。\n全局会话回填状态保持原值，不会扫描全部历史会话。\n\n请完全退出并重新打开 Codex，让侧栏重新读取索引。" + text + (string.IsNullOrWhiteSpace(targetedIndexResult.BackupPath) ? string.Empty : ("\n\n索引备份：\n" + targetedIndexResult.BackupPath)), "迁移成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			catch (OperationCanceledException ex)
			{
				AppendLog("\n" + ex.Message);
				SetStatus("操作已取消，没有继续导入。", error: false);
			}
			catch (Exception ex2)
			{
				if (projectRestore != null)
				{
					AppendLog("\n注意：项目文件已还原到 " + projectRestore.TargetPath + "，但后续会话导入未完成。修复问题后可以重新导入同一迁移包。");
				}
				AppendLog("\n失败：" + ex2.Message);
				SetStatus("操作失败：" + ex2.Message, error: true);
				AppDialog.ShowCompat(window, ex2.Message, dryRun ? "检查失败" : "导入失败", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		finally
		{
			if (temp != null)
			{
				TryDeleteDirectory(temp);
			}
			SetBusy(busy: false, null);
		}
	}

	private void BeginImportProgress(bool dryRun)
	{
		importStartedAt = DateTime.UtcNow;
		importProgressPanel.Visibility = Visibility.Visible;
		importStageProgress.IsIndeterminate = true;
		inspectButton.Content = UiLanguage.T(dryRun ? "正在检查…" : "先检查（不写入）");
		importButton.Content = UiLanguage.T(dryRun ? "开始导入" : "正在导入…");
		UpdateImportStage(dryRun ? "准备安全检查" : "准备导入", "正在初始化，请稍候。");
	}

	private void UpdateImportStage(string stage, string detail)
	{
		if (importStartedAt == DateTime.MinValue)
		{
			importStartedAt = DateTime.UtcNow;
		}
		TimeSpan elapsed = DateTime.UtcNow - importStartedAt;
		importStageText.Text = UiLanguage.T(stage);
		importStageDetailText.Text = UiLanguage.T(detail);
		importElapsedText.Text = elapsed.TotalHours >= 1.0 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"mm\:ss");
		importProgressPanel.Visibility = Visibility.Visible;
		SetStatus(stage, error: false);
	}

	private void EndImportProgress()
	{
		importElapsedText.Text = string.Empty;
		importProgressPanel.Visibility = Visibility.Collapsed;
		inspectButton.Content = UiLanguage.T("先检查（不写入）");
		importButton.Content = UiLanguage.T("开始导入");
		importStartedAt = DateTime.MinValue;
	}
	private void SetBusy(bool busy, string message)
	{
		isBusy = busy;
		busyProgress.Visibility = ((!busy) ? Visibility.Collapsed : Visibility.Visible);
		window.Cursor = (busy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow);
		refreshButton.IsEnabled = !busy;
		browseCctButton.IsEnabled = !busy;
		browseBackupFolderButton.IsEnabled = !busy;
		browsePackageButton.IsEnabled = !busy;
		browseTargetButton.IsEnabled = !busy;
		packagePathBox.IsReadOnly = busy;
		targetPathBox.IsReadOnly = busy;
		cctPathBox.IsReadOnly = busy;
		mapPathCheck.IsEnabled = !busy;
		backupTabButton.IsEnabled = !busy;
		importTabButton.IsEnabled = !busy;
		trashButton.IsEnabled = !busy;
		backupFolderBox.IsEnabled = !busy;
		projectBackupModeRadio.IsEnabled = !busy;
		conversationBackupModeRadio.IsEnabled = !busy;
		mainSessionsTabRadio.IsEnabled = !busy;
		subagentSessionsTabRadio.IsEnabled = !busy;
		selectAllProjectsButton.IsEnabled = !busy;
		clearProjectsButton.IsEnabled = !busy;
		inspectButton.IsEnabled = !busy;
		importButton.IsEnabled = !busy;
		sessionList.IsEnabled = !busy;
		mergeModeRadio.IsEnabled = !busy;
		copyModeRadio.IsEnabled = !busy;
		projectList.IsEnabled = !busy;
		fullFidelityCheck.IsEnabled = !busy;
		UpdateSessionTypeView();
		UpdateSelectedCount();
		UpdateProjectRestoreControls();
		if (!string.IsNullOrWhiteSpace(message))
		{
			SetStatus(message, error: false);
		}
		languageButton.IsEnabled = !busy;
	}

	private void SetStatus(string text, bool error)
	{
		statusText.Text = UiLanguage.T(text);
		statusDot.Fill = (error ? Brush("#D6534D") : Brush("#0D9F76"));
	}

	private void AppendLog(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			string localized = UiLanguage.T(text);
			if (importLog.Text == UiLanguage.T("等待选择迁移包……"))
			{
				importLog.Clear();
			}
			importLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + localized + Environment.NewLine);
			importLog.CaretIndex = importLog.Text.Length;
			importLog.ScrollToEnd();
		}
	}

	private static Brush Brush(string color)
	{
		return (Brush)new BrushConverter().ConvertFromString(color);
	}

	private static void ExtractZipSafely(string zipPath, string destination)
	{
		string value = System.IO.Path.GetFullPath(destination).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
		using ZipArchive zipArchive = ZipFile.OpenRead(zipPath);
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(destination, entry.FullName));
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("迁移包包含越界路径，已拒绝解压。");
			}
			if (string.IsNullOrEmpty(entry.Name))
			{
				Directory.CreateDirectory(fullPath);
				continue;
			}
			string directoryName = System.IO.Path.GetDirectoryName(fullPath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			using Stream stream = entry.Open();
			using FileStream destination2 = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			stream.CopyTo(destination2);
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && System.IO.Path.GetFullPath(path).StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}
}
