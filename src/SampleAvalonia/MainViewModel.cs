
using CommunityToolkit.Mvvm.ComponentModel;
using HarfBuzzSharp;
using StringLocalizationGenerator;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using static System.Net.Mime.MediaTypeNames;

namespace SampleAvalonia;

public partial class MainViewModel : ObservableObject
{
    static ReadOnlySpan<byte> Aa => [0x11, 0x30,];

    private Dictionary<string, StringLocalizationLanguageType> LangDict = new()
    {
        { "日本語",StringLocalizationLanguageType.JP},
        { "English", StringLocalizationLanguageType.EN},
        { "Default",StringLocalizationLanguageType.DEFAULT},
    };

    public ObservableCollection<string> Languages { get; set; } = new ObservableCollection<string>();

    [ObservableProperty]
    private StringLocalizationGenerator.StringLocalizationKeyType dynamicKey = StringLocalizationGenerator.StringLocalizationKeyType.ID_NO;

    [ObservableProperty]
    private string selectedLanguage = "default";

    public MainViewModel()
    {
        foreach (var key in LangDict.Keys)
        {
            Languages.Add(key);
        }
        var first = LangDict.First();
        SelectedLanguage = first.Key;
        StringLocalizationManager.ChangeLanguage(first.Value);
    }

    partial void OnSelectedLanguageChanged(string lang)
    {
        if (LangDict.TryGetValue(lang, out var key))
        {
            StringLocalizationManager.ChangeLanguage(key);
        }
    }

}
