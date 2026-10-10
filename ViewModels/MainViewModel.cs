using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Uttu.Helpers;
using Uttu.Models;
using Uttu.Services;

namespace Uttu.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _db;
    private readonly SecurityService _sec;
    private readonly BufferMcpClient _mcp;
    private readonly CardRendererService _cardRenderer;
    private readonly ToneAnalyzerService _toneAnalyzer;
    private readonly IdeaGeneratorService _ideaGenerator;
    private readonly ToastNotificationService _toastService;
    private readonly ProactiveSchedulerService _scheduler;

    private readonly DispatcherTimer _ramTimer;

    // Collections
    public ObservableCollection<PostDraft> Drafts { get; } = new();
    public ObservableCollection<MemoryItem> Memories { get; } = new();
    public ObservableCollection<MemoryItem> FilteredMemories { get; } = new();
    public ObservableCollection<BufferProfile> BufferProfiles { get; } = new();

    // State
    private VoiceProfile _voiceProfile = new();
    private AppSettings _settings = new();
    private int _selectedTabIndex = 0;
    private string _selectedCategory = "Todas";
    private string _newMemoryText = string.Empty;
    private string _newMemoryCategory = "Ganchos";
    private string _samplePostsText = string.Empty;
    private string? _cardPreviewPath;
    private bool _cardPreviewDarkMode = true;
    private string _bufferTokenInput = string.Empty;
    private string _aiApiKeyInput = string.Empty;
    private string _mcpStatusText = "Conectando ao Buffer MCP...";
    private string _nextRunText = "08:30 (Amanhã)";
    private string _ramText = "RAM: ~45 MB";
    private string _selectedChannelDisplay = "X @usuario | LinkedIn";
    private bool _isLoading;
    private string _infoBannerMessage = string.Empty;

    public Action? RequestShowWindow { get; set; }

    #region Properties

    public VoiceProfile VoiceProfile
    {
        get => _voiceProfile;
        set => SetField(ref _voiceProfile, value);
    }

    public AppSettings Settings
    {
        get => _settings;
        set => SetField(ref _settings, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value))
            {
                ApplyMemoryFilter();
            }
        }
    }

    public string NewMemoryText
    {
        get => _newMemoryText;
        set => SetField(ref _newMemoryText, value);
    }

    public string NewMemoryCategory
    {
        get => _newMemoryCategory;
        set => SetField(ref _newMemoryCategory, value);
    }

    public string SamplePostsText
    {
        get => _samplePostsText;
        set => SetField(ref _samplePostsText, value);
    }

    public string? CardPreviewPath
    {
        get => _cardPreviewPath;
        set => SetField(ref _cardPreviewPath, value);
    }

    public bool CardPreviewDarkMode
    {
        get => _cardPreviewDarkMode;
        set => SetField(ref _cardPreviewDarkMode, value);
    }

    public string BufferTokenInput
    {
        get => _bufferTokenInput;
        set => SetField(ref _bufferTokenInput, value);
    }

    public string AiApiKeyInput
    {
        get => _aiApiKeyInput;
        set => SetField(ref _aiApiKeyInput, value);
    }

    public string McpStatusText
    {
        get => _mcpStatusText;
        set => SetField(ref _mcpStatusText, value);
    }

    public string NextRunText
    {
        get => _nextRunText;
        set => SetField(ref _nextRunText, value);
    }

    public string RamText
    {
        get => _ramText;
        set => SetField(ref _ramText, value);
    }

    public string SelectedChannelDisplay
    {
        get => _selectedChannelDisplay;
        set => SetField(ref _selectedChannelDisplay, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string InfoBannerMessage
    {
        get => _infoBannerMessage;
        set
        {
            if (SetField(ref _infoBannerMessage, value))
            {
                OnPropertyChanged(nameof(HasInfoBanner));
            }
        }
    }

    public bool HasInfoBanner => !string.IsNullOrEmpty(_infoBannerMessage);

    public int DraftsCount => Drafts.Count;
    public int ActiveMemoriesCount => Memories.Count(m => m.IsActive);

    #endregion

    #region Commands

    public ICommand GenerateDraftsCommand { get; }
    public ICommand ApproveDraftCommand { get; }
    public ICommand PublishNowCommand { get; }
    public ICommand DiscardDraftCommand { get; }
    public ICommand RefineDraftCommand { get; }
    public ICommand GenerateCardCommand { get; }
    public ICommand ToggleMemoryCommand { get; }
    public ICommand DeleteMemoryCommand { get; }
    public ICommand AddMemoryCommand { get; }
    public ICommand CalibrateVoiceCommand { get; }
    public ICommand SaveVoiceCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ReconnectMcpCommand { get; }
    public ICommand OpenCacheFolderCommand { get; }
    public ICommand DismissBannerCommand { get; }
    public ICommand ShowWindowCommand { get; }
    public ICommand ExitAppCommand { get; }

    #endregion

    public MainViewModel()
    {
        _db = new DatabaseService();
        _sec = new SecurityService();
        _mcp = new BufferMcpClient();
        _cardRenderer = new CardRendererService();
        _toneAnalyzer = new ToneAnalyzerService();
        _ideaGenerator = new IdeaGeneratorService();
        _toastService = new ToastNotificationService();
        _scheduler = new ProactiveSchedulerService(_db, _ideaGenerator, _toastService);

        // Commands
        GenerateDraftsCommand = new RelayCommand(async _ => await GenerateDraftsAsync());
        ApproveDraftCommand = new RelayCommand(async p => await ApproveDraftAsync(p as PostDraft));
        PublishNowCommand = new RelayCommand(async p => await PublishNowAsync(p as PostDraft));
        DiscardDraftCommand = new RelayCommand(async p => await DiscardDraftAsync(p as PostDraft));
        RefineDraftCommand = new RelayCommand(async p => await RefineDraftAsync(p as PostDraft));
        GenerateCardCommand = new RelayCommand(async p => await GenerateCardAsync(p as PostDraft));
        ToggleMemoryCommand = new RelayCommand(async m => await ToggleMemoryAsync(m as MemoryItem));
        DeleteMemoryCommand = new RelayCommand(async m => await DeleteMemoryAsync(m as MemoryItem));
        AddMemoryCommand = new RelayCommand(async _ => await AddMemoryAsync());
        CalibrateVoiceCommand = new RelayCommand(_ => CalibrateVoice());
        SaveVoiceCommand = new RelayCommand(async _ => await SaveVoiceAsync());
        SaveSettingsCommand = new RelayCommand(async _ => await SaveSettingsAsync());
        ReconnectMcpCommand = new RelayCommand(async _ => await InitializeMcpAsync());
        OpenCacheFolderCommand = new RelayCommand(_ => OpenCacheFolder());
        DismissBannerCommand = new RelayCommand(_ => InfoBannerMessage = string.Empty);
        ShowWindowCommand = new RelayCommand(_ => RequestShowWindow?.Invoke());
        ExitAppCommand = new RelayCommand(_ => Application.Current.Shutdown());

        // RAM monitor timer
        _ramTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _ramTimer.Tick += (s, e) => UpdateRamMetric();
        _ramTimer.Start();

        // Toast notifications hooks
        _toastService.Initialize();
        _toastService.OnApproveRequested += async id =>
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var draft = Drafts.FirstOrDefault(d => d.Id == id);
                if (draft != null)
                {
                    await ApproveDraftAsync(draft);
                }
                else
                {
                    await _db.UpdatePostStatusAsync(id, "Approved");
                }
            });
        };

        _toastService.OnOpenRequested += () =>
        {
            Application.Current.Dispatcher.Invoke(() => RequestShowWindow?.Invoke());
        };

        _scheduler.OnNewDraftsGenerated += newDrafts =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var d in newDrafts)
                {
                    Drafts.Insert(0, d);
                }
                OnPropertyChanged(nameof(DraftsCount));
                NextRunText = _scheduler.NextRunFormatted;
            });
        };

        _mcp.StatusChanged += status =>
        {
            Application.Current.Dispatcher.Invoke(() => McpStatusText = status);
        };
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await _db.InitializeAsync();
            Settings = await _db.GetSettingsAsync();
            VoiceProfile = await _db.GetVoiceProfileAsync();

            BufferTokenInput = SecurityService.Unprotect(Settings.BufferAccessTokenEncrypted);
            AiApiKeyInput = SecurityService.Unprotect(Settings.AiApiKeyEncrypted);
            SamplePostsText = VoiceProfile.SampleText;

            // Load Drafts
            var drafts = await _db.GetDraftPostsAsync();
            Drafts.Clear();
            foreach (var d in drafts)
            {
                Drafts.Add(d);
            }
            OnPropertyChanged(nameof(DraftsCount));

            // Load Memories
            var memories = await _db.GetMemoriesAsync();
            Memories.Clear();
            foreach (var m in memories)
            {
                Memories.Add(m);
            }
            ApplyMemoryFilter();
            OnPropertyChanged(nameof(ActiveMemoriesCount));

            // Start MCP
            await InitializeMcpAsync();

            // Start Scheduler
            _scheduler.Start(Settings);
            NextRunText = _scheduler.NextRunFormatted;
            UpdateRamMetric();

            // Set initial card preview if available
            if (Drafts.Count > 0)
            {
                CardPreviewPath = Drafts.First().CardImagePath;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task InitializeMcpAsync()
    {
        string token = BufferTokenInput;
        bool ok = await _mcp.StartAsync(Settings.BufferMcpCommand, Settings.BufferMcpArgs, token);
        McpStatusText = _mcp.StatusMessage;

        var profiles = await _mcp.ListProfilesAsync();
        BufferProfiles.Clear();
        foreach (var p in profiles)
        {
            BufferProfiles.Add(p);
        }

        UpdateChannelDisplay();
    }

    private void UpdateChannelDisplay()
    {
        var selected = BufferProfiles.Where(p => p.IsSelected).ToList();
        if (selected.Count > 0)
        {
            SelectedChannelDisplay = string.Join(" | ", selected.Select(s => $"{s.FormattedService} {s.FormattedUsername}"));
        }
        else
        {
            SelectedChannelDisplay = "X @usuario | LinkedIn";
        }
    }

    private void ApplyMemoryFilter()
    {
        FilteredMemories.Clear();
        var list = SelectedCategory == "Todas"
            ? Memories
            : Memories.Where(m => m.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));

        foreach (var item in list)
        {
            FilteredMemories.Add(item);
        }
    }

    private void UpdateRamMetric()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            long mb = proc.WorkingSet64 / (1024 * 1024);
            RamText = $"RAM: {mb} MB (Standby Nativo)";
        }
        catch { }
    }

    #region Drafts Actions

    public async Task GenerateDraftsAsync()
    {
        IsLoading = true;
        InfoBannerMessage = "Gerando novos rascunhos proativos com base nas memórias ativas...";
        try
        {
            var newDrafts = await _scheduler.TriggerNowAsync();
            NextRunText = _scheduler.NextRunFormatted;
            InfoBannerMessage = $"{newDrafts.Count} novos rascunhos formulados com sucesso!";
        }
        catch (Exception ex)
        {
            InfoBannerMessage = $"Erro ao gerar rascunhos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ApproveDraftAsync(PostDraft? draft)
    {
        if (draft == null) return;

        IsLoading = true;
        try
        {
            var profileIds = BufferProfiles.Where(p => p.IsSelected).Select(p => p.Id).ToList();
            if (profileIds.Count == 0)
            {
                profileIds.Add("prof_x_01");
            }

            var result = await _mcp.CreateUpdateAsync(profileIds, draft.Content, publishNow: false, cardImagePath: draft.CardImagePath);

            draft.Status = "Approved";
            draft.BufferUpdateId = result.UpdateId;
            await _db.SavePostAsync(draft);

            Drafts.Remove(draft);
            OnPropertyChanged(nameof(DraftsCount));

            InfoBannerMessage = $"✓ Post aprovado e enviado para a fila do Buffer! ({result.Message})";
            _toastService.ShowSuccessToast("Uttu · Post Aprovado", $"Enviado com sucesso à fila do Buffer ({draft.Platform})");
        }
        catch (Exception ex)
        {
            InfoBannerMessage = $"Erro ao aprovar post: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PublishNowAsync(PostDraft? draft)
    {
        if (draft == null) return;

        IsLoading = true;
        try
        {
            var profileIds = BufferProfiles.Where(p => p.IsSelected).Select(p => p.Id).ToList();
            if (profileIds.Count == 0) profileIds.Add("prof_x_01");

            var result = await _mcp.CreateUpdateAsync(profileIds, draft.Content, publishNow: true, cardImagePath: draft.CardImagePath);

            draft.Status = "Published";
            draft.BufferUpdateId = result.UpdateId;
            await _db.SavePostAsync(draft);

            Drafts.Remove(draft);
            OnPropertyChanged(nameof(DraftsCount));

            InfoBannerMessage = $"✓ Publicado imediatamente na rede social via Buffer MCP!";
            _toastService.ShowSuccessToast("Uttu · Publicação Imediata", $"Publicado com sucesso no canal {draft.Platform}");
        }
        catch (Exception ex)
        {
            InfoBannerMessage = $"Erro ao publicar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DiscardDraftAsync(PostDraft? draft)
    {
        if (draft == null) return;

        draft.Status = "Discarded";
        await _db.DeletePostAsync(draft.Id);
        Drafts.Remove(draft);
        OnPropertyChanged(nameof(DraftsCount));
        InfoBannerMessage = "Rascunho descartado.";
    }

    public async Task RefineDraftAsync(PostDraft? draft)
    {
        if (draft == null || string.IsNullOrWhiteSpace(draft.RefinePrompt)) return;

        IsLoading = true;
        try
        {
            string instruction = draft.RefinePrompt;
            var activeMems = Memories.Where(m => m.IsActive).ToList();
            string refined = await _ideaGenerator.RefineDraftAsync(draft, instruction, activeMems, VoiceProfile, Settings);

            draft.Content = refined;
            draft.RefinePrompt = string.Empty;
            await _db.SavePostAsync(draft);

            InfoBannerMessage = "✓ Texto refinado com sucesso!";
        }
        catch (Exception ex)
        {
            InfoBannerMessage = $"Erro no refinamento: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task GenerateCardAsync(PostDraft? draft)
    {
        if (draft == null) return;

        IsLoading = true;
        try
        {
            string cardPath = _cardRenderer.RenderSocialCard(
                draft, 
                authorName: Settings.DefaultAuthorName, 
                authorHandle: Settings.DefaultAuthorHandle, 
                darkMode: CardPreviewDarkMode
            );

            draft.CardImagePath = cardPath;
            await _db.SavePostAsync(draft);

            CardPreviewPath = cardPath;
            SelectedTabIndex = 3; // Navigate to Card Generator tab
            InfoBannerMessage = "✓ Cartão 1600x900 gerado com sucesso via SkiaSharp!";
        }
        catch (Exception ex)
        {
            InfoBannerMessage = $"Erro ao gerar cartão: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Memories Actions

    public async Task ToggleMemoryAsync(MemoryItem? memory)
    {
        if (memory == null) return;

        await _db.ToggleMemoryAsync(memory.Id, memory.IsActive);
        OnPropertyChanged(nameof(ActiveMemoriesCount));
    }

    public async Task DeleteMemoryAsync(MemoryItem? memory)
    {
        if (memory == null) return;

        await _db.DeleteMemoryAsync(memory.Id);
        Memories.Remove(memory);
        FilteredMemories.Remove(memory);
        OnPropertyChanged(nameof(ActiveMemoriesCount));
    }

    public async Task AddMemoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMemoryText)) return;

        var newItem = new MemoryItem
        {
            Text = NewMemoryText.Trim(),
            Category = string.IsNullOrWhiteSpace(NewMemoryCategory) ? "Regras" : NewMemoryCategory,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        await _db.SaveMemoryAsync(newItem);
        Memories.Insert(0, newItem);
        ApplyMemoryFilter();
        NewMemoryText = string.Empty;
        OnPropertyChanged(nameof(ActiveMemoriesCount));
        InfoBannerMessage = "✓ Nova memória adicionada à central de contexto.";
    }

    #endregion

    #region Voice Profile Actions

    public void CalibrateVoice()
    {
        if (string.IsNullOrWhiteSpace(SamplePostsText))
        {
            InfoBannerMessage = "Cole exemplos de postagens anteriores para calibrar o tom.";
            return;
        }

        VoiceProfile = _toneAnalyzer.AnalyzeSamples(SamplePostsText);
        InfoBannerMessage = "✓ Métricas linguísticas e perfil de tom calibrados com sucesso!";
    }

    public async Task SaveVoiceAsync()
    {
        VoiceProfile.SampleText = SamplePostsText;
        await _db.SaveVoiceProfileAsync(VoiceProfile);
        InfoBannerMessage = "✓ Perfil de voz gravado no SQLite local com sucesso.";
    }

    #endregion

    #region Settings Actions

    public async Task SaveSettingsAsync()
    {
        // Encrypt tokens using DPAPI
        Settings.BufferAccessTokenEncrypted = SecurityService.Protect(BufferTokenInput);
        Settings.AiApiKeyEncrypted = SecurityService.Protect(AiApiKeyInput);

        await _db.SaveSettingsAsync(Settings);
        _scheduler.UpdateSettings(Settings);
        NextRunText = _scheduler.NextRunFormatted;

        InfoBannerMessage = "✓ Configurações e tokens gravados com segurança (DPAPI).";

        await InitializeMcpAsync();
    }

    public void OpenCacheFolder()
    {
        try
        {
            string cachePath = _db.CachePath;
            if (Directory.Exists(cachePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cachePath,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
