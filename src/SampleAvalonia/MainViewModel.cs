
using CommunityToolkit.Mvvm.ComponentModel;
using StringLocalizationGenerator;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SampleAvalonia;

public partial class MainViewModel : ObservableObject
{
    private Dictionary<string, string> LangDict = new Dictionary<string, string>()
    {
        { "English","en"},
        { "日本語","jp"},
        { "Default","default"},
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

	partial void OnSelectedLanguageChanged(string? oldValue, string newValue)
	{
		if (LangDict.TryGetValue(newValue, out var key))
		{
			StringLocalizationManager.ChangeLanguage(key);
		}
	}
}
