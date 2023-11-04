# StringLocalizationGenerator
Generate C# source for localization from json file.

|  |  |
|---|---|
| ![preview](https://user-images.githubusercontent.com/114784289/280459762-728222c9-7fa1-45a4-ab21-3e1bd90c8f88.gif) | ![source](https://user-images.githubusercontent.com/114784289/280459845-4d635ad1-7385-4144-b5f9-be71906cb02f.png) ![source](https://user-images.githubusercontent.com/114784289/280460029-613b3a3c-8f27-4690-ad60-00eb34de636c.png)  |

# How To Use
## Install by nuget
PM> Install-Package 

## Create Language Json File

```json
# Configuration
{
  "[STRING_ID]": {
    "[LANGUAGE1]": "[TEXT1]",
    "[LANGUAGE2]": "[TEXT2]",
  },
}
```
```json
# Sample
{
  "ID_YES": {
    "EN": "yes",
    "JP": "はい"
  },
  "ID_NO": {
    "EN": "no",
    "JP": "いいえ"
  }
}
```

## Add <AdditionalFiles> to .csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
	<ItemGroup>
		<AdditionalFiles Include="StringLocalization.json" />
	</ItemGroup>
</Project>
```

## (WPF Only) Add OutputWpfMarkupExtension to .csproj
```xml
<ItemGroup>
  <CompilerVisibleProperty Include="StringLocalizationGenerator_OutputWpfMarkupExtension" />
</ItemGroup>
<PropertyGroup>
  <StringLocalizationGenerator_OutputWpfMarkupExtension>Enable</StringLocalizationGenerator_OutputWpfMarkupExtension>
</PropertyGroup>
```

## (Avalonia Only) Add OutputAvaloniaMarkupExtension to .csproj And <br/>Create StringLocalizationGenerator.BindingExtension.cs File
```xml
<ItemGroup>
  <CompilerVisibleProperty Include="StringLocalizationGenerator_OutputAvaloniaMarkupExtension" />
</ItemGroup>
<PropertyGroup>
  <StringLocalizationGenerator_OutputAvaloniaMarkupExtension>Enable</StringLocalizationGenerator_OutputAvaloniaMarkupExtension>
</PropertyGroup>
```
```csharp
namespace StringLocalizationGenerator;
public partial class BindingExtension
{
}
```

## Use xaml Or axaml
```xaml
<UserControl xmlns:loc="clr-namespace:StringLocalizationGenerator">
  <TextBlock Text="{loc:Binding Key=[STRING_ID]}"></TextBlock>
  <TextBlock Text="{loc:Binding KeyBinding={Binding KeyType}}"></TextBlock>
</UserControl>
```
