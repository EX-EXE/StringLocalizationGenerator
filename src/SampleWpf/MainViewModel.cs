
using CommunityToolkit.Mvvm.ComponentModel;
using StringLocalizationGenerator;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SampleWpf;

public partial class MainViewModel : ObservableObject
{
    private Dictionary<string, string> LangDict = new Dictionary<string, string>()
    {
        { "English","en"},
        { "日本語","jp"},
        { "Default","default"},
    };

    public ObservableCollection<string> Languages { get; set; } = new ObservableCollection<string>();

    public ObservableCollection<string> KeyNames { get; set; } = new ObservableCollection<string>();

    [ObservableProperty]
    private string selectedKeyName = string.Empty;

    [ObservableProperty]
    private StringLocalizationGenerator.StringLocalizationKeyType selectedKeyType = StringLocalizationGenerator.StringLocalizationKeyType.ID_NO;

    [ObservableProperty]
    private string selectedLanguage = "default";

    public MainViewModel()
    {
        foreach (var key in LangDict.Keys)
        {
            Languages.Add(key);
        }
        foreach (var key in StringLocalizationManager.KeyNames)
        {
            KeyNames.Add(key);
        }

        var first = LangDict.First();
        SelectedLanguage = LangDict.First().Key;
        StringLocalizationManager.ChangeLanguage(first.Value);
        SelectedKeyName = KeyNames.First();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (LangDict.TryGetValue(value, out var key))
        {
            StringLocalizationManager.ChangeLanguage(key);
        }
    }
    partial void OnSelectedKeyNameChanged(string value)
    {
        SelectedKeyType = StringLocalizationManager.GetKeyType(value);
    }

}
