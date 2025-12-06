using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailCore.Interfaces;
using MailCore.Models;
using MailCore.Services;
using System.Collections.ObjectModel;
using System.Diagnostics; // Til Debug.WriteLine
using System.Net.Mail;
using MalikClient.Helpers;
using System.Windows.Threading;

namespace MalikClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // ----------------- Dependencies -----------------
    private readonly IEmailService _emailService;
    private readonly IDialogService _dialogService;
    private readonly INetworkService _networkService;
    private readonly TokenService _tokenService;
    private readonly SessionService _sessionService;


    // ----------------- Public read-only model-data -----------------------
    public UserAccount CurrentUser { get; }

    public string Greeting => $"Hello {CurrentUser.DisplayName}!";

    public ObservableCollection<EmailSummary> InboxItems { get; } = new();

    public string InboxCountText =>
        InboxItems.Count switch
        {
            0 => "No messages",
            1 => "1 message",
            _ => $"{InboxItems.Count} messages"
        };

    [ObservableProperty]
    private EmailSummary? selectedEmail;
    // Samtale der hører til den valgte mail

    public bool HasSelectedEmail => SelectedEmail != null;

    [ObservableProperty]
    private EmailConversation? selectedConversation;

    // Loader-status og fejltekst for samtale-view
    [ObservableProperty]
    private bool isConversationLoading;

    [ObservableProperty]
    private string? conversationStatusMessage;


    private CancellationTokenSource? _loadInboxCts;
    private CancellationTokenSource? _loadConversationCts;



    public event Action? LogoutRequested;

    // ----------------- Compose-state -------------------
    [ObservableProperty] private string subject = string.Empty;
    [ObservableProperty] private string body = string.Empty;
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isComposeOpen;
    [ObservableProperty] private bool isCcVisible;
    [ObservableProperty] private bool isBccVisible;

    // ----------------- Recipient validation state -----------------------
    [ObservableProperty] private bool hasInvalidTo;
    [ObservableProperty] private bool hasInvalidCc;
    [ObservableProperty] private bool hasInvalidBcc;
    [ObservableProperty] private bool hasInvalidRecipients;
    [ObservableProperty] private string invalidRecipientsMessage = string.Empty;

    //------------------ inbox properties -----------------------
    [ObservableProperty] private bool isInboxLoading;
    [ObservableProperty] private bool isInboxSelected;


    //------------------ inbox / connection state -----------------------
    [ObservableProperty] private bool isOffline;


    // ----------------- RECIPIENT CHIPS ---------------------
    public ObservableCollection<RecipientChip> ToRecipients { get; } = new();
    public ObservableCollection<RecipientChip> CcRecipients { get; } = new();
    public ObservableCollection<RecipientChip> BccRecipients { get; } = new();

    [ObservableProperty] private string toInput = string.Empty;
    [ObservableProperty] private string ccInput = string.Empty;
    [ObservableProperty] private string bccInput = string.Empty;

    private bool _isHandlingToInputChange;
    private bool _isHandlingCcInputChange;
    private bool _isHandlingBccInputChange;

    // ----------------- Ctor -----------------------
    public MainViewModel(
        UserAccount currentUser,
        IEmailService emailService,
        IDialogService dialogService,
        INetworkService networkService,
        TokenService tokenService,
        SessionService sessionService
        )
    {
        CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

    }

    // ----------------- Commands -----------------------

    [RelayCommand]
    private void Logout()
    {
        // Annuller evt. igangværende load ved logout
        _loadInboxCts?.Cancel();
        _tokenService.DeleteCacheToken(CurrentUser.Id);
   
        _sessionService.ClearActiveUser();
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private void NewEmail()
    {
        Subject = string.Empty;
        Body = string.Empty;
        StatusMessage = string.Empty;
        IsSending = false;

        HasInvalidRecipients = false;
        InvalidRecipientsMessage = string.Empty;

        ToRecipients.Clear();
        CcRecipients.Clear();
        BccRecipients.Clear();

        ToInput = string.Empty;
        CcInput = string.Empty;
        BccInput = string.Empty;

        IsCcVisible = false;
        IsBccVisible = false;
        IsComposeOpen = true;
    }

    [RelayCommand]
    private void CloseCompose() => IsComposeOpen = false;

    [RelayCommand]
    private void ToggleCc() => IsCcVisible = true;

    [RelayCommand]
    private void ToggleBcc() => IsBccVisible = true;

    /// <summary>
    /// OPTIMERET: Henter mails på en baggrundstråd med beskyttelse mod race conditions.
    /// </summary>

    [RelayCommand]
    private async Task LoadInbox()
    {
        IsInboxSelected = true;


        if (IsInboxLoading)
            return;

        if (CurrentUser == null)
            return;

        UpdateOfflineStatus();

        var previousSelectedId = SelectedEmail?.Id;

        _loadInboxCts?.Cancel();
        _loadInboxCts?.Dispose();
        _loadInboxCts = new CancellationTokenSource();
        var ct = _loadInboxCts.Token;

        try
        {
            IsInboxLoading = true;

            // 1) Hent items (IO-bound → ingen UI blokering)
            var serverItems = await _emailService.GetInboxAsync(CurrentUser, ct);

            if (ct.IsCancellationRequested)
                return;

            InboxItems.Clear();
            foreach (var item in serverItems)
                InboxItems.Add(item);


            // 3) Genskab selection
            if (!string.IsNullOrEmpty(previousSelectedId))
            {
                SelectedEmail = InboxItems.FirstOrDefault(x => x.Id == previousSelectedId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Inbox Load Error] {ex}");
            StatusMessage = "Unexpected error while loading inbox.";
        }
        finally
        {
            IsInboxLoading = false;
        }
    }






    // ----------------- SEND EMAIL FLOW -----------------------

    [RelayCommand]
    private async Task SendEmail()
    {
        if (IsSending) return;

        StatusMessage = string.Empty;

        // Auto-konverter input → chip før send
        if (!string.IsNullOrWhiteSpace(ToInput)) { AddChip(ToInput.Trim(), ToRecipients); ToInput = string.Empty; }
        if (!string.IsNullOrWhiteSpace(CcInput)) { AddChip(CcInput.Trim(), CcRecipients); CcInput = string.Empty; }
        if (!string.IsNullOrWhiteSpace(BccInput)) { AddChip(BccInput.Trim(), BccRecipients); BccInput = string.Empty; }

        if (!_networkService.HasInternet())
        {
            StatusMessage = "No internet connection.";
            return;
        }

        var toList = ToRecipients.Select(r => r.Address).ToList();
        var ccList = CcRecipients.Select(r => r.Address).ToList();
        var bccList = BccRecipients.Select(r => r.Address).ToList();

        if (!toList.Any())
        {
            StatusMessage = "Please enter at least one recipient.";
            return;
        }

        var all = toList.Concat(ccList).Concat(bccList).ToList();
        var invalid = all.Where(a => !IsValidEmail(a)).ToList();

        if (invalid.Any())
        {
            HasInvalidRecipients = true;
            InvalidRecipientsMessage = "Invalid addresses: " + string.Join(", ", invalid);
            StatusMessage = InvalidRecipientsMessage;
            return;
        }

        HasInvalidRecipients = false;
        IsSending = true;
        StatusMessage = "Sending email...";

        try
        {
            var message = new EmailMessage
            {
                From = CurrentUser.Email,
                Subject = Subject ?? string.Empty,
                HtmlBody = Body ?? string.Empty,
                TextBody = Body ?? string.Empty
            };

            message.To.AddRange(toList);
            message.Cc.AddRange(ccList);
            message.Bcc.AddRange(bccList);

            // Send i baggrunden
            await Task.Run(() => _emailService.SendEmailAsync(CurrentUser, message));

            StatusMessage = "Email sent.";
            await Task.Delay(1500);

            IsComposeOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error sending: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }
    [RelayCommand]
    private void CloseConversation()
    {
        SelectedEmail = null;
        SelectedConversation = null;
        ConversationStatusMessage = string.Empty;
    }


    // ----------------- Change-hooks -----------------------

    partial void OnToInputChanged(string value) =>
        HandleInputChange(value, ToRecipients, () => ToInput = string.Empty, ref _isHandlingToInputChange);

    partial void OnCcInputChanged(string value) =>
        HandleInputChange(value, CcRecipients, () => CcInput = string.Empty, ref _isHandlingCcInputChange);

    partial void OnBccInputChanged(string value) =>
        HandleInputChange(value, BccRecipients, () => BccInput = string.Empty, ref _isHandlingBccInputChange);

    // Kaldt automatisk når SelectedEmail ændrer sig (CommunityToolkit hook)
    partial void OnSelectedEmailChanged(EmailSummary? value)
    {
        // Fire-and-forget – vi håndterer fejl inde i helperen
        _ = LoadConversationAsync(value);
        OnPropertyChanged(nameof(HasSelectedEmail));
    }



    // ----------------- Helpers -----------------------

    // ----------------- Network helpers -----------------------
    private void UpdateOfflineStatus()
    {
        try
        {
            IsOffline = !_networkService.HasInternet();
        }
        catch
        {
            // Hvis vi ikke kan tjekke, antag offline
            IsOffline = true;
        }
    }


   


    private async Task LoadConversationAsync(EmailSummary? summary)
    {
        // Annuller evt. tidligere samtale-load
        _loadConversationCts?.Cancel();
        _loadConversationCts?.Dispose();
        _loadConversationCts = null;

        // Ryd tidligere state
        ConversationStatusMessage = string.Empty;
        SelectedConversation = null;

        if (summary == null)
            return;

        if (!_networkService.HasInternet())
        {
            // Opdater offline-flag – men vi *afbryder ikke*
            UpdateOfflineStatus();
        }

        var cts = new CancellationTokenSource();
        _loadConversationCts = cts;
        var ct = cts.Token;

        try
        {
            IsConversationLoading = true;

            EmailConversation conversation;

            // Forudsætter at EmailSummary har ThreadId-felt fra Step 1
            if (!string.IsNullOrWhiteSpace(summary.ThreadId))
            {
                conversation = await _emailService.GetConversationAsync(
                    CurrentUser,
                    summary.ThreadId!,
                    ct);
            }
            else
            {
                // Fallback: hent bare den ene besked
                var message = await _emailService.GetMessageAsync(
                    CurrentUser,
                    summary.Id,
                    ct);

                conversation = new EmailConversation(
                    message.ThreadId ?? message.Subject ?? summary.Id,
                    new[] { message });
            }

            if (ct.IsCancellationRequested)
                return;

            SelectedConversation = conversation;
        }
        catch (OperationCanceledException)
        {
            // Ignorér – betyder bare at brugeren valgte en anden mail hurtigt
        }
        catch (InvalidOperationException ex)
        {
            ConversationStatusMessage = ex.Message;
            Debug.WriteLine($"[Conversation Load Error - known] {ex}");
        }
        catch (Exception ex)
        {
            ConversationStatusMessage = "Unexpected error while loading conversation. Check internetconnection";
            Debug.WriteLine($"[Conversation Load Error - unexpected] {ex}");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsConversationLoading = false;
        }
    }



    private void HandleInputChange(string value, ObservableCollection<RecipientChip> list, Action clearAction, ref bool guard)
    {
        if (guard || string.IsNullOrEmpty(value)) return;

        char last = value[^1];
        if (last != ',' && last != ';' && last != '\n' && last != '\r') return;

        var token = value[..^1].Trim();

        guard = true;
        clearAction();
        guard = false;

        if (!string.IsNullOrWhiteSpace(token))
            AddChip(token, list);
    }

    private void AddChip(string text, ObservableCollection<RecipientChip> list)
    {
        var chip = new RecipientChip
        {
            Address = text,
            IsValid = IsValidEmail(text)
        };
        list.Add(chip);
        ValidateRecipientsPreview();
    }

    private static bool IsValidEmail(string address)
    {
        try { _ = new MailAddress(address); return true; }
        catch { return false; }
    }

    private void ValidateRecipientsPreview()
    {
        HasInvalidTo = ToRecipients.Any(r => !IsValidEmail(r.Address));
        HasInvalidCc = CcRecipients.Any(r => !IsValidEmail(r.Address));
        HasInvalidBcc = BccRecipients.Any(r => !IsValidEmail(r.Address));

        var allInvalid = ToRecipients.Concat(CcRecipients).Concat(BccRecipients)
            .Where(r => !IsValidEmail(r.Address))
            .Select(r => r.Address)
            .Distinct()
            .ToList();

        if (allInvalid.Any())
        {
            HasInvalidRecipients = true;
            InvalidRecipientsMessage = "Invalid: " + string.Join(", ", allInvalid);
        }
        else
        {
            HasInvalidRecipients = false;
            InvalidRecipientsMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveRecipient(RecipientChip chip)
    {
        ToRecipients.Remove(chip);
        CcRecipients.Remove(chip);
        BccRecipients.Remove(chip);
        ValidateRecipientsPreview();
    }
}