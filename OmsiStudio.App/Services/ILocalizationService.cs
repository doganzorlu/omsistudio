using System;

namespace OmsiStudio.App.Services;

public interface ILocalizationService
{
    string CurrentCulture { get; }
    string this[string key] { get; }
    void SetCulture(string cultureName);
    event EventHandler? CultureChanged;
}
