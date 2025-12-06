using CommunityToolkit.Mvvm.ComponentModel;

namespace MalikClient.ViewModels;

/// <summary>
/// UI-model for én modtager i To-feltet som en "chip".
/// Det er ren klient-/UI-ting (ikke domænemodel).
/// </summary>
public partial class RecipientChip : ObservableObject
{
    /// <summary>Det der vises på chippen (typisk email eller navn).</summary>
    [ObservableProperty]
    private string address = string.Empty;

    /// <summary>Bruger vi i næste step til rød/grøn (gyldig/ugyldig).</summary>
    [ObservableProperty]
    private bool isValid = true;
}
